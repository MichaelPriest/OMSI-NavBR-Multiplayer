using System.Net.Http;
using System.Net.Http.Json;
using NavBR.Shared.Multiplayer;

namespace NavBR.Client.Multiplayer;

internal sealed class ExternalPortProbeClient : IDisposable
{
    public const string ProbeUrlEnvironmentVariable = "NAVBR_EXTERNAL_PROBE_URL";

    private readonly HttpClient _httpClient;
    private readonly Uri? _endpoint;

    public ExternalPortProbeClient()
    {
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(8)
        };
        _endpoint = TryBuildEndpoint(
            Environment.GetEnvironmentVariable(ProbeUrlEnvironmentVariable));
    }

    public bool IsConfigured => _endpoint is not null;
    public string? ServiceOrigin => _endpoint?.GetLeftPart(UriPartial.Authority);

    public async Task<ExternalPortProbeResult?> ProbeAsync(
        CancellationToken cancellationToken = default)
    {
        if (_endpoint is null)
        {
            return null;
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, _endpoint)
        {
            Content = null
        };
        request.Headers.CacheControl = new System.Net.Http.Headers.CacheControlHeaderValue
        {
            NoCache = true,
            NoStore = true
        };

        using var response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Probe service returned HTTP {(int)response.StatusCode}.");
        }

        return await response.Content.ReadFromJsonAsync<ExternalPortProbeResult>(
            cancellationToken: cancellationToken);
    }

    public void Dispose() => _httpClient.Dispose();

    private static Uri? TryBuildEndpoint(string? value)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized) ||
            !Uri.TryCreate(normalized, UriKind.Absolute, out var baseUri) ||
            baseUri.Scheme is not ("http" or "https"))
        {
            return null;
        }

        var baseText = baseUri.AbsoluteUri.TrimEnd('/');
        return new Uri($"{baseText}/api/network/external-port-probe", UriKind.Absolute);
    }
}
