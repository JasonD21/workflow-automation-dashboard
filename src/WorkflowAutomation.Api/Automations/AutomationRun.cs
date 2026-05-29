namespace WorkflowAutomation.Api.Automations;

public class AutomationRun
{
    public Guid Id { get; set; } = SequentialGuid.New();
    public Guid AutomationId { get; set; }
    public Guid UserId { get; set; }                      // denormalized for fast scoping
    public DateTimeOffset TriggeredAt { get; set; } = DateTimeOffset.UtcNow;
    public RunStatus Status { get; set; }
    public string? IdempotencyKey { get; set; }
    public string? TriggerPayloadSummary { get; set; }    // jsonb
    public string? ActionResultSummary { get; set; }      // jsonb
    public string? ErrorMessage { get; set; }
    public int? DurationMs { get; set; }
}