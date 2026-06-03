using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WorkflowAutomation.Api.Connections;
using WorkflowAutomation.Api.Connections.Providers;
using WorkflowAutomation.Api.Infrastructure.Persistence;

namespace WorkflowAutomation.Api.Automations.Triggers;

public class CalendarPollJob(AppDbContext db, IGoogleCalendarClient calendar, ITriggerProcessor processor)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task RunAsync()
    {
        var ct = CancellationToken.None;
        var connections = await db.Connections
            .Where(c => c.Provider == Provider.GoogleCalendar && c.Status == ConnectionStatus.Active)
            .ToListAsync(ct);

        foreach (var conn in connections)
            await PollOneAsync(conn, ct);
    }

    private async Task PollOneAsync(Connection conn, CancellationToken ct)
    {
        var meta = conn.Metadata is null
            ? []
            : JsonSerializer.Deserialize<Dictionary<string, string>>(conn.Metadata, Json) ?? [];

        if (!meta.ContainsKey("baselineAt"))                       // first ever poll → record the line in the sand
            meta["baselineAt"] = DateTimeOffset.UtcNow.ToString("o");
        var baselineAt = DateTimeOffset.Parse(meta["baselineAt"]);
        var syncToken = meta.GetValueOrDefault("calendarSyncToken");

        var result = await calendar.ListEventsAsync(conn, syncToken, ct);

        if (result.SyncTokenExpired)                               // 410 → drop token, re-baseline next run
        {
            meta.Remove("calendarSyncToken");
            Save(conn, meta);
            await db.SaveChangesAsync(ct);
            return;
        }

        // Only fire for events actually created after our baseline — edits to old events are ignored.
        foreach (var ev in result.Events.Where(e => e.Status == "confirmed" && e.Created >= baselineAt))
        {
            var tokens = new Dictionary<string, string>
            {
                ["event.title"] = ev.Title ?? "",
                ["event.start"] = ev.Start?.ToString("u") ?? "",
                ["event.end"] = ev.End?.ToString("u") ?? "",
                ["event.location"] = ev.Location ?? ""
            };
            await processor.ProcessAsync(new TriggerEvent(Provider.GoogleCalendar, TriggerTypes.CalendarEventCreated, conn.Id, $"gcal-created-{ev.Id}", tokens), ct);
        }

        if (result.NextSyncToken is not null)
            meta["calendarSyncToken"] = result.NextSyncToken;

        Save(conn, meta);
        await db.SaveChangesAsync(ct);
    }

    private static void Save(Connection conn, Dictionary<string, string> meta) =>
        conn.Metadata = JsonSerializer.Serialize(meta, Json);
}