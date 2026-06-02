using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WorkflowAutomation.Api.Connections;
using WorkflowAutomation.Api.Infrastructure.Persistence;

namespace WorkflowAutomation.Api.Automations.Execution;

public interface IAutomationRunner
{
    Task<AutomationRun> RunAsync(Automation automation, IReadOnlyDictionary<string, string> tokens, bool isTest, string? idempotencyKey, CancellationToken ct);
}

public class AutomationRunner(AppDbContext db, IActionExecutorResolver executors, IConnectionTokenAccessor tokenAccessor) : IAutomationRunner
{
    // (replace the DbContext line above per the note below)
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<AutomationRun> RunAsync(Automation automation, IReadOnlyDictionary<string, string> tokens,
        bool isTest, string? idempotencyKey, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        var run = new AutomationRun
        {
            AutomationId = automation.Id,
            UserId = automation.UserId,
            IsTest = isTest,
            TriggeredAt = DateTimeOffset.UtcNow,
            IdempotencyKey = idempotencyKey,
            TriggerPayloadSummary = JsonSerializer.Serialize(tokens, Json)
        };

        try
        {
            var filter = automation.FilterConfig is null
                ? null
                : JsonSerializer.Deserialize<FilterConditionDto>(automation.FilterConfig, Json);

            if (!FilterEvaluator.Passes(filter, tokens))
            {
                run.Status = RunStatus.Skipped;
                run.ActionResultSummary = JsonSerializer.Serialize(new { reason = "Filter not matched" }, Json);
            }
            else
            {
                var config = JsonSerializer.Deserialize<Dictionary<string, string>>(automation.ActionConfig, Json) ?? [];
                var accessToken = automation.ActionConnectionId is { } cid
                    ? await tokenAccessor.GetAccessTokenAsync(cid, ct)
                    : null;
                var userEmail = await db.Users
                    .Where(uu => uu.Id == automation.UserId)
                    .Select(uu => uu.Email)
                    .FirstOrDefaultAsync(ct);

                var ctx = new ActionContext(automation.UserId, userEmail, config, tokens, accessToken);
                var result = await executors.Get(automation.ActionType).ExecuteAsync(ctx, ct);

                run.Status = result.Success ? RunStatus.Success : RunStatus.Failed;
                run.ActionResultSummary = result.Summary is null ? null
                    : JsonSerializer.Serialize(new { summary = result.Summary }, Json);
                run.ErrorMessage = result.Error;
            }
        }
        catch (Exception ex)
        {
            run.Status = RunStatus.Failed;
            run.ErrorMessage = ex.Message;
        }

        sw.Stop();
        run.DurationMs = (int)sw.ElapsedMilliseconds;
        if (!isTest && run.Status == RunStatus.Success)
            automation.LastTriggeredAt = run.TriggeredAt;

        db.AutomationRuns.Add(run);
        await db.SaveChangesAsync(ct);
        return run;
    }
}