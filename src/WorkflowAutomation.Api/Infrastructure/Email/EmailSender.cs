using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;

namespace WorkflowAutomation.Api.Infrastructure.Email;

public record EmailResult(bool Success, string? Error);

public interface IEmailSender
{
    Task<EmailResult> SendAsync(string to, string subject, string html, CancellationToken ct);
}

public class ResendEmailSender(HttpClient http, IOptions<ResendOptions> options) : IEmailSender
{
    private readonly ResendOptions _options = options.Value;

    public async Task<EmailResult> SendAsync(string to, string subject, string html, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.resend.com/emails");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        req.Content = JsonContent.Create(new { from = _options.FromEmail, to = new[] { to }, subject, html });

        using var resp = await http.SendAsync(req, ct);
        if (resp.IsSuccessStatusCode) return new EmailResult(true, null);

        var error = await resp.Content.ReadAsStringAsync(ct);
        return new EmailResult(false, $"Resend ({(int)resp.StatusCode}): {error}");
    }
}