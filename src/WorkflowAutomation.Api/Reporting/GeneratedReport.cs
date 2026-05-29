namespace WorkflowAutomation.Api.Reporting;

public class GeneratedReport
{
    public Guid Id { get; set; } = SequentialGuid.New();
    public Guid ReportScheduleId { get; set; }
    public Guid UserId { get; set; }
    public DateTimeOffset GeneratedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset PeriodStart { get; set; }
    public DateTimeOffset PeriodEnd { get; set; }
    public string DataSnapshot { get; set; } = default!;    // jsonb
    public string RenderedHtml { get; set; } = default!;
    public EmailStatus EmailStatus { get; set; } = EmailStatus.Pending;
    public DateTimeOffset? EmailedAt { get; set; }
}