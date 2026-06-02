using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace WorkflowAutomation.Api.Automations.Execution;

public class GoogleCreateEventExecutor(ITemplateRenderer renderer, HttpClient http) : IActionExecutor
{
    public string ActionType => ActionTypes.CalendarCreateEvent;

    public async Task<ActionResult> ExecuteAsync(ActionContext ctx, CancellationToken ct)
    {
        if (ctx.AccessToken is null) return ActionResult.Fail("Google connection token unavailable.");

        var title = renderer.Render(ctx.ActionConfig.GetValueOrDefault("titleTemplate") ?? "", ctx.Tokens);
        _ = int.TryParse(ctx.ActionConfig.GetValueOrDefault("startOffsetMinutes"), out var startOffset);
        if (!int.TryParse(ctx.ActionConfig.GetValueOrDefault("durationMinutes"), out var duration) || duration <= 0)
            duration = 30;

        var start = DateTimeOffset.UtcNow.AddMinutes(startOffset);
        var end = start.AddMinutes(duration);

        using var request = new HttpRequestMessage(HttpMethod.Post,
            "https://www.googleapis.com/calendar/v3/calendars/primary/events");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ctx.AccessToken);
        request.Content = JsonContent.Create(new
        {
            summary = title,
            start = new { dateTime = start.ToString("o") },
            end = new { dateTime = end.ToString("o") }
        });

        using var resp = await http.SendAsync(request, ct);
        if (resp.IsSuccessStatusCode) return ActionResult.Ok($"Created event \"{title}\"");

        var error = await resp.Content.ReadAsStringAsync(ct);
        return ActionResult.Fail($"Google Calendar error ({(int)resp.StatusCode}): {error}");
    }
}