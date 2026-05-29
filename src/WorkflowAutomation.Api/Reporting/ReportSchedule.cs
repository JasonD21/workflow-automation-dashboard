namespace WorkflowAutomation.Api.Reporting;

public class ReportSchedule
{
    public Guid Id { get; set; } = SequentialGuid.New();
    public Guid UserId { get; set; }
    public string Name { get; set; } = default!;
    public bool IsEnabled { get; set; } = true;
    public DayOfWeek DayOfWeek { get; set; }
    public TimeOnly TimeOfDay { get; set; }
    public string TimeZone { get; set; } = "UTC";          // IANA
    public string IncludedSources { get; set; } = default!; // jsonb
    public string RecipientEmail { get; set; } = default!;
    public string? HangfireJobId { get; set; }
    public DateTimeOffset? LastRunAt { get; set; }
    public DateTimeOffset? NextRunAt { get; set; }
}