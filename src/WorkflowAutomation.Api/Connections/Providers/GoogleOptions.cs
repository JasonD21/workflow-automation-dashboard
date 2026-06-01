namespace WorkflowAutomation.Api.Connections.Providers;

public class GoogleOptions
{
    public const string SectionName = "Google";
    public string ClientId { get; set; } = default!;
    public string ClientSecret { get; set; } = default!;
}