using Microsoft.EntityFrameworkCore;
using WorkflowAutomation.Api.Connections.Providers;
using WorkflowAutomation.Api.Infrastructure.Persistence;

namespace WorkflowAutomation.Api.Automations.Triggers;

public class QboEntityChangeJob(AppDbContext db, IQboClient qbo, ITriggerProcessor processor)
{
    public async Task RunAsync(Guid connectionId, string invoiceId, string operation)
    {
        var ct = CancellationToken.None;
        var connection = await db.Connections.FirstOrDefaultAsync(c => c.Id == connectionId, ct);
        if (connection is null) return;

        var invoice = await qbo.GetInvoiceAsync(connection, invoiceId, ct);
        if (invoice is null) return;

        var tokens = new Dictionary<string, string>
        {
            ["invoice.number"] = invoice.Number ?? invoice.Id,
            ["invoice.customer"] = invoice.Customer ?? "",
            ["invoice.total"] = invoice.Total.ToString("0.00"),
            ["invoice.currency"] = invoice.Currency ?? ""
        };

        if (operation == "Create")
            await processor.ProcessAsync(new TriggerEvent(Provider.QuickBooks, TriggerTypes.InvoiceCreated,
                connectionId, $"qbo-invoice-created-{invoice.Id}", tokens), ct);

        // "Paid" = balance cleared on a positive invoice. Idempotency key fires this once per invoice.
        if (invoice.Balance == 0 && invoice.Total > 0)
            await processor.ProcessAsync(new TriggerEvent(Provider.QuickBooks, TriggerTypes.InvoicePaid,
                connectionId, $"qbo-invoice-paid-{invoice.Id}", tokens), ct);
    }
}