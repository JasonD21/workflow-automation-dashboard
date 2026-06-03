using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WorkflowAutomation.Api.Infrastructure.Persistence;

namespace WorkflowAutomation.Api.Reporting;

public record SaveReportScheduleRequest(
    string Name,
    bool IsEnabled,
    DayOfWeek DayOfWeek,
    TimeOnly TimeOfDay,
    string TimeZone,
    List<string> IncludedSources,
    string? RecipientEmail
);

public record ReportScheduleDto(
    Guid Id,
    string Name,
    bool IsEnabled,
    DayOfWeek DayOfWeek,
    TimeOnly TimeOfDay,
    string TimeZone,
    List<string> IncludedSources,
    string RecipientEmail,
    DateTimeOffset? LastRunAt
);

public interface IReportScheduleService
{
    Task<ReportScheduleDto> CreateAsync(Guid userId, SaveReportScheduleRequest req, CancellationToken ct);
    Task<ReportScheduleDto?> UpdateAsync(Guid userId, Guid id, SaveReportScheduleRequest req, CancellationToken ct);
    Task<IReadOnlyList<ReportScheduleDto>> ListAsync(Guid userId, CancellationToken ct);
    Task<bool> DeleteAsync(Guid userId, Guid id, CancellationToken ct);
}

public class ReportScheduleService(AppDbContext db, IReportScheduler scheduler) : IReportScheduleService
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<ReportScheduleDto> CreateAsync(Guid userId, SaveReportScheduleRequest req, CancellationToken ct)
    {
        var email = await ResolveEmailAsync(userId, req.RecipientEmail, ct);
        var s = new ReportSchedule { UserId = userId };
        Apply(s, req, email);
        s.HangfireJobId = ReportScheduler.JobId(s.Id);

        db.ReportSchedules.Add(s);
        await db.SaveChangesAsync(ct);
        scheduler.Sync(s);
        return Map(s);
    }

    public async Task<ReportScheduleDto?> UpdateAsync(Guid userId, Guid id, SaveReportScheduleRequest req, CancellationToken ct)
    {
        var s = await db.ReportSchedules.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, ct);
        if (s is null) return null;

        Apply(s, req, await ResolveEmailAsync(userId, req.RecipientEmail, ct));
        await db.SaveChangesAsync(ct);
        scheduler.Sync(s);
        return Map(s);
    }

    public async Task<IReadOnlyList<ReportScheduleDto>> ListAsync(Guid userId, CancellationToken ct) =>
        [.. (await db.ReportSchedules.Where(s => s.UserId == userId).OrderBy(s => s.Name).ToListAsync(ct)).Select(Map)];

    public async Task<bool> DeleteAsync(Guid userId, Guid id, CancellationToken ct)
    {
        var s = await db.ReportSchedules.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, ct);
        if (s is null) return false;
        scheduler.Remove(s.Id);
        db.ReportSchedules.Remove(s);     // GeneratedReports cascade
        await db.SaveChangesAsync(ct);
        return true;
    }

    private async Task<string> ResolveEmailAsync(Guid userId, string? requested, CancellationToken ct) =>
        string.IsNullOrWhiteSpace(requested)
            ? await db.Users.Where(u => u.Id == userId).Select(u => u.Email).FirstOrDefaultAsync(ct) ?? ""
            : requested;

    private static void Apply(ReportSchedule s, SaveReportScheduleRequest req, string email)
    {
        s.Name = req.Name.Trim();
        s.IsEnabled = req.IsEnabled;
        s.DayOfWeek = req.DayOfWeek;
        s.TimeOfDay = req.TimeOfDay;
        s.TimeZone = req.TimeZone;
        s.IncludedSources = JsonSerializer.Serialize(req.IncludedSources, Json);
        s.RecipientEmail = email;
    }

    private static ReportScheduleDto Map(ReportSchedule s) => new(
        s.Id, s.Name, s.IsEnabled, s.DayOfWeek, s.TimeOfDay, s.TimeZone,
        JsonSerializer.Deserialize<List<string>>(s.IncludedSources, Json) ?? [],
        s.RecipientEmail, s.LastRunAt);
}