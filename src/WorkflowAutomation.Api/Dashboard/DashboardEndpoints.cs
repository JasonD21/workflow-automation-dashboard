using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using WorkflowAutomation.Api.Automations;
using WorkflowAutomation.Api.Identity;
using WorkflowAutomation.Api.Infrastructure.Persistence;

namespace WorkflowAutomation.Api.Dashboard;

public record ConnectionBriefDto(
    Guid Id,
    string Provider,
    string DisplayName,
    string Status
);
public record RunBriefDto(
    Guid Id,
    Guid AutomationId,
    string AutomationName,
    string Status,
    DateTimeOffset TriggeredAt,
    bool IsTest
);
public record ReportBriefDto(
    Guid Id,
    string Name,
    string DayOfWeek,
    string TimeOfDay
);
public record DashboardSummaryDto(
    IReadOnlyList<ConnectionBriefDto> Connections,
    int EnabledAutomations,
    int RunsLast7Days,
    int FailedRunsLast7Days,
    IReadOnlyList<RunBriefDto> RecentRuns,
    ReportBriefDto? NextReport
);

public static class DashboardEndpoints
{
    public static IEndpointRouteBuilder MapDashboardEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/dashboard/summary", Summary).RequireAuthorization();
        return app;
    }

    private static async Task<IResult> Summary(ClaimsPrincipal u, AppDbContext db, CancellationToken ct)
    {
        var userId = u.GetUserId();
        var weekAgo = DateTimeOffset.UtcNow.AddDays(-7);

        var connections = await db.Connections.Where(c => c.UserId == userId)
            .Select(c => new ConnectionBriefDto(c.Id, c.Provider.ToString(), c.DisplayName, c.Status.ToString()))
            .ToListAsync(ct);

        var enabled = await db.Automations.CountAsync(a => a.UserId == userId && a.IsEnabled, ct);
        var runsWeek = await db.AutomationRuns.CountAsync(r => r.UserId == userId && r.TriggeredAt >= weekAgo, ct);
        var failedWeek = await db.AutomationRuns.CountAsync(r => r.UserId == userId && r.TriggeredAt >= weekAgo && r.Status == RunStatus.Failed, ct);

        var runs = await db.AutomationRuns.Where(r => r.UserId == userId)
            .OrderByDescending(r => r.TriggeredAt)
            .Take(5)
            .ToListAsync(ct);

        var ids = runs.Select(r => r.AutomationId)
            .Distinct()
            .ToList();

        var names = await db.Automations.IgnoreQueryFilters()
            .Where(a => ids.Contains(a.Id)).ToDictionaryAsync(a => a.Id, a => a.Name, ct);

        var recent = runs.Select(r => new RunBriefDto(r.Id, r.AutomationId, names.GetValueOrDefault(r.AutomationId, "(deleted)"), r.Status.ToString(), r.TriggeredAt, r.IsTest)).ToList();

        var next = await db.ReportSchedules.Where(s => s.UserId == userId && s.IsEnabled)
            .OrderBy(s => s.Name)
            .Select(s => new ReportBriefDto(s.Id, s.Name, s.DayOfWeek.ToString(), s.TimeOfDay.ToString("HH:mm")))
            .FirstOrDefaultAsync(ct);

        return Results.Ok(new DashboardSummaryDto(connections, enabled, runsWeek, failedWeek, recent, next));
    }
}