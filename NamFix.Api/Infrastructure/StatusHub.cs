using Microsoft.AspNetCore.SignalR;

namespace NamFix.Api.Infrastructure;

/// <summary>
/// Lightweight presence hub used only as a backend heartbeat. The client keeps a connection open;
/// a live connection means "API online". No methods are required — connect/disconnect is the signal.
/// </summary>
public sealed class StatusHub : Hub
{
}
