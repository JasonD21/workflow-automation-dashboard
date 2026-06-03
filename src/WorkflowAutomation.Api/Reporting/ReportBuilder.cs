using Microsoft.EntityFrameworkCore;
using WorkflowAutomation.Api.Connections;
using WorkflowAutomation.Api.Connections.Providers;
using WorkflowAutomation.Api.Infrastructure.Persistence;

namespace WorkflowAutomation.Api.Reporting;

public interface IReportBuilder
{
    Task<ReportData> BuildAsync(Guid userId, IReadOnlyCollection<string> sources, DateTimeOffset periodStart, DateTimeOffset periodEnd, CancellationToken ct);
}

public class ReportBuilder(AppDbContext db, IQboClient qbo, IGoogleCalendarClient calendar, ISlackClient slack) : IReportBuilder
{
    public async Task<ReportData> BuildAsync(Guid userId, IReadOnlyCollection<string> sources, DateTimeOffset periodStart, DateTimeOffset periodEnd, CancellationToken ct)
    {
        var connections = await db.Connections
            .Where(c => c.UserId == userId && c.Status == ConnectionStatus.Active)
            .ToListAsync(ct);

        Connection? Conn(Provider p) => connections.FirstOrDefault(c => c.Provider == p);

        InvoiceSummary? invoices = null;
        if (sources.Contains("QuickBooks") && Conn(Provider.QuickBooks) is { } qboConn)
            invoices = await BuildInvoicesAsync(qboConn, periodStart, periodEnd, ct);

        CalendarSummary? cal = null;
        if (sources.Contains("GoogleCalendar") && Conn(Provider.GoogleCalendar) is { } gcalConn)
            cal = await BuildCalendarAsync(gcalConn, periodStart, periodEnd, ct);

        SlackSummary? slk = null;
        if (sources.Contains("Slack") && Conn(Provider.Slack) is { } slackConn)
        {
            var activity = await slack.GetActivityAsync(slackConn, periodStart, ct);
            slk = new SlackSummary(activity.Sum(a => a.MessageCount),
                [.. activity.Select(a => new ChannelActivity(a.ChannelName, a.MessageCount))]);
        }

        return new ReportData(periodStart, periodEnd, invoices, cal, slk);
    }

    private async Task<InvoiceSummary> BuildInvoicesAsync(Connection conn, DateTimeOffset start, DateTimeOffset end, CancellationToken ct)
    {
        var invoices = await qbo.QueryInvoicesAsync(conn, ct);
        var startDate = DateOnly.FromDateTime(start.UtcDateTime);
        var endDate = DateOnly.FromDateTime(end.UtcDateTime);

        var created = invoices.Where(i => i.TxnDate is { } d && d >= startDate && d <= endDate).ToList();
        var unpaid = invoices.Where(i => i.Balance > 0).ToList();
        var currency = invoices.FirstOrDefault()?.Currency ?? "USD";

        return new InvoiceSummary(created.Count, created.Sum(i => i.Total), unpaid.Count, unpaid.Sum(i => i.Balance), currency);
    }

    private async Task<CalendarSummary> BuildCalendarAsync(Connection conn, DateTimeOffset start, DateTimeOffset end, CancellationToken ct)
    {
        var events = (await calendar.ListEventsInRangeAsync(conn, start, end, ct))
            .Where(e => e.Status == "confirmed").OrderBy(e => e.Start).ToList();
        var titles = events.Where(e => e.Title is not null).Take(5).Select(e => e.Title!).ToList();

        return new CalendarSummary(events.Count, titles);
    }
}