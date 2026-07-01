using NamFix.Shared.Dtos;

namespace NamFix.Shared.Contracts;

/// <summary>
/// Pushes live job updates to connected clients. The Application layer raises these after it persists
/// a change; the host (NamFix.Api) supplies a SignalR-backed implementation. Kept in Shared.Contracts
/// so the service layer never references SignalR directly. A no-op implementation is fine for hosts
/// without realtime (e.g. tests).
/// </summary>
public interface IJobRealtimeNotifier
{
    /// <summary>Tell the participants a job's state changed so their open views refresh.</summary>
    Task JobChangedAsync(Guid jobRequestId, IEnumerable<Guid> participantUserIds);

    /// <summary>Push a newly posted chat message to both participants.</summary>
    Task MessagePostedAsync(Guid jobRequestId, IEnumerable<Guid> participantUserIds, JobMessageDto message);

    /// <summary>Push a notification to a single recipient (drives the nav bell live).</summary>
    Task NotificationAsync(Guid recipientUserId, NotificationDto notification);
}
