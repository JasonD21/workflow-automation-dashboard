namespace WorkflowAutomation.Api.Connections;

public class Connection
{
    public Guid Id { get; set; } = SequentialGuid.New();
    public Guid UserId { get; set; }
    public Provider Provider { get; set; }
    public string ProviderAccountId { get; set; } = default!;
    public string? DisplayName { get; set; }
    public ConnectionStatus Status { get; set; } = ConnectionStatus.Active;
    public string? GrantedScopes { get; set; }
    public string? Metadata { get; set; }            // jsonb
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastRefreshedAt { get; set; }
    public ProviderToken? Token { get; set; }
}