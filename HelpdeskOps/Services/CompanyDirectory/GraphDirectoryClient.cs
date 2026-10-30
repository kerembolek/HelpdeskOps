using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace HelpdeskOps.Services.CompanyDirectory;

// Reads the tenant's users from Microsoft Graph as the app itself (client credentials), which needs the application
// permission User.Read.All with admin consent. Plain HTTP calls are enough here, so no Graph SDK is added.
public class GraphDirectoryClient : IDirectoryClient
{
    private const string UserFields = "id,givenName,surname,displayName,mail,userPrincipalName,department,accountEnabled,userType";

    private readonly HttpClient _http;
    private readonly EntraOptions _options;

    public GraphDirectoryClient(HttpClient http, IOptions<DirectoryOptions> options)
    {
        _http = http;
        _options = options.Value.Entra;
    }

    public async Task<IReadOnlyList<DirectoryUser>> GetUsersAsync(CancellationToken cancellationToken)
    {
        var token = await GetTokenAsync(cancellationToken);
        var people = new List<DirectoryUser>();

        var url = $"{_options.GraphEndpoint.TrimEnd('/')}/v1.0/users?$select={UserFields}&$top=999";
        while (url != null)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var document = await SendAsync(request, cancellationToken);

            foreach (var item in document.RootElement.GetProperty("value").EnumerateArray())
            {
                if (ToPerson(item) is DirectoryUser person)
                {
                    people.Add(person);
                }
            }

            url = document.RootElement.TryGetProperty("@odata.nextLink", out var next) ? next.GetString() : null;
        }

        return people;
    }

    private async Task<string> GetTokenAsync(CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{_options.Instance.TrimEnd('/')}/{_options.TenantId}/oauth2/v2.0/token")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = _options.ClientId,
                ["client_secret"] = _options.ClientSecret,
                ["scope"] = $"{_options.GraphEndpoint.TrimEnd('/')}/.default"
            })
        };

        using var document = await SendAsync(request, cancellationToken);
        return document.RootElement.GetProperty("access_token").GetString()
            ?? throw new DirectoryUnavailableException("Microsoft Graph anahtarı alınamadı.");
    }

    private async Task<JsonDocument> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _http.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new DirectoryUnavailableException($"Microsoft Graph {(int)response.StatusCode} hatası döndürdü.");
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            throw new DirectoryUnavailableException("Microsoft Graph'a ulaşılamadı.", ex);
        }
    }

    // Guests belong to another company and are not staff, and neither are outside accounts marked #EXT#, such as a
    // tenant created from a personal Microsoft account, even when Entra lists them as members. Entries with no address
    // cannot sign in.
    private static DirectoryUser? ToPerson(JsonElement item)
    {
        var principalName = Text(item, "userPrincipalName");
        var email = Text(item, "mail") ?? principalName;
        if (Text(item, "id") is not string id || email == null || Text(item, "userType") is "Guest"
            || principalName?.Contains("#EXT#", StringComparison.OrdinalIgnoreCase) == true)
        {
            return null;
        }

        var (first, last) = DirectoryNames.Split(Text(item, "givenName"), Text(item, "surname"), Text(item, "displayName"), email);
        var enabled = item.TryGetProperty("accountEnabled", out var value) && value.ValueKind == JsonValueKind.True;
        return new DirectoryUser(id, email, first, last, Text(item, "department"), enabled);
    }

    private static string? Text(JsonElement item, string name) =>
        item.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString())
            ? value.GetString()!.Trim()
            : null;
}
