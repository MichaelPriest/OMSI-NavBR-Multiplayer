using System.Net.Http;
using System.Net.Http.Json;
using NavBR.Shared.Multiplayer;

namespace NavBR.Client.Multiplayer;

internal sealed class PublicRoomDirectoryClient : IDisposable
{
    private readonly HttpClient _http = new()
    {
        Timeout = TimeSpan.FromSeconds(5)
    };

    public async Task<IReadOnlyList<PublicRoomSummary>> GetRoomsAsync(
        string serverUrl,
        CancellationToken cancellationToken = default)
    {
        var endpoint = BuildRoomsEndpoint(serverUrl);
        var rooms = await _http.GetFromJsonAsync<PublicRoomSummary[]>(endpoint, cancellationToken);
        return rooms ?? Array.Empty<PublicRoomSummary>();
    }

    public void Dispose() => _http.Dispose();

    private static Uri BuildRoomsEndpoint(string serverUrl)
    {
        var value = (serverUrl ?? string.Empty).Trim();
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException("Invalid multiplayer server URL.", nameof(serverUrl));
        }

        var builder = new UriBuilder(uri)
        {
            Path = "/api/rooms",
            Query = string.Empty,
            Fragment = string.Empty
        };
        return builder.Uri;
    }
}
