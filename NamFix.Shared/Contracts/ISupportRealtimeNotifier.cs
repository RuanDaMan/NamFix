using NamFix.Shared.Dtos;

namespace NamFix.Shared.Contracts;

/// <summary>
/// Pushes live support-ticket updates to connected clients. The Application layer raises these after
/// it persists a change; the host (NamFix.Api) supplies a SignalR-backed implementation. Kept in
/// Shared.Contracts so the service layer never references SignalR directly. A no-op implementation
/// is fine for hosts without realtime (e.g. tests).
/// </summary>
public interface ISupportRealtimeNotifier
{
    /// <summary>Tell the ticket's participants its state changed so their open views refresh.</summary>
    Task TicketChangedAsync(Guid ticketId, IEnumerable<Guid> participantUserIds);

    /// <summary>Push a newly posted ticket message to the ticket's participants.</summary>
    Task SupportMessagePostedAsync(Guid ticketId, IEnumerable<Guid> participantUserIds, SupportMessageDto message);

    /// <summary>Push a notification to a single recipient (drives the nav bell live).</summary>
    Task NotificationAsync(Guid recipientUserId, NotificationDto notification);
}
