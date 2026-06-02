using System.Security.Claims;
using WorkflowAutomation.Api.Identity;

namespace WorkflowAutomation.Api.Automations;

public static class AutomationEndpoints
{
    public record EnabledRequest(bool Enabled);

    public static IEndpointRouteBuilder MapAutomationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/automations").RequireAuthorization();
        group.MapGet("", List);
        group.MapGet("/{id:guid}", Get);
        group.MapPost("", Create);
        group.MapPost("/{id:guid}/test", TestRun);
        group.MapPut("/{id:guid}", Update);
        group.MapPatch("/{id:guid}/enabled", SetEnabled);
        group.MapDelete("/{id:guid}", Delete);
        return app;
    }

    private static async Task<IResult> List(ClaimsPrincipal u, IAutomationsService svc, CancellationToken ct) => Results.Ok(await svc.ListAsync(u.GetUserId(), ct));

    private static async Task<IResult> Get(Guid id, ClaimsPrincipal u, IAutomationsService svc, CancellationToken ct)
    {
        var dto = await svc.GetAsync(u.GetUserId(), id, ct);
        return dto is null ? Results.NotFound() : Results.Ok(dto);
    }

    private static async Task<IResult> Create(SaveAutomationRequest req, ClaimsPrincipal u, IAutomationsService svc, CancellationToken ct)
    {
        var r = await svc.CreateAsync(u.GetUserId(), req, ct);
        return r.Ok ? Results.Created($"/api/automations/{r.Automation!.Id}", r.Automation) : ToProblem(r);
    }

    private static async Task<IResult> TestRun(Guid id, ClaimsPrincipal u, IAutomationsService svc, CancellationToken ct)
    {
        var run = await svc.TestRunAsync(u.GetUserId(), id, ct);
        return run is null ? Results.NotFound() : Results.Ok(run);
    }

    private static async Task<IResult> Update(Guid id, SaveAutomationRequest req, ClaimsPrincipal u, IAutomationsService svc, CancellationToken ct)
    {
        var r = await svc.UpdateAsync(u.GetUserId(), id, req, ct);
        return r.Ok ? Results.Ok(r.Automation) : ToProblem(r);
    }

    private static async Task<IResult> SetEnabled(Guid id, EnabledRequest body, ClaimsPrincipal u, IAutomationsService svc, CancellationToken ct)
        => await svc.SetEnabledAsync(u.GetUserId(), id, body.Enabled, ct) ? Results.NoContent() : Results.NotFound();

    private static async Task<IResult> Delete(Guid id, ClaimsPrincipal u, IAutomationsService svc, CancellationToken ct)
        => await svc.DeleteAsync(u.GetUserId(), id, ct) ? Results.NoContent() : Results.NotFound();

    private static IResult ToProblem(SaveResult r)
    {
        if (r.Errors.Any(e => e.Field == "id")) return Results.NotFound();
        var dict = r.Errors.GroupBy(e => e.Field)
            .ToDictionary(g => g.Key, g => g.Select(e => e.Message).ToArray());
        return Results.ValidationProblem(dict);
    }
}