namespace WorkflowAutomation.Api.Automations;

public static class TriggerTypes
{
    public const string InvoiceCreated = "quickbooks.invoice_created";
    public const string InvoicePaid = "quickbooks.invoice_paid";
    public const string CalendarEventCreated = "google.event_created";
    public const string SlackMessagePosted = "slack.message_posted";
}

public static class ActionTypes
{
    public const string SlackPostMessage = "slack.post_message";
    public const string EmailSend = "email.send";
    public const string CalendarCreateEvent = "google.create_event";
}

public record CatalogField(string Key, string Label, string Type, bool Required);

public record TriggerDefinition(string Type, Provider Provider, string DisplayName, string Description,
    IReadOnlyList<CatalogField> ConfigFields, IReadOnlyList<string> Tokens);

public record ActionDefinition(string Type, Provider Provider, string DisplayName, string Description,
    bool RequiresConnection, IReadOnlyList<CatalogField> ConfigFields,
    IReadOnlyList<string> TemplatedFields);

public static class Catalog
{
    public static readonly IReadOnlyList<TriggerDefinition> Triggers =
    [
        new(TriggerTypes.InvoicePaid, Provider.QuickBooks, "Invoice paid",
            "Fires when an invoice is marked paid in QuickBooks.",
            [], ["invoice.number", "invoice.customer", "invoice.total", "invoice.currency"]),

        new(TriggerTypes.InvoiceCreated, Provider.QuickBooks, "Invoice created",
            "Fires when a new invoice is created in QuickBooks.",
            [], ["invoice.number", "invoice.customer", "invoice.total", "invoice.currency"]),

        new(TriggerTypes.CalendarEventCreated, Provider.GoogleCalendar, "Calendar event created",
            "Fires when a new event is added to your Google Calendar.",
            [], ["event.title", "event.start", "event.end", "event.location"]),

        new(TriggerTypes.SlackMessagePosted, Provider.Slack, "Message posted",
            "Fires when a message is posted in a Slack channel.",
            [new CatalogField("channelId", "Channel", "slack-channel", true)],
            ["message.text", "message.user", "message.channel"]),
    ];

    public static readonly IReadOnlyList<ActionDefinition> Actions =
    [
        new(ActionTypes.SlackPostMessage, Provider.Slack, "Post a Slack message",
            "Posts a message to a Slack channel.", RequiresConnection: true,
            [new CatalogField("channelId", "Channel", "slack-channel", true),
             new CatalogField("messageTemplate", "Message", "textarea", true)],
            ["messageTemplate"]
        ),

        new(ActionTypes.EmailSend, Provider.Email, "Send an email",
            "Sends an email via the app's mailer.", RequiresConnection: false,
            [new CatalogField("to", "To", "email", false),
             new CatalogField("subjectTemplate", "Subject", "text", true),
             new CatalogField("bodyTemplate", "Body", "textarea", true)],
            ["subjectTemplate", "bodyTemplate"]
        ),

        new(ActionTypes.CalendarCreateEvent, Provider.GoogleCalendar, "Create a calendar event",
            "Creates an event on your Google Calendar.", RequiresConnection: true,
            [new CatalogField("titleTemplate", "Title", "text", true),
             new CatalogField("startOffsetMinutes", "Start (minutes from now)", "number", true),
             new CatalogField("durationMinutes", "Duration (minutes)", "number", true)],
            ["titleTemplate"]
        ),
    ];
}