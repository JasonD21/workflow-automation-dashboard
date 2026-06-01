using Microsoft.AspNetCore.DataProtection;

namespace WorkflowAutomation.Api.Connections;

public interface ITokenProtector
{
    string Protect(string plaintext);
    string Unprotect(string ciphertext);
}

public class TokenProtector(IDataProtectionProvider provider) : ITokenProtector
{
    private readonly IDataProtector _protector = provider.CreateProtector("WorkflowAutomation.ProviderTokens.v1");

    public string Protect(string plaintext) => _protector.Protect(plaintext);
    public string Unprotect(string ciphertext) => _protector.Unprotect(ciphertext);
}