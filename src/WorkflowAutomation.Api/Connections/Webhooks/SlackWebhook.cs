using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WorkflowAutomation.Api.Automations;
using WorkflowAutomation.Api.Automations.Triggers;
using WorkflowAutomation.Api.Connections.Providers;
using WorkflowAutomation.Api.Infrastructure.Persistence;

namespace WorkflowAutomation.Api.Connections.Webhooks;

public static class SlackWebhook
{
    public static IEndpointRouteBuilder MapSlackWebhook(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/webhooks/slack", Handle).AllowAnonymous();
        return app;
    }

    private static async Task<IResult> Handle(HttpRequest req, IOptions<SlackOptions> options, AppDbContext db, CancellationToken ct)
    {
        // 1. Read the RAW body (signature is computed over exact bytes).
        req.EnableBuffering();
        using var reader = new StreamReader(req.Body, Encoding.UTF8, leaveOpen: true);
        var rawBody = await reader.ReadToEndAsync(ct);
        req.Body.Position = 0;

        using var doc = JsonDocument.Parse(rawBody);
        var root = doc.RootElement;

        // 2. URL verification handshake — echo the challenge, BEFORE signature checks.
        if (root.TryGetProperty("type", out var t) && t.GetString() == "url_verification")
            return Results.Ok(new { challenge = root.GetProperty("challenge").GetString() });

        // 3. Verify the signature.
        if (!VerifySignature(req, rawBody, options.Value.SigningSecret))
            return Results.Unauthorized();

        // 4. Only handle plain channel messages (ignore bots, edits, joins, etc.).
        if (root.GetProperty("type").GetString() != "event_callback") return Results.Ok();
        var ev = root.GetProperty("event");
        if (ev.GetProperty("type").GetString() != "message"
            || ev.TryGetProperty("subtype", out _)
            || ev.TryGetProperty("bot_id", out _))
            return Results.Ok();

        var teamId = root.GetProperty("team_id").GetString();
        var connection = await db.Connections.FirstOrDefaultAsync(
            c => c.Provider == Provider.Slack && c.ProviderAccountId == teamId, ct);
        if (connection is null) return Results.Ok();   // unknown workspace — ack and drop

        var tokens = new Dictionary<string, string>
        {
            ["message.text"] = ev.GetProperty("text").GetString() ?? "",
            ["message.user"] = ev.TryGetProperty("user", out var u) ? u.GetString() ?? "" : "",
            ["message.channel"] = ev.GetProperty("channel").GetString() ?? ""
        };
        var eventId = root.GetProperty("event_id").GetString()!;   // Slack's dedupe id

        var evt = new TriggerEvent(Provider.Slack, TriggerTypes.SlackMessagePosted,
            connection.Id, eventId, tokens);

        // 5. ACK immediately, process off-thread.
        BackgroundJob.Enqueue<ProcessTriggerJob>(j => j.RunAsync(evt));
        return Results.Ok();
    }

    private static bool VerifySignature(HttpRequest req, string rawBody, string signingSecret)
    {
        var timestamp = req.Headers["X-Slack-Request-Timestamp"].ToString();
        var signature = req.Headers["X-Slack-Signature"].ToString();
        if (string.IsNullOrEmpty(timestamp) || string.IsNullOrEmpty(signature)) return false;

        // Replay protection: reject if older than 5 minutes.
        if (!long.TryParse(timestamp, out var ts)
            || Math.Abs(DateTimeOffset.UtcNow.ToUnixTimeSeconds() - ts) > 300) return false;

        var baseString = $"v0:{timestamp}:{rawBody}";
        var hash = HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(signingSecret), Encoding.UTF8.GetBytes(baseString));
        var expected = "v0=" + Convert.ToHexString(hash).ToLowerInvariant();

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(signature));
    }
}