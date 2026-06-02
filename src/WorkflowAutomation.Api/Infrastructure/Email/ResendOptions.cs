namespace WorkflowAutomation.Api.Infrastructure.Email;

public class ResendOptions
{
    public const string SectionName = "Resend";
    public string ApiKey { get; set; } = default!;
    public string FromEmail { get; set; } = "onboarding@resend.dev";
}