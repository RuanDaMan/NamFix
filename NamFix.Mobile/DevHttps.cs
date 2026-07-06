using System.Net;
using System.Net.Security;
using System.Net.WebSockets;
using Microsoft.AspNetCore.Http.Connections.Client;

namespace NamFix.Mobile;

/// <summary>
/// Local-development HTTPS trust for the MAUI app. The local API runs over HTTPS with an
/// mkcert/ASP.NET dev certificate that Android does not trust out of the box, so both REST calls and
/// SignalR handshakes (<c>/hubs/status</c>, <c>/hubs/notifications</c>) fail their TLS handshake.
///
/// We accept that certificate <b>only when the target host is a local/dev address</b> (loopback, the
/// emulator's <c>10.0.2.2</c> alias for the host loopback, or a private-LAN IPv4). A real deployed API
/// on a public host still gets full certificate validation, so this is safe to ship — which is why it
/// is keyed on the <i>host</i> rather than on <c>#if DEBUG</c>, and therefore works in Debug AND
/// Release against the local API while remaining secure against a production endpoint.
/// </summary>
internal static class DevHttps
{
    /// <summary>Whether the configured API base points at a local dev host (evaluated once).</summary>
    private static readonly bool TrustApiHost = IsDevHost(new Uri(MobileConfig.ApiBaseUrl).Host);

    /// <summary>True for hosts that are local development targets, where the dev cert is acceptable.</summary>
    public static bool IsDevHost(string? host)
    {
        if (string.IsNullOrEmpty(host)) return false;
        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase)) return true;
        if (host == "10.0.2.2") return true; // Android emulator alias for the host loopback.
        if (!IPAddress.TryParse(host, out var ip)) return false;
        if (IPAddress.IsLoopback(ip)) return true;

        var b = ip.GetAddressBytes();
        if (b.Length != 4) return false; // Private ranges below are IPv4 only.
        return b[0] == 10
            || (b[0] == 172 && b[1] >= 16 && b[1] <= 31)
            || (b[0] == 192 && b[1] == 168);
    }

    /// <summary>
    /// Primary HTTP handler for the REST client: accept the dev certificate for local hosts (per
    /// request), but require normal validation for everything else.
    /// </summary>
    public static HttpMessageHandler CreateRestHandler() => new HttpClientHandler
    {
        ServerCertificateCustomValidationCallback = (request, _, _, errors) =>
            errors == SslPolicyErrors.None || IsDevHost(request.RequestUri?.Host),
    };

    /// <summary>
    /// SignalR connection configurator: trust the dev certificate on both the negotiate handler and
    /// the WebSocket transport when the API host is local. A no-op for non-dev hosts.
    /// </summary>
    public static void ConfigureSignalR(HttpConnectionOptions options)
    {
        if (!TrustApiHost) return;

        options.HttpMessageHandlerFactory = handler =>
        {
            switch (handler)
            {
                case HttpClientHandler h:
                    h.ServerCertificateCustomValidationCallback =
                        HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
                    break;
                case SocketsHttpHandler s:
                    s.SslOptions.RemoteCertificateValidationCallback = (_, _, _, _) => true;
                    break;
            }
            return handler;
        };

        options.WebSocketConfiguration = ws =>
            ws.RemoteCertificateValidationCallback = (_, _, _, _) => true;
    }
}
