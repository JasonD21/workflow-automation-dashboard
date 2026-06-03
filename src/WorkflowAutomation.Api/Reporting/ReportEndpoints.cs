using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using WorkflowAutomation.Api.Identity;
using WorkflowAutomation.Api.Infrastructure.Persistence;

namespace WorkflowAutomation.Api.Reporting;

public record GeneratedReportDto(
    Guid Id,
    Guid ReportScheduleId,
    DateTimeOffset GeneratedAt,
    DateTimeOffset PeriodStart,
    DateTimeOffset PeriodEnd,
    EmailStatus EmailStatus,
    DateTimeOffset? EmailedAt
);

public record GeneratedReportDetailDto(
    Guid Id, Guid ReportScheduleId,
    DateTimeOffset GeneratedAt,
    DateTimeOffset PeriodStart,
    DateTimeOffset PeriodEnd,
    EmailStatus EmailStatus,
    DateTimeOffset? EmailedAt,
    string DataSnapshot,
    string RenderedHtml
);

public static class ReportEndpoints
{
    public static IEndpointRouteBuilder MapReportEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/report-schedules").RequireAuthorization();
        g.MapGet("", async (ClaimsPrincipal u, IReportScheduleService svc, CancellationToken ct)
            => Results.Ok(await svc.ListAsync(u.GetUserId(), ct)));

        g.MapPost("", async (SaveReportScheduleRequest req, ClaimsPrincipal u, IReportScheduleService svc, CancellationToken ct)
            => Results.Ok(await svc.CreateAsync(u.GetUserId(), req, ct)));

        g.MapPut("/{id:guid}", async (Guid id, SaveReportScheduleRequest req, ClaimsPrincipal u, IReportScheduleService svc, CancellationToken ct)
            => await svc.UpdateAsync(u.GetUserId(), id, req, ct) is { } dto ? Results.Ok(dto) : Results.NotFound());

        g.MapDelete("/{id:guid}", async (Guid id, ClaimsPrincipal u, IReportScheduleService svc, CancellationToken ct)
            => await svc.DeleteAsync(u.GetUserId(), id, ct) ? Results.NoContent() : Results.NotFound());

        g.MapPost("/{id:guid}/generate", async (Guid id, ClaimsPrincipal u, IReportGenerator generator, AppDbContext db, CancellationToken ct) =>
            {
                if (!await db.ReportSchedules.AnyAsync(s => s.Id == id && s.UserId == u.GetUserId(), ct))
                    return Results.NotFound();

                var reportId = await generator.GenerateAndDeliverAsync(id, ct);
                if (reportId is null) return Results.NotFound();

                var dto = await db.GeneratedReports.Where(r => r.Id == reportId)
                    .Select(r => new GeneratedReportDetailDto(r.Id, r.ReportScheduleId, r.GeneratedAt,
                        r.PeriodStart, r.PeriodEnd, r.EmailStatus, r.EmailedAt, r.DataSnapshot, r.RenderedHtml))
                    .FirstAsync(ct);
                return Results.Ok(dto);
            });

        var reports = app.MapGroup("/api/reports").RequireAuthorization();
        reports.MapGet("", async (ClaimsPrincipal u, AppDbContext db, CancellationToken ct, int page = 1, int pageSize = 20) =>
        {
            var userId = u.GetUserId();
            var q = db.GeneratedReports.Where(r => r.UserId == userId);
            var total = await q.CountAsync(ct);
            var items = await q.OrderByDescending(r => r.GeneratedAt).Skip((page - 1) * pageSize).Take(pageSize)
                .Select(r => new GeneratedReportDto(r.Id, r.ReportScheduleId, r.GeneratedAt,
                    r.PeriodStart, r.PeriodEnd, r.EmailStatus, r.EmailedAt))
                .ToListAsync(ct);
            return Results.Ok(new { total, page, pageSize, items });
        });
        reports.MapGet("/{id:guid}", async (Guid id, ClaimsPrincipal u, AppDbContext db, CancellationToken ct) =>
        {
            var dto = await db.GeneratedReports.Where(r => r.Id == id && r.UserId == u.GetUserId())
                .Select(r => new GeneratedReportDetailDto(r.Id, r.ReportScheduleId, r.GeneratedAt,
                    r.PeriodStart, r.PeriodEnd, r.EmailStatus, r.EmailedAt, r.DataSnapshot, r.RenderedHtml))
                .FirstOrDefaultAsync(ct);
            return dto is null ? Results.NotFound() : Results.Ok(dto);
        });

        return app;
    }
}