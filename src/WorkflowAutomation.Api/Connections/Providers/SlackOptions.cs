namespace WorkflowAutomation.Api.Connections.Providers;

public class SlackOptions
{
    public const string SectionName = "Slack";
    public string ClientId { get; set; } = default!;
    public string ClientSecret { get; set; } = default!;
    public string SigningSecret { get; set; } = default!;
}