namespace WorkflowAutomation.Api.Automations.Execution;

public static class SampleData
{
    public static IReadOnlyDictionary<string, string> ForTrigger(string triggerType) => triggerType switch
    {
        TriggerTypes.InvoicePaid or TriggerTypes.InvoiceCreated => new Dictionary<string, string>
        {
            ["invoice.number"] = "1042",
            ["invoice.customer"] = "Acme Inc.",
            ["invoice.total"] = "1500.00",
            ["invoice.currency"] = "USD",
        },
        TriggerTypes.CalendarEventCreated => new Dictionary<string, string>
        {
            ["event.title"] = "Client kickoff call",
            ["event.start"] = DateTimeOffset.UtcNow.AddHours(2).ToString("u"),
            ["event.end"] = DateTimeOffset.UtcNow.AddHours(3).ToString("u"),
            ["event.location"] = "Google Meet",
        },
        TriggerTypes.SlackMessagePosted => new Dictionary<string, string>
        {
            ["message.text"] = "Can we get a quote for the new project?",
            ["message.user"] = "U12345",
            ["message.channel"] = "C67890",
        },
        _ => []
    };
}