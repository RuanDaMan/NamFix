using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using NamFix.Application.Data.Repositories;
using NamFix.Shared.Contracts;

namespace NamFix.Api.Infrastructure;

/// <summary>
/// Authenticated hub that delivers live job + support updates, chat messages and notifications. Each
/// connection joins a group keyed by the authenticated user's id, so the server can push to a specific
/// person (notifications) or to the participants of a job/ticket (updates/messages). Connect/disconnect
/// also drives presence (<see cref="IPresenceTracker"/>) and stamps the user's LastSeenUtc, powering
/// the admin "who is online" / last-seen view.
///
/// Server → client methods: <c>JobChanged</c>, <c>MessagePosted</c>, <c>TicketChanged</c>,
/// <c>SupportMessagePosted</c>, <c>Notification</c>.
/// </summary>
[Authorize]
public sealed class NotificationHub : Hub
{
    private readonly IPresenceTracker _presence;
    private readonly IUserRepository _users;

    public NotificationHub(IPresenceTracker presence, IUserRepository users)
    {
        _presence = presence;
        _users = users;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = GetUserId(Context.User);
        if (userId is not null)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, GroupFor(userId.Value));
            await _presence.UserConnectedAsync(userId.Value);
            await _users.UpdateLastSeenAsync(userId.Value, DateTime.UtcNow);
        }
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = GetUserId(Context.User);
        if (userId is not null)
        {
            await _presence.UserDisconnectedAsync(userId.Value);
            await _users.UpdateLastSeenAsync(userId.Value, DateTime.UtcNow);
        }
        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>Group name a user's connections are placed in, for targeted server pushes.</summary>
    public static string GroupFor(Guid userId) => $"user:{userId}";

    private static Guid? GetUserId(ClaimsPrincipal? user)
    {
        var raw = user?.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? user?.FindFirstValue(JwtRegisteredClaimNames.Sub);
        return Guid.TryParse(raw, out var id) ? id : null;
    }
}
