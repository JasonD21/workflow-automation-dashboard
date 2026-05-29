using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace WorkflowAutomation.Api.Identity;

public static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal principal)
    {
        var sub = principal.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(sub, out var id)
            ? id
            : throw new InvalidOperationException("Token has no valid subject claim.");
    }
}