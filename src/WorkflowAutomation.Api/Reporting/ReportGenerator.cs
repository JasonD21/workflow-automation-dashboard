using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WorkflowAutomation.Api.Infrastructure.Email;
using WorkflowAutomation.Api.Infrastructure.Persistence;

namespace WorkflowAutomation.Api.Reporting;

public interface IReportGenerator
{
    Task<Guid?> GenerateAndDeliverAsync(Guid scheduleId, CancellationToken ct);
}

public class ReportGenerator(AppDbContext db, IReportBuilder builder, IReportHtmlRenderer renderer, IEmailSender email) : IReportGenerator
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<Guid?> GenerateAndDeliverAsync(Guid scheduleId, CancellationToken ct)
    {
        var schedule = await db.ReportSchedules.FirstOrDefaultAsync(s => s.Id == scheduleId, ct);
        if (schedule is null) return null;

        var end = DateTimeOffset.UtcNow;
        var start = end.AddDays(-7);
        var sources = JsonSerializer.Deserialize<List<string>>(schedule.IncludedSources, Json) ?? [];

        var data = await builder.BuildAsync(schedule.UserId, sources, start, end, ct);
        var html = renderer.Render(schedule.Name, data);

        var report = new GeneratedReport
        {
            ReportScheduleId = schedule.Id,
            UserId = schedule.UserId,
            GeneratedAt = DateTimeOffset.UtcNow,
            PeriodStart = start,
            PeriodEnd = end,
            DataSnapshot = JsonSerializer.Serialize(data, Json),
            RenderedHtml = html,
            EmailStatus = EmailStatus.Pending
        };

        var result = await email.SendAsync(schedule.RecipientEmail, $"{schedule.Name} — weekly report", html, ct);
        report.EmailStatus = result.Success ? EmailStatus.Sent : EmailStatus.Failed;
        if (result.Success) report.EmailedAt = DateTimeOffset.UtcNow;

        db.GeneratedReports.Add(report);
        schedule.LastRunAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return report.Id;
    }
}