using Microsoft.AspNetCore.Identity;

namespace WorkflowAutomation.Api.Identity;

public class ApplicationUser : IdentityUser<Guid>
{
    public string? DisplayName { get; set; }
    public string TimeZone { get; set; } = "UTC";   // IANA
}