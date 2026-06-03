using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WorkflowAutomation.Api.Automations.Triggers;
using WorkflowAutomation.Api.Connections.Providers;
using WorkflowAutomation.Api.Infrastructure.Persistence;

namespace WorkflowAutomation.Api.Connections.Webhooks;

public static class QboWebhook
{
    public static IEndpointRouteBuilder MapQboWebhook(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/webhooks/quickbooks", Handle).AllowAnonymous();
        return app;
    }

    private static async Task<IResult> Handle(HttpRequest req, IOptions<QboOptions> options, AppDbContext db, CancellationToken ct)
    {
        req.EnableBuffering();
        using var reader = new StreamReader(req.Body, Encoding.UTF8, leaveOpen: true);
        var rawBody = await reader.ReadToEndAsync(ct);
        req.Body.Position = 0;

        if (!VerifySignature(req, rawBody, options.Value.WebhookVerifierToken))
            return Results.Unauthorized();

        using var doc = JsonDocument.Parse(rawBody);
        if (!doc.RootElement.TryGetProperty("eventNotifications", out var notifications))
            return Results.Ok();

        foreach (var n in notifications.EnumerateArray())
        {
            var realmId = n.GetProperty("realmId").GetString();
            var connection = await db.Connections.FirstOrDefaultAsync(
                c => c.Provider == Provider.QuickBooks && c.ProviderAccountId == realmId, ct);
            if (connection is null) continue;

            if (!n.TryGetProperty("dataChangeEvent", out var dce)
                || !dce.TryGetProperty("entities", out var entities)) continue;

            foreach (var e in entities.EnumerateArray())
            {
                if (e.GetProperty("name").GetString() != "Invoice") continue;
                var id = e.GetProperty("id").GetString()!;
                var op = e.TryGetProperty("operation", out var o) ? o.GetString() ?? "" : "";
                if (op is "Delete" or "Void") continue;

                BackgroundJob.Enqueue<QboEntityChangeJob>(j => j.RunAsync(connection.Id, id, op));
            }
        }

        return Results.Ok();
    }

    private static bool VerifySignature(HttpRequest req, string rawBody, string verifierToken)
    {
        var signature = req.Headers["intuit-signature"].ToString();
        if (string.IsNullOrEmpty(signature)) return false;

        var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(verifierToken), Encoding.UTF8.GetBytes(rawBody));
        var expected = Convert.ToBase64String(hash);   // Intuit sends base64, not hex
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(signature));
    }
}