namespace WorkflowAutomation.Api.Connections;

public interface IOAuthProviderResolver
{
    IOAuthProvider Get(Provider provider);
}

public class OAuthProviderResolver(IEnumerable<IOAuthProvider> providers) : IOAuthProviderResolver
{
    private readonly Dictionary<Provider, IOAuthProvider> _map = providers.ToDictionary(p => p.Provider);

    public IOAuthProvider Get(Provider provider) => _map.TryGetValue(provider, out var p)
            ? p
            : throw new NotSupportedException($"No OAuth provider for {provider}.");
}