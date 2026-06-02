using WorkflowAutomation.Api.Infrastructure.Email;

namespace WorkflowAutomation.Api.Automations.Execution;

public class EmailSendExecutor(ITemplateRenderer renderer, IEmailSender email) : IActionExecutor
{
    public string ActionType => ActionTypes.EmailSend;

    public async Task<ActionResult> ExecuteAsync(ActionContext ctx, CancellationToken ct)
    {
        var to = ctx.ActionConfig.GetValueOrDefault("to");
        if (string.IsNullOrWhiteSpace(to)) to = ctx.UserEmail;
        if (string.IsNullOrWhiteSpace(to)) return ActionResult.Fail("No recipient email available.");

        var subject = renderer.Render(ctx.ActionConfig.GetValueOrDefault("subjectTemplate") ?? "", ctx.Tokens);
        var body = renderer.Render(ctx.ActionConfig.GetValueOrDefault("bodyTemplate") ?? "", ctx.Tokens);

        var result = await email.SendAsync(to, subject, body, ct);
        return result.Success ? ActionResult.Ok($"Email sent to {to}") : ActionResult.Fail(result.Error!);
    }
}