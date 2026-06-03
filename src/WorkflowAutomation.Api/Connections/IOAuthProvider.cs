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

public record RefreshResult(
    string AccessToken,
    string? RefreshToken,                 // QBO rotates and returns a new one; Google keeps the old one (null)
    DateTimeOffset? AccessTokenExpiresAt,
    DateTimeOffset? RefreshTokenExpiresAt
);

public interface IOAuthProvider
{
    Provider Provider { get; }
    bool SupportsRefresh { get; }
    string BuildAuthorizeUrl(string state, string redirectUri);
    Task<OAuthTokenResult> ExchangeCodeAsync(string code, string redirectUri,
        IReadOnlyDictionary<string, string?> callbackParams, CancellationToken ct = default);
    Task<RefreshResult> RefreshAsync(string refreshToken, CancellationToken ct = default);
}