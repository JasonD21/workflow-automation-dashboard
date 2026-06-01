using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace WorkflowAutomation.Api.Connections.Providers;

public class GoogleOAuthProvider(IOptions<GoogleOptions> options, HttpClient http) : IOAuthProvider
{
    private readonly GoogleOptions _options = options.Value;
    private const string Scope = "openid email https://www.googleapis.com/auth/calendar.events.readonly";
    private const string AuthorizeUrl = "https://accounts.google.com/o/oauth2/v2/auth";
    private const string TokenUrl = "https://oauth2.googleapis.com/token";

    public Provider Provider => Provider.GoogleCalendar;

    public string BuildAuthorizeUrl(string state, string redirectUri)
    {
        var query = new Dictionary<string, string?>
        {
            ["client_id"] = _options.ClientId,
            ["redirect_uri"] = redirectUri,
            ["response_type"] = "code",
            ["scope"] = Scope,
            ["access_type"] = "offline",   // required to receive a refresh token
            ["prompt"] = "consent",        // force refresh-token issuance on every consent
            ["state"] = state
        };
        return QueryHelpers.AddQueryString(AuthorizeUrl, query);
    }

    public async Task<OAuthTokenResult> ExchangeCodeAsync(string code, string redirectUri, IReadOnlyDictionary<string, string?> callbackParams, CancellationToken ct = default)
    {
        using var resp = await http.PostAsync(TokenUrl,
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["code"] = code,
                ["client_id"] = _options.ClientId,
                ["client_secret"] = _options.ClientSecret,
                ["redirect_uri"] = redirectUri,
                ["grant_type"] = "authorization_code"
            }), ct);
        resp.EnsureSuccessStatusCode();

        var token = await resp.Content.ReadFromJsonAsync<GoogleTokenResponse>(ct) ?? throw new InvalidOperationException("Google token response was empty.");

        string? email = null, sub = null;
        if (token.IdToken is not null)
        {
            var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token.IdToken);
            email = jwt.Claims.FirstOrDefault(c => c.Type == "email")?.Value;
            sub = jwt.Claims.FirstOrDefault(c => c.Type == "sub")?.Value;
        }

        var now = DateTimeOffset.UtcNow;
        return new OAuthTokenResult(
            AccessToken: token.AccessToken,
            RefreshToken: token.RefreshToken,
            AccessTokenExpiresAt: now.AddSeconds(token.ExpiresIn),
            RefreshTokenExpiresAt: null,   // Google doesn't return one; 7-day testing death detected at refresh
            ProviderAccountId: sub ?? throw new InvalidOperationException("Google id_token missing sub."),
            DisplayName: email,
            GrantedScopes: token.Scope
        );
    }

    private record GoogleTokenResponse
    {
        [JsonPropertyName("access_token")] public string AccessToken { get; init; } = default!;
        [JsonPropertyName("refresh_token")] public string? RefreshToken { get; init; }
        [JsonPropertyName("expires_in")] public int ExpiresIn { get; init; }
        [JsonPropertyName("scope")] public string? Scope { get; init; }
        [JsonPropertyName("id_token")] public string? IdToken { get; init; }
    }
}