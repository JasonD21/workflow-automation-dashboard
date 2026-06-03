using Hangfire;

namespace WorkflowAutomation.Api.Automations.Triggers;

public static class DevTriggerEndpoints
{
    public record SimulateRequest(Guid ConnectionId, string TriggerType,
        Dictionary<string, string> Tokens, string? IdempotencyKey);

    public static IEndpointRouteBuilder MapDevTriggerEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/dev/simulate-trigger", (SimulateRequest req) =>
        {
            var def = Catalog.Triggers.FirstOrDefault(t => t.Type == req.TriggerType);
            if (def is null) return Results.BadRequest("Unknown trigger type.");

            var evt = new TriggerEvent(def.Provider, req.TriggerType, req.ConnectionId,
                req.IdempotencyKey ?? $"sim-{Guid.NewGuid():N}", req.Tokens);

            BackgroundJob.Enqueue<ProcessTriggerJob>(j => j.RunAsync(evt));
            return Results.Accepted();
        }).RequireAuthorization();

        return app;
    }
}