using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;

namespace WorkflowAutomation.Api.Connections.Providers;

public record GoogleCalendarEvent(
    string Id,
    string? Title,
    string? Status,
    DateTimeOffset? Start,
    DateTimeOffset? End,
    string? Location,
    DateTimeOffset? Created
);

public record CalendarSyncResult(
    IReadOnlyList<GoogleCalendarEvent> Events,
    string? NextSyncToken,
    bool SyncTokenExpired
);

public interface IGoogleCalendarClient
{
    Task<CalendarSyncResult> ListEventsAsync(Connection connection, string? syncToken, CancellationToken ct);
}

public class GoogleCalendarClient(HttpClient http, IConnectionTokenAccessor tokens) : IGoogleCalendarClient
{
    private const string Base = "https://www.googleapis.com/calendar/v3/calendars/primary/events";

    public async Task<CalendarSyncResult> ListEventsAsync(Connection connection, string? syncToken, CancellationToken ct)
    {
        var accessToken = await tokens.GetAccessTokenAsync(connection.Id, ct);   // refreshes if stale
        if (accessToken is null) return new([], syncToken, false);

        var events = new List<GoogleCalendarEvent>();
        string? pageToken = null;
        string? nextSyncToken = null;

        do
        {
            var query = new Dictionary<string, string?> { ["singleEvents"] = "true" };
            if (syncToken is not null) query["syncToken"] = syncToken;       // can't combine with timeMin
            else query["timeMin"] = DateTimeOffset.UtcNow.ToString("o");     // baseline window
            if (pageToken is not null) query["pageToken"] = pageToken;

            using var request = new HttpRequestMessage(HttpMethod.Get, QueryHelpers.AddQueryString(Base, query));
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            using var resp = await http.SendAsync(request, ct);

            if (resp.StatusCode == HttpStatusCode.Gone) return new([], null, true);  // 410: sync token expired
            if (!resp.IsSuccessStatusCode) return new(events, syncToken, false);

            var doc = await resp.Content.ReadFromJsonAsync<JsonElement>(ct);
            if (doc.TryGetProperty("items", out var items))
                foreach (var item in items.EnumerateArray())
                    events.Add(Parse(item));

            pageToken = doc.TryGetProperty("nextPageToken", out var pt) ? pt.GetString() : null;
            nextSyncToken = doc.TryGetProperty("nextSyncToken", out var st) ? st.GetString() : nextSyncToken;
        }
        while (pageToken is not null);

        return new(events, nextSyncToken, false);
    }

    private static GoogleCalendarEvent Parse(JsonElement e)
    {
        DateTimeOffset? Date(string prop) => e.TryGetProperty(prop, out var x) && x.TryGetProperty("dateTime", out var dt)
                && DateTimeOffset.TryParse(dt.GetString(), out var v) ? v : null;

        return new GoogleCalendarEvent(
            e.GetProperty("id").GetString()!,
            e.TryGetProperty("summary", out var s) ? s.GetString() : null,
            e.TryGetProperty("status", out var st) ? st.GetString() : null,
            Date("start"), Date("end"),
            e.TryGetProperty("location", out var l) ? l.GetString() : null,
            e.TryGetProperty("created", out var c) && DateTimeOffset.TryParse(c.GetString(), out var cv) ? cv : null
        );
    }
}