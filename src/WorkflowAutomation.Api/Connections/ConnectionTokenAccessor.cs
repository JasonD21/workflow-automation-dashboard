using Microsoft.EntityFrameworkCore;
using WorkflowAutomation.Api.Infrastructure.Persistence;

namespace WorkflowAutomation.Api.Connections;

public interface IConnectionTokenAccessor
{
    Task<string?> GetAccessTokenAsync(Guid connectionId, CancellationToken ct);
}

public class ConnectionTokenAccessor(AppDbContext db, ITokenProtector protector, IConnectionRefresher refresher) : IConnectionTokenAccessor
{
    public async Task<string?> GetAccessTokenAsync(Guid connectionId, CancellationToken ct)
    {
        if (!await refresher.EnsureFreshAsync(connectionId, ct)) return null;   // refresh-if-needed first
        var token = await db.ProviderTokens.FirstOrDefaultAsync(t => t.ConnectionId == connectionId, ct);
        return token is null ? null : protector.Unprotect(token.AccessTokenEncrypted);
    }
}