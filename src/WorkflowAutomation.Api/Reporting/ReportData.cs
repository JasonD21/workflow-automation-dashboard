namespace WorkflowAutomation.Api.Reporting;

public record ReportData(
    DateTimeOffset PeriodStart,
    DateTimeOffset PeriodEnd,
    InvoiceSummary? Invoices,
    CalendarSummary? Calendar,
    SlackSummary? Slack
);

public record InvoiceSummary(int CreatedCount, decimal CreatedTotal, int UnpaidCount, decimal UnpaidTotal, string Currency);
public record CalendarSummary(int EventCount, IReadOnlyList<string> Titles);
public record SlackSummary(int MessageCount, IReadOnlyList<ChannelActivity> Channels);
public record ChannelActivity(string Channel, int Count);