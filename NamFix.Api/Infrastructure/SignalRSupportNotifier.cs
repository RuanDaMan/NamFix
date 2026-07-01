using Microsoft.AspNetCore.SignalR;
using NamFix.Shared.Contracts;
using NamFix.Shared.Dtos;

namespace NamFix.Api.Infrastructure;

/// <summary>
/// SignalR-backed <see cref="ISupportRealtimeNotifier"/>. Translates the Application layer's realtime
/// intents into pushes over <see cref="NotificationHub"/> (the single authenticated hub), targeting the
/// per-user groups. Server → client methods: <c>TicketChanged(Guid ticketId)</c>,
/// <c>SupportMessagePosted(Guid ticketId, SupportMessageDto message)</c>, and the shared
/// <c>Notification(NotificationDto)</c>.
/// </summary>
public sealed class SignalRSupportNotifier : ISupportRealtimeNotifier
{
    private readonly IHubContext<NotificationHub> _hub;
    public SignalRSupportNotifier(IHubContext<NotificationHub> hub) => _hub = hub;

    public Task TicketChangedAsync(Guid ticketId, IEnumerable<Guid> participantUserIds) =>
        _hub.Clients.Groups(Groups(participantUserIds)).SendAsync("TicketChanged", ticketId);

    public Task SupportMessagePostedAsync(Guid ticketId, IEnumerable<Guid> participantUserIds, SupportMessageDto message) =>
        _hub.Clients.Groups(Groups(participantUserIds)).SendAsync("SupportMessagePosted", ticketId, message);

    public Task NotificationAsync(Guid recipientUserId, NotificationDto notification) =>
        _hub.Clients.Group(NotificationHub.GroupFor(recipientUserId)).SendAsync("Notification", notification);

    private static IReadOnlyList<string> Groups(IEnumerable<Guid> userIds) =>
        userIds.Distinct().Select(NotificationHub.GroupFor).ToList();
}
