using Microsoft.AspNetCore.SignalR;
using NamFix.Shared.Contracts;
using NamFix.Shared.Dtos;

namespace NamFix.Api.Infrastructure;

/// <summary>
/// SignalR-backed <see cref="IJobRealtimeNotifier"/>. Translates the Application layer's realtime
/// intents into pushes over <see cref="NotificationHub"/>, targeting the per-user groups.
/// </summary>
public sealed class SignalRJobNotifier : IJobRealtimeNotifier
{
    private readonly IHubContext<NotificationHub> _hub;
    public SignalRJobNotifier(IHubContext<NotificationHub> hub) => _hub = hub;

    public Task JobChangedAsync(Guid jobRequestId, IEnumerable<Guid> participantUserIds) =>
        _hub.Clients.Groups(Groups(participantUserIds)).SendAsync("JobChanged", jobRequestId);

    public Task MessagePostedAsync(Guid jobRequestId, IEnumerable<Guid> participantUserIds, JobMessageDto message) =>
        _hub.Clients.Groups(Groups(participantUserIds)).SendAsync("MessagePosted", jobRequestId, message);

    public Task NotificationAsync(Guid recipientUserId, NotificationDto notification) =>
        _hub.Clients.Group(NotificationHub.GroupFor(recipientUserId)).SendAsync("Notification", notification);

    private static IReadOnlyList<string> Groups(IEnumerable<Guid> userIds) =>
        userIds.Distinct().Select(NotificationHub.GroupFor).ToList();
}
