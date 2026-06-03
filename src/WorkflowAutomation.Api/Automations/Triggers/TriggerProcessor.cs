using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WorkflowAutomation.Api.Automations.Execution;
using WorkflowAutomation.Api.Infrastructure.Persistence;

namespace WorkflowAutomation.Api.Automations.Triggers;

public interface ITriggerProcessor
{
    Task ProcessAsync(TriggerEvent evt, CancellationToken ct);
}

public class TriggerProcessor(AppDbContext db, IAutomationRunner runner) : ITriggerProcessor
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task ProcessAsync(TriggerEvent evt, CancellationToken ct)
    {
        var automations = await db.Automations
            .Where(a => a.IsEnabled && a.TriggerProvider == evt.Provider
                && a.TriggerType == evt.TriggerType
                && a.TriggerConnectionId == evt.ConnectionId)
            .ToListAsync(ct);

        foreach (var automation in automations)
        {
            if (!TriggerConfigMatches(automation, evt)) continue;

            // idempotency: this automation already handled this event?
            var already = await db.AutomationRuns.AnyAsync(
                r => r.AutomationId == automation.Id && r.IdempotencyKey == evt.IdempotencyKey, ct);
            if (already) continue;

            await runner.RunAsync(automation, evt.Tokens, isTest: false, evt.IdempotencyKey, ct);
        }
    }

    private static bool TriggerConfigMatches(Automation a, TriggerEvent evt)
    {
        // Only the Slack "message posted" trigger scopes to a specific channel.
        if (a.TriggerType != TriggerTypes.SlackMessagePosted) return true;

        var cfg = a.TriggerConfig is null
            ? null
            : JsonSerializer.Deserialize<Dictionary<string, string>>(a.TriggerConfig, Json);
        var channelId = cfg?.GetValueOrDefault("channelId");
        return string.IsNullOrEmpty(channelId)
            || evt.Tokens.GetValueOrDefault("message.channel") == channelId;
    }
}