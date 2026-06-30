using NamFix.Shared.Dtos;

namespace NamFix.Shared.Contracts;

/// <summary>
/// Pushes live booking updates to connected clients. The Application layer raises these after it
/// persists a change; the host (NamFix.Api) supplies a SignalR-backed implementation. Kept in
/// Shared.Contracts so the service layer never references SignalR directly. A no-op implementation
/// is fine for hosts without realtime (e.g. tests).
/// </summary>
public interface IBookingRealtimeNotifier
{
    /// <summary>Tell both participants a booking's state changed so their open views refresh.</summary>
    Task BookingChangedAsync(Guid bookingId, IEnumerable<Guid> participantUserIds);

    /// <summary>Push a newly posted chat message to both participants.</summary>
    Task MessagePostedAsync(Guid bookingId, IEnumerable<Guid> participantUserIds, BookingMessageDto message);

    /// <summary>Push a notification to a single recipient (drives the nav bell live).</summary>
    Task NotificationAsync(Guid recipientUserId, NotificationDto notification);
}
