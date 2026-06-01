namespace WorkflowAutomation.Api.Connections;

public record OAuthTokenResult(
    string AccessToken,
    string? RefreshToken,
    DateTimeOffset? AccessTokenExpiresAt,
    DateTimeOffset? RefreshTokenExpiresAt,
    string ProviderAccountId,
    string? DisplayName,
    string? GrantedScopes
);

public interface IOAuthProvider
{
    Provider Provider { get; }
    string BuildAuthorizeUrl(string state, string redirectUri);
    Task<OAuthTokenResult> ExchangeCodeAsync(string code, string redirectUri, IReadOnlyDictionary<string, string?> callbackParams, CancellationToken ct = default);
}