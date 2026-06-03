namespace WorkflowAutomation.Api.Automations.Triggers;

public record TriggerEvent(
    Provider Provider,
    string TriggerType,
    Guid ConnectionId,
    string IdempotencyKey,                       // the provider's event id — dedupe key
    Dictionary<string, string> Tokens           // concrete type for clean Hangfire serialization
);