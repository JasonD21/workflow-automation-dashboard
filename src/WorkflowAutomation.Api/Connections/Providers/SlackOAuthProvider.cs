using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace WorkflowAutomation.Api.Connections.Providers;

public class SlackOAuthProvider(IOptions<SlackOptions> options, HttpClient http) : IOAuthProvider
{
    private readonly SlackOptions _options = options.Value;
    private const string Scopes = "channels:read,channels:history,chat:write";

    public Provider Provider => Provider.Slack;

    public string BuildAuthorizeUrl(string state, string redirectUri)
    {
        var query = new Dictionary<string, string?>
        {
            ["client_id"] = _options.ClientId,
            ["scope"] = Scopes,
            ["redirect_uri"] = redirectUri,
            ["state"] = state
        };
        return QueryHelpers.AddQueryString("https://slack.com/oauth/v2/authorize", query);
    }

    public async Task<OAuthTokenResult> ExchangeCodeAsync(string code, string redirectUri, CancellationToken ct = default)
    {
        var form = new Dictionary<string, string>
        {
            ["client_id"] = _options.ClientId,
            ["client_secret"] = _options.ClientSecret,
            ["code"] = code,
            ["redirect_uri"] = redirectUri
        };

        using var resp = await http.PostAsync("https://slack.com/api/oauth.v2.access", new FormUrlEncodedContent(form), ct);
        var payload = await resp.Content.ReadFromJsonAsync<SlackOAuthResponse>(ct);

        if (payload is null || !payload.Ok)
            throw new InvalidOperationException($"Slack OAuth failed: {payload?.Error ?? "unknown"}");

        return new OAuthTokenResult(
            AccessToken: payload.AccessToken!,
            RefreshToken: null,                  // Slack bot tokens don't expire
            AccessTokenExpiresAt: null,
            RefreshTokenExpiresAt: null,
            ProviderAccountId: payload.Team!.Id,
            DisplayName: payload.Team.Name,
            GrantedScopes: payload.Scope);
    }

    private record SlackOAuthResponse
    {
        [JsonPropertyName("ok")] public bool Ok { get; init; }
        [JsonPropertyName("error")] public string? Error { get; init; }
        [JsonPropertyName("access_token")] public string? AccessToken { get; init; }
        [JsonPropertyName("scope")] public string? Scope { get; init; }
        [JsonPropertyName("team")] public SlackTeam? Team { get; init; }
    }

    private record SlackTeam
    {
        [JsonPropertyName("id")] public string Id { get; init; } = default!;
        [JsonPropertyName("name")] public string Name { get; init; } = default!;
    }
}