namespace WorkflowAutomation.Api.Automations.Execution;

public record ActionContext(
    Guid UserId,
    string? UserEmail,
    IReadOnlyDictionary<string, string> ActionConfig,
    IReadOnlyDictionary<string, string> Tokens,   // the trigger payload, flattened
    string? AccessToken  // decrypted; null for connection-less actions (email)
);

public record ActionResult(bool Success, string? Summary, string? Error)
{
    public static ActionResult Ok(string summary) => new(true, summary, null);
    public static ActionResult Fail(string error) => new(false, null, error);
}

public interface IActionExecutor
{
    string ActionType { get; }
    Task<ActionResult> ExecuteAsync(ActionContext ctx, CancellationToken ct);
}

public interface IActionExecutorResolver
{
    IActionExecutor Get(string actionType);
}

public class ActionExecutorResolver(IEnumerable<IActionExecutor> executors) : IActionExecutorResolver
{
    private readonly Dictionary<string, IActionExecutor> _map = executors.ToDictionary(e => e.ActionType);

    public IActionExecutor Get(string actionType) =>
        _map.TryGetValue(actionType, out var e)
            ? e
            : throw new NotSupportedException($"No executor for action '{actionType}'.");
}