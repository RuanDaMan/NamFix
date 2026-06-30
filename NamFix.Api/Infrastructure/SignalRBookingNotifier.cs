using Microsoft.AspNetCore.SignalR;
using NamFix.Shared.Contracts;
using NamFix.Shared.Dtos;

namespace NamFix.Api.Infrastructure;

/// <summary>
/// SignalR-backed <see cref="IBookingRealtimeNotifier"/>. Translates the Application layer's
/// realtime intents into pushes over <see cref="BookingHub"/>, targeting the per-user groups.
/// </summary>
public sealed class SignalRBookingNotifier : IBookingRealtimeNotifier
{
    private readonly IHubContext<BookingHub> _hub;
    public SignalRBookingNotifier(IHubContext<BookingHub> hub) => _hub = hub;

    public Task BookingChangedAsync(Guid bookingId, IEnumerable<Guid> participantUserIds) =>
        _hub.Clients.Groups(Groups(participantUserIds)).SendAsync("BookingChanged", bookingId);

    public Task MessagePostedAsync(Guid bookingId, IEnumerable<Guid> participantUserIds, BookingMessageDto message) =>
        _hub.Clients.Groups(Groups(participantUserIds)).SendAsync("MessagePosted", bookingId, message);

    public Task NotificationAsync(Guid recipientUserId, NotificationDto notification) =>
        _hub.Clients.Group(BookingHub.GroupFor(recipientUserId)).SendAsync("Notification", notification);

    private static IReadOnlyList<string> Groups(IEnumerable<Guid> userIds) =>
        userIds.Distinct().Select(BookingHub.GroupFor).ToList();
}
