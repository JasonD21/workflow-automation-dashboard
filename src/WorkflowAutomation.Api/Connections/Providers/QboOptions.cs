namespace WorkflowAutomation.Api.Connections.Providers;

public class QboOptions
{
    public const string SectionName = "QuickBooks";
    public string ClientId { get; set; } = default!;
    public string ClientSecret { get; set; } = default!;
    public string BaseUrl { get; set; } = "https://sandbox-quickbooks.api.intuit.com";
    public string WebhookVerifierToken { get; set; } = default!;
}