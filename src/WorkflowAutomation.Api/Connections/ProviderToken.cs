namespace WorkflowAutomation.Api.Connections;

public class ProviderToken
{
    public Guid Id { get; set; } = SequentialGuid.New();
    public Guid ConnectionId { get; set; }
    public Connection Connection { get; set; } = default!;
    public string AccessTokenEncrypted { get; set; } = default!;
    public string? RefreshTokenEncrypted { get; set; }
    public DateTimeOffset? AccessTokenExpiresAt { get; set; }
    public DateTimeOffset? RefreshTokenExpiresAt { get; set; }
}