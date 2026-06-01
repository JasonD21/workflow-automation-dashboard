using Microsoft.EntityFrameworkCore;
using WorkflowAutomation.Api.Automations;
using WorkflowAutomation.Api.Infrastructure.Persistence;

namespace WorkflowAutomation.Api.Connections;

public record ConnectionDto(
    Guid Id, Provider Provider, string? DisplayName, ConnectionStatus Status,
    string? GrantedScopes, DateTimeOffset CreatedAt, DateTimeOffset? LastRefreshedAt);

public interface IConnectionsService
{
    string BuildAuthorizeUrl(Guid userId, Provider provider, string redirectUri);
    Task<Provider?> HandleCallbackAsync(string code, string state, string redirectUri, CancellationToken ct);
    Task<IReadOnlyList<ConnectionDto>> ListAsync(Guid userId, CancellationToken ct);
    Task<ConnectionDto?> GetAsync(Guid userId, Guid id, CancellationToken ct);
    Task<bool> DisconnectAsync(Guid userId, Guid id, CancellationToken ct);
}

public class ConnectionsService(AppDbContext db, IOAuthProviderResolver resolver, IOAuthStateService stateService, ITokenProtector protector) : IConnectionsService
{
    public string BuildAuthorizeUrl(Guid userId, Provider provider, string redirectUri)
    {
        var state = stateService.Create(userId, provider);
        return resolver.Get(provider).BuildAuthorizeUrl(state, redirectUri);
    }

    public async Task<Provider?> HandleCallbackAsync(string code, string state, string redirectUri, CancellationToken ct)
    {
        var validated = stateService.Validate(state);
        if (validated is null) return null;

        var result = await resolver.Get(validated.Provider).ExchangeCodeAsync(code, redirectUri, ct);

        var conn = await db.Connections
            .Include(c => c.Token)
            .FirstOrDefaultAsync(c =>
                c.UserId == validated.UserId &&
                c.Provider == validated.Provider &&
                c.ProviderAccountId == result.ProviderAccountId, ct);

        if (conn is null)
        {
            conn = new Connection
            {
                UserId = validated.UserId,
                Provider = validated.Provider,
                ProviderAccountId = result.ProviderAccountId,
                Token = new ProviderToken()
            };
            db.Connections.Add(conn);
        }
        else
        {
            conn.Token ??= new ProviderToken { ConnectionId = conn.Id };
            // reconnect: re-enable automations that were disabled when this connection broke
            await SetDependentAutomationsEnabledAsync(validated.UserId, conn.Id, true, ct);
        }

        conn.DisplayName = result.DisplayName;
        conn.GrantedScopes = result.GrantedScopes;
        conn.Status = ConnectionStatus.Active;
        conn.LastRefreshedAt = DateTimeOffset.UtcNow;

        conn.Token!.AccessTokenEncrypted = protector.Protect(result.AccessToken);
        conn.Token.RefreshTokenEncrypted = result.RefreshToken is null ? null : protector.Protect(result.RefreshToken);
        conn.Token.AccessTokenExpiresAt = result.AccessTokenExpiresAt;
        conn.Token.RefreshTokenExpiresAt = result.RefreshTokenExpiresAt;

        await db.SaveChangesAsync(ct);
        return validated.Provider;
    }

    public async Task<IReadOnlyList<ConnectionDto>> ListAsync(Guid userId, CancellationToken ct) =>
        await db.Connections
            .Where(c => c.UserId == userId)
            .OrderBy(c => c.CreatedAt)
            .Select(c => new ConnectionDto(c.Id, c.Provider, c.DisplayName, c.Status, c.GrantedScopes, c.CreatedAt, c.LastRefreshedAt))
            .ToListAsync(ct);

    public async Task<ConnectionDto?> GetAsync(Guid userId, Guid id, CancellationToken ct) =>
        await db.Connections
            .Where(c => c.UserId == userId && c.Id == id)
            .Select(c => new ConnectionDto(c.Id, c.Provider, c.DisplayName, c.Status, c.GrantedScopes, c.CreatedAt, c.LastRefreshedAt))
            .FirstOrDefaultAsync(ct);

    public async Task<bool> DisconnectAsync(Guid userId, Guid id, CancellationToken ct)
    {
        var conn = await db.Connections.FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId, ct);
        if (conn is null) return false;

        await SetDependentAutomationsEnabledAsync(userId, id, false, ct);
        db.Connections.Remove(conn);   // ProviderToken cascades
        await db.SaveChangesAsync(ct);
        return true;
    }

    private async Task SetDependentAutomationsEnabledAsync(
        Guid userId, Guid connectionId, bool enabled, CancellationToken ct)
    {
        var dependents = await db.Automations
            .Where(a => a.UserId == userId && (a.TriggerConnectionId == connectionId || a.ActionConnectionId == connectionId))
            .ToListAsync(ct);
        foreach (var a in dependents)
        {
            a.IsEnabled = enabled;
            a.UpdatedAt = DateTimeOffset.UtcNow;
        }
    }
}