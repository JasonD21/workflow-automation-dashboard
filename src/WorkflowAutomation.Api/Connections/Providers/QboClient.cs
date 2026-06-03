using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace WorkflowAutomation.Api.Connections.Providers;

public record QboInvoice(
    string Id,
    string? Number,
    string? Customer,
    decimal Total,
    decimal Balance,
    string? Currency,
    DateOnly? TxnDate
);

public interface IQboClient
{
    Task<QboInvoice?> GetInvoiceAsync(Connection connection, string invoiceId, CancellationToken ct);
    Task<IReadOnlyList<QboInvoice>> QueryInvoicesAsync(Connection connection, CancellationToken ct);
}

public class QboClient(HttpClient http, IConnectionTokenAccessor tokens, IOptions<QboOptions> options) : IQboClient
{
    private readonly QboOptions _options = options.Value;

    public async Task<QboInvoice?> GetInvoiceAsync(Connection connection, string invoiceId, CancellationToken ct)
    {
        var accessToken = await tokens.GetAccessTokenAsync(connection.Id, ct);   // refreshes if stale
        if (accessToken is null) return null;

        var url = $"{_options.BaseUrl}/v3/company/{connection.ProviderAccountId}/invoice/{invoiceId}?minorversion=70";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var resp = await http.SendAsync(request, ct);
        if (!resp.IsSuccessStatusCode) return null;

        var doc = await resp.Content.ReadFromJsonAsync<JsonElement>(ct);
        return ParseInvoice(doc.GetProperty("Invoice"));
    }

    public async Task<IReadOnlyList<QboInvoice>> QueryInvoicesAsync(Connection connection, CancellationToken ct)
    {
        var accessToken = await tokens.GetAccessTokenAsync(connection.Id, ct);
        if (accessToken is null) return [];

        var query = "SELECT * FROM Invoice ORDERBY MetaData.LastUpdatedTime DESC MAXRESULTS 1000";
        var url = $"{_options.BaseUrl}/v3/company/{connection.ProviderAccountId}/query?query={Uri.EscapeDataString(query)}&minorversion=70";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var resp = await http.SendAsync(request, ct);
        if (!resp.IsSuccessStatusCode) return [];

        var doc = await resp.Content.ReadFromJsonAsync<JsonElement>(ct);
        if (!doc.TryGetProperty("QueryResponse", out var qr) || !qr.TryGetProperty("Invoice", out var arr))
            return [];   // QBO omits "Invoice" entirely when there are none

        return arr.EnumerateArray().Select(ParseInvoice).ToList();
    }

    private static QboInvoice ParseInvoice(JsonElement inv) => new(
        inv.GetProperty("Id").GetString()!,
        inv.TryGetProperty("DocNumber", out var d) ? d.GetString() : null,
        inv.TryGetProperty("CustomerRef", out var c) && c.TryGetProperty("name", out var cn) ? cn.GetString() : null,
        inv.TryGetProperty("TotalAmt", out var ta) ? ta.GetDecimal() : 0m,
        inv.TryGetProperty("Balance", out var b) ? b.GetDecimal() : 0m,
        inv.TryGetProperty("CurrencyRef", out var cr) && cr.TryGetProperty("value", out var cv) ? cv.GetString() : null,
        inv.TryGetProperty("TxnDate", out var td) && DateOnly.TryParse(td.GetString(), out var dt) ? dt : null
    );
}