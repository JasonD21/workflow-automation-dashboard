using Hangfire.Dashboard;

namespace WorkflowAutomation.Api.Infrastructure;

public class HangfireDashboardAuth(string accessKey) : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var http = context.GetHttpContext();
        // Always allow from localhost (local dev).
        var ip = http.Connection.RemoteIpAddress;
        if (ip is not null && System.Net.IPAddress.IsLoopback(ip)) return true;

        // Otherwise require ?key=<secret>.
        return http.Request.Query["key"] == accessKey;
    }
}