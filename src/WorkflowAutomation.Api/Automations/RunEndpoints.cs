using System.Linq.Expressions;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using WorkflowAutomation.Api.Identity;
using WorkflowAutomation.Api.Infrastructure.Persistence;

namespace WorkflowAutomation.Api.Automations;

public record RunDto(Guid Id, Guid AutomationId, bool IsTest, DateTimeOffset TriggeredAt, RunStatus Status, string? IdempotencyKey,
    string? TriggerPayloadSummary, string? ActionResultSummary, string? ErrorMessage, int? DurationMs)
{
    public static RunDto From(AutomationRun r) => new(r.Id, r.AutomationId, r.IsTest, r.TriggeredAt, r.Status, r.IdempotencyKey,
        r.TriggerPayloadSummary, r.ActionResultSummary, r.ErrorMessage, r.DurationMs);
}

public static class RunEndpoints
{
    private static readonly Expression<Func<AutomationRun, RunDto>> ToDto = r => new RunDto(r.Id, r.AutomationId, r.IsTest, r.TriggeredAt,
        r.Status, r.IdempotencyKey, r.TriggerPayloadSummary, r.ActionResultSummary, r.ErrorMessage, r.DurationMs);

    public static IEndpointRouteBuilder MapRunEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/runs", List).RequireAuthorization();
        app.MapGet("/api/runs/{id:guid}", Get).RequireAuthorization();
        app.MapGet("/api/automations/{id:guid}/runs", ForAutomation).RequireAuthorization();
        return app;
    }

    private static async Task<IResult> List(ClaimsPrincipal u, AppDbContext db, CancellationToken ct,
        RunStatus? status = null, int page = 1, int pageSize = 20)
    {
        var userId = u.GetUserId();
        var q = db.AutomationRuns.Where(r => r.UserId == userId);
        if (status is not null) q = q.Where(r => r.Status == status);

        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(r => r.TriggeredAt)
            .Skip((page - 1) * pageSize).Take(pageSize).Select(ToDto).ToListAsync(ct);

        return Results.Ok(new { total, page, pageSize, items });
    }

    private static async Task<IResult> Get(Guid id, ClaimsPrincipal u, AppDbContext db, CancellationToken ct)
    {
        var dto = await db.AutomationRuns.Where(r => r.Id == id && r.UserId == u.GetUserId())
            .Select(ToDto).FirstOrDefaultAsync(ct);
        return dto is null ? Results.NotFound() : Results.Ok(dto);
    }

    private static async Task<IResult> ForAutomation(Guid id, ClaimsPrincipal u, AppDbContext db, CancellationToken ct)
    {
        var items = await db.AutomationRuns
            .Where(r => r.AutomationId == id && r.UserId == u.GetUserId())
            .OrderByDescending(r => r.TriggeredAt).Select(ToDto).ToListAsync(ct);
        return Results.Ok(items);
    }
}