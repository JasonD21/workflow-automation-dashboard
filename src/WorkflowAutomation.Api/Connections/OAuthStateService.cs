using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;

namespace WorkflowAutomation.Api.Connections;

public record OAuthState(Guid UserId, Provider Provider, string Nonce);

public interface IOAuthStateService
{
    string Create(Guid userId, Provider provider);
    OAuthState? Validate(string state);
}

public class OAuthStateService(IDataProtectionProvider provider) : IOAuthStateService
{
    private readonly ITimeLimitedDataProtector _protector = provider.CreateProtector("WorkflowAutomation.OAuthState.v1")
            .ToTimeLimitedDataProtector();

    public string Create(Guid userId, Provider provider)
    {
        var json = JsonSerializer.Serialize(new OAuthState(userId, provider, Guid.NewGuid().ToString("N")));
        return _protector.Protect(json, TimeSpan.FromMinutes(10));
    }

    public OAuthState? Validate(string state)
    {
        try
        {
            return JsonSerializer.Deserialize<OAuthState>(_protector.Unprotect(state));
        }
        catch
        {
            return null;   // tampered, expired, or malformed → reject
        }
    }
}