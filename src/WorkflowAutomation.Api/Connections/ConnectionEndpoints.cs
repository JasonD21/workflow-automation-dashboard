using System.Security.Claims;
using WorkflowAutomation.Api.Identity;

namespace WorkflowAutomation.Api.Connections;

public static class ConnectionEndpoints
{
    public static IEndpointRouteBuilder MapConnectionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/connections").RequireAuthorization();
        group.MapGet("", List);
        group.MapGet("/{id:guid}", Get);
        group.MapGet("/{provider}/authorize", Authorize);
        group.MapDelete("/{id:guid}", Disconnect);

        // Callback is the provider's browser redirect — no JWT, identity comes from signed state.
        app.MapGet("/api/connections/{provider}/callback", Callback).AllowAnonymous();
        return app;
    }

    private static async Task<IResult> List(ClaimsPrincipal user, IConnectionsService svc, CancellationToken ct) => Results.Ok(await svc.ListAsync(user.GetUserId(), ct));

    private static async Task<IResult> Get(Guid id, ClaimsPrincipal user, IConnectionsService svc, CancellationToken ct)
    {
        var dto = await svc.GetAsync(user.GetUserId(), id, ct);
        return dto is null ? Results.NotFound() : Results.Ok(dto);
    }

    private static IResult Authorize(string provider, ClaimsPrincipal user, IConnectionsService svc, HttpRequest req)
    {
        if (!Enum.TryParse<Provider>(provider, ignoreCase: true, out var p))
            return Results.NotFound();

        var url = svc.BuildAuthorizeUrl(user.GetUserId(), p, RedirectUri(req, p));
        return Results.Ok(new { authorizeUrl = url });
    }

    private static async Task<IResult> Callback(string provider, string? code, string? state,
        IConnectionsService svc, IConfiguration config, HttpRequest req, CancellationToken ct)
    {
        var frontend = config["Frontend:BaseUrl"] ?? "https://localhost:4200";
        if (!Enum.TryParse<Provider>(provider, true, out var p) || code is null || state is null)
            return Results.Redirect($"{frontend}/connections?status=error");

        var result = await svc.HandleCallbackAsync(code, state, RedirectUri(req, p), ct);
        var status = result is null ? "error" : "connected";
        return Results.Redirect($"{frontend}/connections?status={status}&provider={p.ToString().ToLowerInvariant()}");
    }

    private static async Task<IResult> Disconnect(Guid id, ClaimsPrincipal user, IConnectionsService svc, CancellationToken ct) => await svc.DisconnectAsync(user.GetUserId(), id, ct)
        ? Results.NoContent()
        : Results.NotFound();

    private static string RedirectUri(HttpRequest req, Provider provider) => $"{req.Scheme}://{req.Host}/api/connections/{provider.ToString().ToLowerInvariant()}/callback";
}