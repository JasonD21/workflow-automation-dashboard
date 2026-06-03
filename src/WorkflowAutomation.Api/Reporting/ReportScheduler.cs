using Hangfire;

namespace WorkflowAutomation.Api.Reporting;

public interface IReportScheduler
{
    void Sync(ReportSchedule schedule);
    void Remove(Guid scheduleId);
}

public class ReportScheduler(IRecurringJobManager jobs) : IReportScheduler
{
    public static string JobId(Guid scheduleId) => $"report-{scheduleId}";

    public void Sync(ReportSchedule schedule)
    {
        var id = JobId(schedule.Id);
        if (!schedule.IsEnabled) { jobs.RemoveIfExists(id); return; }

        // cron: "minute hour * * dayOfWeek"  (System.DayOfWeek Sunday=0 matches cron 0-6)
        var cron = $"{schedule.TimeOfDay.Minute} {schedule.TimeOfDay.Hour} * * {(int)schedule.DayOfWeek}";

        jobs.AddOrUpdate<GenerateReportJob>(id, j => j.RunScheduledAsync(schedule.Id), cron,
            new RecurringJobOptions { TimeZone = ResolveTimeZone(schedule.TimeZone) });
    }

    public void Remove(Guid scheduleId) => jobs.RemoveIfExists(JobId(scheduleId));

    private static TimeZoneInfo ResolveTimeZone(string iana)
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById(iana); }
        catch { return TimeZoneInfo.Utc; }
    }
}