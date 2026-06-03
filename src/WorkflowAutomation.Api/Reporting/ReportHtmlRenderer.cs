using System.Net;
using System.Text;

namespace WorkflowAutomation.Api.Reporting;

public interface IReportHtmlRenderer
{
    string Render(string title, ReportData data);
}

public class ReportHtmlRenderer : IReportHtmlRenderer
{
    public string Render(string title, ReportData data)
    {
        var period = $"{data.PeriodStart:MMM d} – {data.PeriodEnd:MMM d, yyyy}";
        var sb = new StringBuilder();

        sb.Append(@"<div style=""font-family:-apple-system,Segoe UI,Roboto,sans-serif;max-width:560px;margin:0 auto;color:#1a1a1a;"">");
        sb.Append($@"<div style=""background:#4f46e5;color:#fff;padding:24px;border-radius:12px 12px 0 0;"">
            <h1 style=""margin:0;font-size:20px;"">{Enc(title)}</h1>
            <p style=""margin:4px 0 0;opacity:.85;font-size:13px;"">{period}</p></div>");
        sb.Append(@"<div style=""border:1px solid #eee;border-top:none;border-radius:0 0 12px 12px;padding:8px 24px 24px;"">");

        if (data.Invoices is { } inv)
        {
            sb.Append(Section("Invoices"));
            sb.Append(Stat($"{inv.CreatedCount}", "new invoices", Money(inv.CreatedTotal, inv.Currency)));
            sb.Append(Stat($"{inv.UnpaidCount}", "still unpaid", Money(inv.UnpaidTotal, inv.Currency)));
        }
        if (data.Calendar is { } cal)
        {
            sb.Append(Section("Calendar"));
            sb.Append(Stat($"{cal.EventCount}", "meetings", ""));
            if (cal.Titles.Count > 0)
                sb.Append($@"<ul style=""margin:4px 0 0;padding-left:18px;color:#555;font-size:13px;"">{string.Concat(cal.Titles.Select(t => $"<li>{Enc(t)}</li>"))}</ul>");
        }
        if (data.Slack is { MessageCount: > 0 } s)
        {
            sb.Append(Section("Slack"));
            sb.Append(Stat($"{s.MessageCount}", "messages", ""));
        }
        if (data.Invoices is null && data.Calendar is null && (data.Slack is null || data.Slack.MessageCount == 0))
            sb.Append(@"<p style=""color:#777;"">No activity to report this period.</p>");

        sb.Append("</div></div>");
        return sb.ToString();
    }

    private static string Section(string t) =>
        $@"<h2 style=""font-size:13px;text-transform:uppercase;letter-spacing:.05em;color:#888;margin:20px 0 4px;"">{t}</h2>";

    private static string Stat(string number, string label, string value) =>
        $@"<div style=""display:flex;justify-content:space-between;align-items:baseline;padding:8px 0;border-bottom:1px solid #f3f3f3;"">
            <span><b style=""font-size:22px;"">{number}</b> <span style=""color:#666;"">{label}</span></span>
            <span style=""color:#111;font-weight:600;"">{value}</span></div>";

    private static string Money(decimal amount, string currency) => $"{currency} {amount:N2}";
    private static string Enc(string s) => WebUtility.HtmlEncode(s);
}