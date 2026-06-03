using Microsoft.EntityFrameworkCore;
using WorkflowAutomation.Api.Automations;
using WorkflowAutomation.Api.Infrastructure.Persistence;

namespace WorkflowAutomation.Api.Connections;

public interface IConnectionRefresher
{
    Task<bool> EnsureFreshAsync(Guid connectionId, CancellationToken ct);   // false ⇒ NeedsReconnect
}

public class ConnectionRefresher(AppDbContext db, IOAuthProviderResolver resolver, ITokenProtector protector) : IConnectionRefresher
{
    private static readonly TimeSpan Buffer = TimeSpan.FromMinutes(2);

    public async Task<bool> EnsureFreshAsync(Guid connectionId, CancellationToken ct)
    {
        var conn = await db.Connections.Include(c => c.Token)
            .FirstOrDefaultAsync(c => c.Id == connectionId, ct);
        if (conn?.Token is null || conn.Status == ConnectionStatus.NeedsReconnect) return false;

        var provider = resolver.Get(conn.Provider);
        if (!provider.SupportsRefresh) return true;                              // Slack: nothing to do
        if (conn.Token.AccessTokenExpiresAt is { } exp && exp - Buffer > DateTimeOffset.UtcNow)
            return true;                                                         // still valid

        if (conn.Token.RefreshTokenEncrypted is null)
        {
            await MarkNeedsReconnectAsync(conn, ct);
            return false;
        }

        try
        {
            var result = await provider.RefreshAsync(protector.Unprotect(conn.Token.RefreshTokenEncrypted), ct);

            conn.Token.AccessTokenEncrypted = protector.Protect(result.AccessToken);
            if (result.RefreshToken is not null)                                 // QBO rotation — persist the new one!
                conn.Token.RefreshTokenEncrypted = protector.Protect(result.RefreshToken);
            conn.Token.AccessTokenExpiresAt = result.AccessTokenExpiresAt;
            if (result.RefreshTokenExpiresAt is not null)
                conn.Token.RefreshTokenExpiresAt = result.RefreshTokenExpiresAt;
            conn.Status = ConnectionStatus.Active;
            conn.LastRefreshedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
            return true;
        }
        catch
        {
            await MarkNeedsReconnectAsync(conn, ct);                             // includes Google's 7-day death
            return false;
        }
    }

    private async Task MarkNeedsReconnectAsync(Connection conn, CancellationToken ct)
    {
        conn.Status = ConnectionStatus.NeedsReconnect;
        var dependents = await db.Automations
            .Where(a => a.UserId == conn.UserId &&
                        (a.TriggerConnectionId == conn.Id || a.ActionConnectionId == conn.Id))
            .ToListAsync(ct);
        foreach (var a in dependents) { a.IsEnabled = false; a.UpdatedAt = DateTimeOffset.UtcNow; }
        await db.SaveChangesAsync(ct);
    }
}