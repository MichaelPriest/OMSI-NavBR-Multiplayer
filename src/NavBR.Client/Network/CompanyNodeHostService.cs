using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.Sockets;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using NavBR.Client.Operations;
using NavBR.Shared.Network;

namespace NavBR.Client.Network;

internal sealed class CompanyNodeHostService : IAsyncDisposable
{
    private WebApplication? _app;

    public bool IsRunning => _app is not null;
    public int Port { get; private set; }
    public string LocalUrl => IsRunning ? $"http://127.0.0.1:{Port}" : string.Empty;

    public async Task<CompanyNodeSnapshot> StartAsync(
        NavBRPublicIdentity identity,
        VirtualCompanyData localCompany,
        int port = 27740,
        CancellationToken cancellationToken = default)
    {
        if (_app is not null)
        {
            return CompanyNodeStore.LoadCompany()
                ?? throw new InvalidOperationException("Company Node ativo sem empresa configurada.");
        }
        if (port is < 1024 or > 65535)
        {
            throw new ArgumentOutOfRangeException(nameof(port));
        }

        var company = CompanyNodeStore.EnsureLocalCompany(identity, localCompany);
        var builder = WebApplication.CreateBuilder(Array.Empty<string>());
        builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
        var app = builder.Build();

        app.MapGet("/health", () => Results.Ok(new
        {
            status = "ok",
            service = "NavBR.CompanyNode",
            companyId = company.CompanyId,
            company = company.Name,
            schema = company.SchemaVersion,
            serverUtc = DateTimeOffset.UtcNow
        }));
        app.MapGet("/api/company", () =>
            CompanyNodeStore.LoadCompany() is { } snapshot
                ? Results.Ok(snapshot)
                : Results.NotFound(new { error = "company_not_configured" }));
        app.MapGet("/api/company/members", () =>
            CompanyNodeStore.LoadCompany() is { } snapshot
                ? Results.Ok(snapshot.Members)
                : Results.NotFound(new { error = "company_not_configured" }));
        app.MapPost("/api/company/join", (CompanyJoinRequest request) =>
        {
            var result = CompanyNodeStore.TryJoin(request);
            return result.Success ? Results.Ok(result) : Results.BadRequest(result);
        });

        try
        {
            await app.StartAsync(cancellationToken);
        }
        catch
        {
            await app.DisposeAsync();
            throw;
        }

        _app = app;
        Port = port;
        return company;
    }

    public IReadOnlyList<string> GetLanUrls()
    {
        if (!IsRunning)
        {
            return Array.Empty<string>();
        }

        try
        {
            return Dns.GetHostEntry(Dns.GetHostName())
                .AddressList
                .Where(address =>
                    address.AddressFamily == AddressFamily.InterNetwork &&
                    !IPAddress.IsLoopback(address))
                .Select(address => $"http://{address}:{Port}")
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        var app = _app;
        _app = null;
        Port = 0;
        if (app is null)
        {
            return;
        }

        try
        {
            await app.StopAsync(cancellationToken);
        }
        finally
        {
            await app.DisposeAsync();
        }
    }

    public async ValueTask DisposeAsync() => await StopAsync();
}

internal sealed record CompanyMembershipLink(
    string CompanyId,
    string CompanyName,
    string NodeUrl,
    CompanyRole Role,
    DateTimeOffset JoinedAtUtc);

internal static class CompanyMembershipStore
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "OMSI NavBR Multiplayer",
        "Network",
        "company-membership.json");

    public static CompanyMembershipLink? Load()
    {
        try
        {
            return File.Exists(FilePath)
                ? System.Text.Json.JsonSerializer.Deserialize<CompanyMembershipLink>(File.ReadAllText(FilePath))
                : null;
        }
        catch
        {
            return null;
        }
    }

    public static void Save(CompanyMembershipLink membership)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(
            FilePath,
            System.Text.Json.JsonSerializer.Serialize(
                membership,
                new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
    }
}

internal sealed class NavBRNetworkRuntime : IAsyncDisposable
{
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(8d) };

    public CompanyNodeHostService CompanyNode { get; } = new();
    public NavBRPublicIdentity Identity => NavBRIdentityStore.LoadOrCreate();
    public CompanyMembershipLink? Membership => CompanyMembershipStore.Load();

    public async Task<CompanyNodeSnapshot> StartCompanyNodeAsync(
        int port = 27740,
        CancellationToken cancellationToken = default)
    {
        var identity = Identity;
        var company = await CompanyNode.StartAsync(
            identity,
            VirtualCompanyStore.Load(),
            port,
            cancellationToken);
        var self = company.Members.First(member =>
            string.Equals(member.PlayerId, identity.PlayerId, StringComparison.OrdinalIgnoreCase));
        CompanyMembershipStore.Save(new CompanyMembershipLink(
            company.CompanyId,
            company.Name,
            CompanyNode.LocalUrl,
            self.Role,
            self.JoinedAtUtc));
        return company;
    }

    public string CreateDriverInvite() =>
        CompanyNodeStore.CreateInvite(Identity.PlayerId, CompanyRole.Driver, TimeSpan.FromDays(7), 1);

    public async Task<CompanyJoinResponse> JoinAsync(
        string nodeUrl,
        string inviteCode,
        CancellationToken cancellationToken = default)
    {
        var normalizedUrl = NormalizeNodeUrl(nodeUrl);
        var company = await _http.GetFromJsonAsync<CompanyNodeSnapshot>(
            $"{normalizedUrl}/api/company",
            cancellationToken)
            ?? throw new InvalidOperationException("O Company Node não retornou os dados da empresa.");

        var identity = Identity;
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var request = new CompanyJoinRequest(
            company.CompanyId,
            inviteCode.Trim(),
            identity,
            timestamp,
            NavBRIdentityStore.SignJoinRequest(company.CompanyId, inviteCode, timestamp));

        using var response = await _http.PostAsJsonAsync(
            $"{normalizedUrl}/api/company/join",
            request,
            cancellationToken);
        var result = await response.Content.ReadFromJsonAsync<CompanyJoinResponse>(cancellationToken: cancellationToken)
            ?? new CompanyJoinResponse(false, "invalid_response", null);
        if (!response.IsSuccessStatusCode || !result.Success || result.Company is null)
        {
            return result with { Success = false };
        }

        var member = result.Company.Members.FirstOrDefault(item =>
            string.Equals(item.PlayerId, identity.PlayerId, StringComparison.OrdinalIgnoreCase));
        if (member is null)
        {
            return new CompanyJoinResponse(false, "membership_not_returned", result.Company);
        }

        CompanyMembershipStore.Save(new CompanyMembershipLink(
            result.Company.CompanyId,
            result.Company.Name,
            normalizedUrl,
            member.Role,
            member.JoinedAtUtc));
        return result;
    }

    public async ValueTask DisposeAsync()
    {
        await CompanyNode.DisposeAsync();
        _http.Dispose();
    }

    private static string NormalizeNodeUrl(string value)
    {
        var text = value?.Trim().TrimEnd('/') ?? string.Empty;
        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException("Informe um endereço válido do Company Node, por exemplo http://192.168.0.10:27740.");
        }
        return uri.GetLeftPart(UriPartial.Authority);
    }
}
