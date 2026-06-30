using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace NamFix.Api.Infrastructure;

/// <summary>
/// Authenticated hub that delivers live booking updates, chat messages and notifications. Each
/// connection joins a group keyed by the authenticated user's id, so the server can push to a
/// specific person (notifications) or to both participants of a booking (updates/messages).
///
/// Server → client methods: <c>BookingChanged(Guid bookingId)</c>, <c>MessagePosted(Guid bookingId,
/// BookingMessageDto message)</c>, <c>Notification(NotificationDto notification)</c>.
/// </summary>
[Authorize]
public sealed class BookingHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        var userId = GetUserId(Context.User);
        if (userId is not null)
            await Groups.AddToGroupAsync(Context.ConnectionId, GroupFor(userId.Value));
        await base.OnConnectedAsync();
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
