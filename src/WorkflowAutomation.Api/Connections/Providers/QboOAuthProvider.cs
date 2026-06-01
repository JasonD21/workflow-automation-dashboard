using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace WorkflowAutomation.Api.Connections.Providers;

public class QboOAuthProvider(IOptions<QboOptions> options, HttpClient http) : IOAuthProvider
{
    private readonly QboOptions _options = options.Value;
    private const string Scope = "com.intuit.quickbooks.accounting";
    private const string AuthorizeUrl = "https://appcenter.intuit.com/connect/oauth2";
    private const string TokenUrl = "https://oauth.platform.intuit.com/oauth2/v1/tokens/bearer";

    public Provider Provider => Provider.QuickBooks;

    public string BuildAuthorizeUrl(string state, string redirectUri)
    {
        var query = new Dictionary<string, string?>
        {
            ["client_id"] = _options.ClientId,
            ["response_type"] = "code",
            ["scope"] = Scope,
            ["redirect_uri"] = redirectUri,
            ["state"] = state
        };
        return QueryHelpers.AddQueryString(AuthorizeUrl, query);
    }

    public async Task<OAuthTokenResult> ExchangeCodeAsync(string code, string redirectUri, IReadOnlyDictionary<string, string?> callbackParams, CancellationToken ct = default)
    {
        var realmId = callbackParams.GetValueOrDefault("realmId") ?? throw new InvalidOperationException("QuickBooks callback is missing realmId.");

        using var request = new HttpRequestMessage(HttpMethod.Post, TokenUrl);
        var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_options.ClientId}:{_options.ClientSecret}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", basic);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = redirectUri
        });

        using var resp = await http.SendAsync(request, ct);
        resp.EnsureSuccessStatusCode();
        var token = await resp.Content.ReadFromJsonAsync<QboTokenResponse>(ct) ?? throw new InvalidOperationException("QuickBooks token response was empty.");

        var now = DateTimeOffset.UtcNow;
        return new OAuthTokenResult(
            AccessToken: token.AccessToken,
            RefreshToken: token.RefreshToken,
            AccessTokenExpiresAt: now.AddSeconds(token.ExpiresIn),
            RefreshTokenExpiresAt: now.AddSeconds(token.RefreshTokenExpiresIn),
            ProviderAccountId: realmId,
            DisplayName: $"QuickBooks (Company {realmId})",
            GrantedScopes: Scope
        );
    }

    private record QboTokenResponse
    {
        [JsonPropertyName("access_token")] public string AccessToken { get; init; } = default!;
        [JsonPropertyName("refresh_token")] public string RefreshToken { get; init; } = default!;
        [JsonPropertyName("expires_in")] public int ExpiresIn { get; init; }
        [JsonPropertyName("x_refresh_token_expires_in")] public int RefreshTokenExpiresIn { get; init; }
    }
}