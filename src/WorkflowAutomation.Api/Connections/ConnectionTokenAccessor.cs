using Microsoft.EntityFrameworkCore;
using WorkflowAutomation.Api.Infrastructure.Persistence;

namespace WorkflowAutomation.Api.Connections;

public interface IConnectionTokenAccessor
{
    Task<string?> GetAccessTokenAsync(Guid connectionId, CancellationToken ct);
}

public class ConnectionTokenAccessor(AppDbContext db, ITokenProtector protector) : IConnectionTokenAccessor
{
    public async Task<string?> GetAccessTokenAsync(Guid connectionId, CancellationToken ct)
    {
        var token = await db.ProviderTokens.FirstOrDefaultAsync(t => t.ConnectionId == connectionId, ct);
        return token is null ? null : protector.Unprotect(token.AccessTokenEncrypted);
    }
}