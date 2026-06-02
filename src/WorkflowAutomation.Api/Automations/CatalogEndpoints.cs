namespace WorkflowAutomation.Api.Automations;

public static class CatalogEndpoints
{
    public static IEndpointRouteBuilder MapCatalogEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/catalog").RequireAuthorization();
        group.MapGet("/triggers", () => Results.Ok(Catalog.Triggers));
        group.MapGet("/actions", () => Results.Ok(Catalog.Actions));
        return app;
    }
}