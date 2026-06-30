using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NamFix.Application.Data.Repositories;
using NamFix.Shared.Dtos;

namespace NamFix.Api.Controllers;

[Authorize]
public sealed class NotificationsController : ApiControllerBase
{
    private readonly INotificationRepository _notifications;
    public NotificationsController(INotificationRepository notifications) => _notifications = notifications;

    /// <summary>The signed-in user's most recent notifications (newest first).</summary>
    [HttpGet]
    public async Task<ActionResult<List<NotificationDto>>> Mine() =>
        Ok(await _notifications.ListForUserAsync(CurrentUserId));

    [HttpPost("{id:guid}/read")]
    public async Task<ActionResult> MarkRead(Guid id)
    {
        await _notifications.MarkReadAsync(id, CurrentUserId);
        return NoContent();
    }

    [HttpPost("read-all")]
    public async Task<ActionResult> MarkAllRead()
    {
        await _notifications.MarkAllReadAsync(CurrentUserId);
        return NoContent();
    }
}
