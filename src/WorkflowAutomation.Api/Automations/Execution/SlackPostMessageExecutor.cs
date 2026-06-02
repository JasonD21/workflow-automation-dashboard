using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace WorkflowAutomation.Api.Automations.Execution;

public class SlackPostMessageExecutor(ITemplateRenderer renderer, HttpClient http) : IActionExecutor
{
    public string ActionType => ActionTypes.SlackPostMessage;

    public async Task<ActionResult> ExecuteAsync(ActionContext ctx, CancellationToken ct)
    {
        if (ctx.AccessToken is null)
            return ActionResult.Fail("Slack connection token unavailable.");

        var channel = ctx.ActionConfig.GetValueOrDefault("channelId");
        if (string.IsNullOrWhiteSpace(channel))
            return ActionResult.Fail("No Slack channel configured.");

        var text = renderer.Render(ctx.ActionConfig.GetValueOrDefault("messageTemplate") ?? "", ctx.Tokens);

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://slack.com/api/chat.postMessage");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ctx.AccessToken);
        request.Content = JsonContent.Create(new { channel, text });

        using var resp = await http.SendAsync(request, ct);
        var payload = await resp.Content.ReadFromJsonAsync<SlackPostResponse>(ct);

        return payload is { Ok: true }
            ? ActionResult.Ok($"Posted to {payload.Channel}")
            : ActionResult.Fail($"Slack error: {payload?.Error ?? "unknown"}");
    }

    private record SlackPostResponse
    {
        [JsonPropertyName("ok")] public bool Ok { get; init; }
        [JsonPropertyName("error")] public string? Error { get; init; }
        [JsonPropertyName("channel")] public string? Channel { get; init; }
    }
}