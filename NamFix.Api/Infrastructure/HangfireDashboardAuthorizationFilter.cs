using System.Net;
using Hangfire.Dashboard;

namespace NamFix.Api.Infrastructure;

/// <summary>
/// Authorization for the Hangfire dashboard. The dashboard is reached by plain browser navigation,
/// which cannot carry the app's JWT bearer token (it lives in the client's localStorage), so a
/// role-based check would lock everyone out. Instead we restrict the dashboard to <b>local requests
/// only</b> — an operator/admin views it from the server host. Tighten this (e.g. a reverse-proxy or
/// cookie auth) before exposing the API publicly.
/// </summary>
public sealed class HangfireDashboardAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var http = context.GetHttpContext();
        var remote = http.Connection.RemoteIpAddress;
        return remote is not null && IPAddress.IsLoopback(remote);
    }
}
