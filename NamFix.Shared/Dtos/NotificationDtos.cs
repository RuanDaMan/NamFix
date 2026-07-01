using NamFix.Shared.Enums;

namespace NamFix.Shared.Dtos;

/// <summary>An in-app notification surfaced in the nav bell dropdown.</summary>
public record NotificationDto
{
    public Guid Id { get; init; }
    public Guid? JobRequestId { get; init; }
    public Guid? TicketId { get; init; }
    public NotificationType Type { get; init; }
    public string Message { get; init; } = string.Empty;
    public bool IsRead { get; init; }
    public DateTime CreatedAtUtc { get; init; }
}
