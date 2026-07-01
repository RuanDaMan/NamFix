namespace NamFix.Shared.Contracts;

/// <summary>
/// Tracks which users currently hold a live authenticated realtime connection ("who is online").
/// Updated by the realtime hub on connect/disconnect; read by the admin User Management page.
/// Kept in Shared.Contracts so the Application layer never references SignalR directly — the host
/// (NamFix.Api) supplies the in-memory implementation.
/// </summary>
public interface IPresenceTracker
{
    /// <summary>Record that a connection for the user opened (increments its connection count).</summary>
    Task UserConnectedAsync(Guid userId);

    /// <summary>Record that a connection for the user closed (decrements its connection count).</summary>
    Task UserDisconnectedAsync(Guid userId);

    /// <summary>True when the user has at least one live connection.</summary>
    bool IsOnline(Guid userId);

    /// <summary>The set of users currently online.</summary>
    IReadOnlyCollection<Guid> OnlineUserIds { get; }
}
