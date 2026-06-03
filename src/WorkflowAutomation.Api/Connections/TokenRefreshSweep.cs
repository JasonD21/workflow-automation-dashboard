using Microsoft.EntityFrameworkCore;
using WorkflowAutomation.Api.Infrastructure.Persistence;

namespace WorkflowAutomation.Api.Connections;

public class TokenRefreshSweep(AppDbContext db, IConnectionRefresher refresher)
{
    public async Task RunAsync()
    {
        var ct = CancellationToken.None;
        var threshold = DateTimeOffset.UtcNow.AddMinutes(15);

        var due = await db.Connections
            .Where(c => c.Status == ConnectionStatus.Active
                && (c.Provider == Provider.QuickBooks || c.Provider == Provider.GoogleCalendar)
                && c.Token!.AccessTokenExpiresAt != null
                && c.Token.AccessTokenExpiresAt <= threshold)
            .Select(c => c.Id)
            .ToListAsync(ct);

        foreach (var id in due)
            await refresher.EnsureFreshAsync(id, ct);
    }
}