namespace WorkflowAutomation.Api.Automations;

public class Automation
{
    public Guid Id { get; set; } = SequentialGuid.New();
    public Guid UserId { get; set; }
    public string Name { get; set; } = default!;
    public bool IsEnabled { get; set; } = true;
    public bool IsDeleted { get; set; }

    public Provider TriggerProvider { get; set; }
    public string TriggerType { get; set; } = default!;    // e.g. "invoice.paid"
    public Guid TriggerConnectionId { get; set; }
    public string? FilterConfig { get; set; }              // jsonb, nullable

    public Provider ActionProvider { get; set; }
    public string ActionType { get; set; } = default!;     // e.g. "slack.post_message"
    public Guid? ActionConnectionId { get; set; }          // null for the email action
    public string ActionConfig { get; set; } = default!;   // jsonb
    public string? TriggerConfig { get; set; }   // jsonb, nullable

    public DateTimeOffset? LastTriggeredAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}