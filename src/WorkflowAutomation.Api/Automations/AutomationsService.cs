using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WorkflowAutomation.Api.Automations.Execution;
using WorkflowAutomation.Api.Infrastructure.Persistence;

namespace WorkflowAutomation.Api.Automations;

public record FilterConditionDto(
    string Field,
    string Operator,
    string Value
);

public record SaveAutomationRequest(
    string Name,
    string TriggerType,
    Guid TriggerConnectionId,
    Dictionary<string, string>? TriggerConfig,
    FilterConditionDto? Filter,
    string ActionType,
    Guid? ActionConnectionId,
    Dictionary<string, string>? ActionConfig
);

public record AutomationDto(
    Guid Id,
    string Name,
    bool IsEnabled,
    string TriggerType,
    Provider TriggerProvider,
    Guid TriggerConnectionId,
    Dictionary<string, string>? TriggerConfig,
    FilterConditionDto? Filter,
    string ActionType,
    Provider ActionProvider,
    Guid? ActionConnectionId,
    Dictionary<string, string> ActionConfig,
    DateTimeOffset? LastTriggeredAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt
);

public record ValidationError(string Field, string Message);

public record SaveResult(AutomationDto? Automation, IReadOnlyList<ValidationError> Errors)
{
    public bool Ok => Errors.Count == 0;
    public static SaveResult Success(AutomationDto dto) => new(dto, []);
    public static SaveResult Fail(IReadOnlyList<ValidationError> e) => new(null, e);
}

public interface IAutomationsService
{
    Task<SaveResult> CreateAsync(Guid userId, SaveAutomationRequest req, CancellationToken ct);
    Task<SaveResult> UpdateAsync(Guid userId, Guid id, SaveAutomationRequest req, CancellationToken ct);
    Task<IReadOnlyList<AutomationDto>> ListAsync(Guid userId, CancellationToken ct);
    Task<AutomationDto?> GetAsync(Guid userId, Guid id, CancellationToken ct);
    Task<bool> SetEnabledAsync(Guid userId, Guid id, bool enabled, CancellationToken ct);
    Task<bool> DeleteAsync(Guid userId, Guid id, CancellationToken ct);
    Task<RunDto?> TestRunAsync(Guid userId, Guid id, CancellationToken ct);
}

