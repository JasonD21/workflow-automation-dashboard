using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using WorkflowAutomation.Api.Automations;
using WorkflowAutomation.Api.Connections;
using WorkflowAutomation.Api.Identity;
using WorkflowAutomation.Api.Infrastructure.Persistence;
using WorkflowAutomation.Api.Reporting;

namespace WorkflowAutomation.Api.Infrastructure;

public class DemoSeeder(AppDbContext db, UserManager<ApplicationUser> users, ITokenProtector protector, IReportHtmlRenderer renderer, IConfiguration config)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task SeedAsync()
    {
        var email = config["Demo:Email"];
        if (string.IsNullOrEmpty(email) || await users.FindByEmailAsync(email) is not null)
            return;   // idempotent — seed once

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = "Demo User",
            TimeZone = "Africa/Johannesburg",
            IsDemo = true
        };
        // random password — demo is only reachable via /api/auth/demo-login, never a password login
        await users.CreateAsync(user, $"Demo!{Guid.NewGuid():N}aA1");

        // Connections — wired to real controlled tokens from config where available so the
        // demo's Test runs genuinely fire (Slack is the always-on backbone). Falls back to a
        // placeholder so the connection still *displays* as Active even without a token.
        var slack = Conn(user.Id, Provider.Slack, "Acme Workspace", config["Demo:SlackBotToken"], realmId: config["Demo:SlackTeamId"] ?? "T-DEMO");
        var qbo = Conn(user.Id, Provider.QuickBooks, "Acme Books (sandbox)", config["Demo:QboAccessToken"], config["Demo:QboRefreshToken"],
            realmId: config["Demo:QboRealmId"] ?? "R-DEMO", accessExpiry: DateTimeOffset.UtcNow.AddMinutes(50),
            refreshExpiry: DateTimeOffset.UtcNow.AddDays(100));
        var gcal = Conn(user.Id, Provider.GoogleCalendar, "demo@acme.com", config["Demo:GoogleAccessToken"], config["Demo:GoogleRefreshToken"],
            realmId: "G-DEMO", accessExpiry: DateTimeOffset.UtcNow.AddMinutes(50));

        db.Connections.AddRange(slack, qbo, gcal);

        // Automations
        var a1 = Automation(user.Id, "Invoice paid → notify #finance", TriggerTypes.InvoicePaid, Provider.QuickBooks, qbo.Id, ActionTypes.SlackPostMessage,
            Provider.Slack, slack.Id,
                new()
                {
                    ["channelId"] = config["Demo:SlackChannelId"] ?? "C-DEMO",
                    ["messageTemplate"] = "💰 {{invoice.customer}} paid {{invoice.total}}"
                }
        );
        var a2 = Automation(user.Id, "Invoice created → email me a copy", TriggerTypes.InvoiceCreated, Provider.QuickBooks, qbo.Id, ActionTypes.EmailSend,
            Provider.Email, null,
                new()
                {
                    ["subjectTemplate"] = "New invoice {{invoice.number}}",
                    ["bodyTemplate"] = "<p>{{invoice.customer}} — {{invoice.total}}</p>"
                }
        );
        var a3 = Automation(user.Id, "New meeting → Slack heads-up", TriggerTypes.CalendarEventCreated, Provider.GoogleCalendar, gcal.Id, ActionTypes.SlackPostMessage,
            Provider.Slack, slack.Id,
                new()
                {
                    ["channelId"] = config["Demo:SlackChannelId"] ?? "C-DEMO",
                    ["messageTemplate"] = "📅 Upcoming: {{event.title}}"
                }
        );
        db.Automations.AddRange(a1, a2, a3);

        // A week of run history so the dashboard and activity log look alive
        db.AutomationRuns.AddRange(History(a1, a2, a3));

        // A report schedule + one already-generated report
        var schedule = new ReportSchedule
        {
            UserId = user.Id,
            Name = "Weekly digest",
            IsEnabled = true,
            DayOfWeek = DayOfWeek.Monday,
            TimeOfDay = new TimeOnly(9, 0),
            TimeZone = "Africa/Johannesburg",
            RecipientEmail = email,
            IncludedSources = JsonSerializer.Serialize(new[] { "QuickBooks", "GoogleCalendar", "Slack" }, Json)
        };
        schedule.HangfireJobId = $"report-{schedule.Id}";
        db.ReportSchedules.Add(schedule);

        var data = new ReportData(DateTimeOffset.UtcNow.AddDays(-7), DateTimeOffset.UtcNow,
            new InvoiceSummary(4, 6200m, 2, 2400m, "USD"),
            new CalendarSummary(5, ["Client kickoff", "Design review", "Standup"]),
            new SlackSummary(37, [new ChannelActivity("finance", 12), new ChannelActivity("leads", 25)]));

        db.GeneratedReports.Add(new GeneratedReport
        {
            ReportScheduleId = schedule.Id,
            UserId = user.Id,
            GeneratedAt = DateTimeOffset.UtcNow.AddDays(-2),
            PeriodStart = data.PeriodStart,
            PeriodEnd = data.PeriodEnd,
            DataSnapshot = JsonSerializer.Serialize(data, Json),
            RenderedHtml = renderer.Render(schedule.Name, data),
            EmailStatus = EmailStatus.Sent,
            EmailedAt = DateTimeOffset.UtcNow.AddDays(-2)
        });

        await db.SaveChangesAsync();
    }

    private Connection Conn(Guid userId, Provider provider, string display, string? accessToken, string? refreshToken = null, string realmId = "",
        DateTimeOffset? accessExpiry = null, DateTimeOffset? refreshExpiry = null) => new()
        {
            UserId = userId,
            Provider = provider,
            ProviderAccountId = realmId,
            DisplayName = display,
            Status = ConnectionStatus.Active,
            LastRefreshedAt = DateTimeOffset.UtcNow,
            Token = new ProviderToken
            {
                AccessTokenEncrypted = protector.Protect(accessToken ?? "demo-placeholder-token"),
                RefreshTokenEncrypted = refreshToken is null ? null : protector.Protect(refreshToken),
                AccessTokenExpiresAt = accessExpiry,
                RefreshTokenExpiresAt = refreshExpiry
            }
        };

    private static Automation Automation(Guid userId, string name, string triggerType, Provider triggerProvider, Guid triggerConn, string actionType,
        Provider actionProvider, Guid? actionConn, Dictionary<string, string> actionConfig) => new()
        {
            UserId = userId,
            Name = name,
            IsEnabled = true,
            TriggerProvider = triggerProvider,
            TriggerType = triggerType,
            TriggerConnectionId = triggerConn,
            ActionProvider = actionProvider,
            ActionType = actionType,
            ActionConnectionId = actionConn,
            ActionConfig = JsonSerializer.Serialize(actionConfig, Json),
            LastTriggeredAt = DateTimeOffset.UtcNow.AddHours(-6)
        };

    private static IEnumerable<AutomationRun> History(Automation a1, Automation a2, Automation a3)
    {
        var rng = new Random(42);
        var samples = new (Automation A, string Payload, string Result)[]
        {
            (a1, "{\"invoice.customer\":\"Acme Inc.\",\"invoice.total\":\"1500.00\"}", "{\"summary\":\"Posted to #finance\"}"),
            (a2, "{\"invoice.number\":\"1042\",\"invoice.customer\":\"Beta LLC\"}", "{\"summary\":\"Email sent\"}"),
            (a3, "{\"event.title\":\"Client kickoff\"}", "{\"summary\":\"Posted to #finance\"}"),
        };
        for (var i = 0; i < 18; i++)
        {
            var s = samples[i % samples.Length];
            var failed = i % 7 == 5;
            yield return new AutomationRun
            {
                AutomationId = s.A.Id,
                UserId = s.A.UserId,
                TriggeredAt = DateTimeOffset.UtcNow.AddHours(-rng.Next(2, 168)),
                Status = failed ? RunStatus.Failed : RunStatus.Success,
                IdempotencyKey = $"seed-{i}",
                TriggerPayloadSummary = s.Payload,
                ActionResultSummary = failed ? null : s.Result,
                ErrorMessage = failed ? "Slack error: channel_not_found" : null,
                DurationMs = rng.Next(120, 900)
            };
        }
    }
}