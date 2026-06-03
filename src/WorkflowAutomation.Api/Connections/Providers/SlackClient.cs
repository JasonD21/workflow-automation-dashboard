using System.Linq;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace WorkflowAutomation.Api.Connections.Providers;

public record SlackChannelActivity(
    string ChannelName,
    int MessageCount
);

public interface ISlackClient
{
    Task<IReadOnlyList<SlackChannelActivity>> GetActivityAsync(Connection connection, DateTimeOffset since, CancellationToken ct);
}

public class SlackClient(HttpClient http, IConnectionTokenAccessor tokens) : ISlackClient
{
    public async Task<IReadOnlyList<SlackChannelActivity>> GetActivityAsync(Connection connection, DateTimeOffset since, CancellationToken ct)
    {
        var token = await tokens.GetAccessTokenAsync(connection.Id, ct);
        if (token is null) return [];

        var channels = await GetMemberChannelsAsync(token, ct);   // empty if channels:read scope missing
        var oldest = since.ToUnixTimeSeconds().ToString();

        var result = new List<SlackChannelActivity>();
        foreach (var (id, name) in channels.Take(10))
        {
            var count = await CountMessagesAsync(token, id, oldest, ct);
            if (count > 0) result.Add(new SlackChannelActivity(name, count));
        }
        return result;
    }

    private async Task<List<(string Id, string Name)>> GetMemberChannelsAsync(string token, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, "https://slack.com/api/conversations.list?types=public_channel&limit=200&exclude_archived=true");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var resp = await http.SendAsync(req, ct);
        var doc = await resp.Content.ReadFromJsonAsync<JsonElement>(ct);

        var list = new List<(string, string)>();
        if (doc.TryGetProperty("ok", out var ok) && ok.GetBoolean() && doc.TryGetProperty("channels", out var chans))
            foreach (var c in chans.EnumerateArray())
                if (c.TryGetProperty("is_member", out var m) && m.GetBoolean())
                    list.Add((c.GetProperty("id").GetString()!, c.GetProperty("name").GetString() ?? ""));

        return list;
    }

    private async Task<int> CountMessagesAsync(string token, string channelId, string oldest, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, $"https://slack.com/api/conversations.history?channel={channelId}&oldest={oldest}&limit=200");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var resp = await http.SendAsync(req, ct);
        var doc = await resp.Content.ReadFromJsonAsync<JsonElement>(ct);

        if (doc.TryGetProperty("ok", out var ok) && ok.GetBoolean() && doc.TryGetProperty("messages", out var msgs))
            return msgs.EnumerateArray().Count(m => !m.TryGetProperty("subtype", out _));  // skip system messages

        return 0;
    }
}