public class AutomationsService(AppDbContext db, IAutomationRunner runner) : IAutomationsService
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<SaveResult> CreateAsync(Guid userId, SaveAutomationRequest req, CancellationToken ct)
    {
        var (errors, triggerDef, actionDef) = await ValidateAsync(userId, req, ct);
        if (errors.Count > 0)
            return SaveResult.Fail(errors);

        var a = new Automation { UserId = userId };
        Apply(a, req, triggerDef!, actionDef!);
        db.Automations.Add(a);
        await db.SaveChangesAsync(ct);
        return SaveResult.Success(Map(a));
    }

    public async Task<SaveResult> UpdateAsync(Guid userId, Guid id, SaveAutomationRequest req, CancellationToken ct)
    {
        var a = await db.Automations.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, ct);
        if (a is null) return SaveResult.Fail([new ValidationError("id", "Automation not found.")]);

        var (errors, triggerDef, actionDef) = await ValidateAsync(userId, req, ct);
        if (errors.Count > 0)
            return SaveResult.Fail(errors);

        Apply(a, req, triggerDef!, actionDef!);
        a.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return SaveResult.Success(Map(a));
    }

    public async Task<IReadOnlyList<AutomationDto>> ListAsync(Guid userId, CancellationToken ct) =>
        [.. (await db.Automations.Where(a => a.UserId == userId)
            .OrderByDescending(a => a.CreatedAt).ToListAsync(ct))
            .Select(Map)];

    public async Task<AutomationDto?> GetAsync(Guid userId, Guid id, CancellationToken ct)
    {
        var a = await db.Automations.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, ct);
        return a is null ? null : Map(a);
    }

    public async Task<bool> SetEnabledAsync(Guid userId, Guid id, bool enabled, CancellationToken ct)
    {
        var a = await db.Automations.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, ct);
        if (a is null) return false;
        a.IsEnabled = enabled;
        a.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> DeleteAsync(Guid userId, Guid id, CancellationToken ct)
    {
        var a = await db.Automations.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, ct);
        if (a is null) return false;
        a.IsDeleted = true;                 // soft delete; query filter hides it, runs survive
        a.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<RunDto?> TestRunAsync(Guid userId, Guid id, CancellationToken ct)
    {
        var automation = await db.Automations.FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId, ct);
        if (automation is null) return null;

        var tokens = Execution.SampleData.ForTrigger(automation.TriggerType);
        var run = await runner.RunAsync(automation, tokens, isTest: true, $"test-{Guid.NewGuid():N}", ct);
        return RunDto.From(run);
    }

    private static void Apply(Automation a, SaveAutomationRequest req,
        TriggerDefinition triggerDef, ActionDefinition actionDef)
    {
        a.Name = req.Name.Trim();
        a.TriggerProvider = triggerDef.Provider;
        a.TriggerType = req.TriggerType;
        a.TriggerConnectionId = req.TriggerConnectionId;
        a.TriggerConfig = req.TriggerConfig is null ? null : JsonSerializer.Serialize(req.TriggerConfig, Json);
        a.FilterConfig = req.Filter is null ? null : JsonSerializer.Serialize(req.Filter, Json);
        a.ActionProvider = actionDef.Provider;
        a.ActionType = req.ActionType;
        a.ActionConnectionId = req.ActionConnectionId;
        a.ActionConfig = JsonSerializer.Serialize(req.ActionConfig ?? [], Json);
    }

    private async Task<(List<ValidationError> Errors, TriggerDefinition? Trigger, ActionDefinition? Action)> ValidateAsync(Guid userId, SaveAutomationRequest req, CancellationToken ct)
    {
        var errors = new List<ValidationError>();

        if (string.IsNullOrWhiteSpace(req.Name))
            errors.Add(new("name", "Name is required."));

        var triggerDef = Catalog.Triggers.FirstOrDefault(t => t.Type == req.TriggerType);
        if (triggerDef is null) errors.Add(new("triggerType", $"Unknown trigger '{req.TriggerType}'."));

        var actionDef = Catalog.Actions.FirstOrDefault(a => a.Type == req.ActionType);
        if (actionDef is null) errors.Add(new("actionType", $"Unknown action '{req.ActionType}'."));

        var triggerConn = await db.Connections
            .FirstOrDefaultAsync(c => c.Id == req.TriggerConnectionId && c.UserId == userId, ct);
        if (triggerConn is null)
            errors.Add(new("triggerConnectionId", "Trigger connection not found."));
        else if (triggerDef is not null && triggerConn.Provider != triggerDef.Provider)
            errors.Add(new("triggerConnectionId",
                $"Connection is {triggerConn.Provider}, but trigger needs {triggerDef.Provider}."));

        if (actionDef is not null)
        {
            if (actionDef.RequiresConnection)
            {
                if (req.ActionConnectionId is null)
                    errors.Add(new("actionConnectionId", "This action requires a connection."));
                else
                {
                    var actionConn = await db.Connections.FirstOrDefaultAsync(c => c.Id == req.ActionConnectionId && c.UserId == userId, ct);
                    if (actionConn is null)
                        errors.Add(new("actionConnectionId", "Action connection not found."));
                    else if (actionConn.Provider != actionDef.Provider)
                        errors.Add(new("actionConnectionId",
                            $"Connection is {actionConn.Provider}, but action needs {actionDef.Provider}."));
                }
            }
            else if (req.ActionConnectionId is not null)
                errors.Add(new("actionConnectionId", "This action does not use a connection."));
        }

        if (triggerDef is not null)
            errors.AddRange(MissingFields(triggerDef.ConfigFields, req.TriggerConfig, "triggerConfig"));
        if (actionDef is not null)
            errors.AddRange(MissingFields(actionDef.ConfigFields, req.ActionConfig, "actionConfig"));

        return (errors, triggerDef, actionDef);
    }

    private static IEnumerable<ValidationError> MissingFields(IReadOnlyList<CatalogField> fields, Dictionary<string, string>? config, string prefix) =>
        fields.Where(f => f.Required && (config is null || !config.TryGetValue(f.Key, out var v) || string.IsNullOrWhiteSpace(v)))
              .Select(f => new ValidationError($"{prefix}.{f.Key}", $"{f.Label} is required."));

    private static AutomationDto Map(Automation a) => new(
        a.Id, a.Name, a.IsEnabled,
        a.TriggerType, a.TriggerProvider, a.TriggerConnectionId,
        a.TriggerConfig is null ? null : JsonSerializer.Deserialize<Dictionary<string, string>>(a.TriggerConfig, Json),
        a.FilterConfig is null ? null : JsonSerializer.Deserialize<FilterConditionDto>(a.FilterConfig, Json),
        a.ActionType, a.ActionProvider, a.ActionConnectionId,
        JsonSerializer.Deserialize<Dictionary<string, string>>(a.ActionConfig, Json) ?? [],
        a.LastTriggeredAt, a.CreatedAt, a.UpdatedAt
    );
}