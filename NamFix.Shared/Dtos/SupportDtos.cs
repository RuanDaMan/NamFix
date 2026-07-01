using System.ComponentModel.DataAnnotations;
using NamFix.Shared.Enums;

namespace NamFix.Shared.Dtos;

/// <summary>A user opens a support ticket with a subject, category, priority and first message.</summary>
public record CreateTicketRequest
{
    [Required, MinLength(3), MaxLength(200)]
    public string Subject { get; init; } = string.Empty;

    public SupportCategory Category { get; init; } = SupportCategory.General;
    public SupportPriority Priority { get; init; } = SupportPriority.Normal;

    [Required, MinLength(3), MaxLength(4000)]
    public string Body { get; init; } = string.Empty;
}

/// <summary>A new message in a ticket thread (a reply from the requester or an admin).</summary>
public record PostSupportMessageRequest
{
    [Required, MinLength(1), MaxLength(4000)]
    public string Body { get; init; } = string.Empty;
}

/// <summary>Admin updates a ticket's status and/or priority.</summary>
public record UpdateTicketRequest
{
    public TicketStatus? Status { get; init; }
    public SupportPriority? Priority { get; init; }
}

/// <summary>A support ticket in list/detail form, with the requester's display details and counts.</summary>
public record SupportTicketDto
{
    public Guid Id { get; init; }
    public Guid RequesterUserId { get; init; }
    public string RequesterName { get; init; } = string.Empty;
    public string RequesterEmail { get; init; } = string.Empty;
    public string Subject { get; init; } = string.Empty;
    public SupportCategory Category { get; init; }
    public SupportPriority Priority { get; init; }
    public TicketStatus Status { get; init; }
    public int MessageCount { get; init; }
    public bool HasAttachments { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime UpdatedAtUtc { get; init; }
    public DateTime LastMessageAtUtc { get; init; }
    public DateTime? ClosedAtUtc { get; init; }

    /// <summary>Set only on the create response, so the client can attach the opening message's files.</summary>
    public Guid? OpeningMessageId { get; init; }
}

/// <summary>A message within a ticket thread, with its attachments and the sender's display name.</summary>
public record SupportMessageDto
{
    public Guid Id { get; init; }
    public Guid TicketId { get; init; }
    public Guid SenderUserId { get; init; }
    public string SenderName { get; init; } = string.Empty;
    public bool SenderIsAdmin { get; init; }
    public bool IsSystem { get; init; }
    public string Body { get; init; } = string.Empty;
    public DateTime CreatedAtUtc { get; init; }
    public List<SupportAttachmentDto> Attachments { get; init; } = new();
}

/// <summary>Metadata for a file attached to a ticket (the bytes are fetched via the download endpoint).</summary>
public record SupportAttachmentDto
{
    public Guid Id { get; init; }
    public Guid TicketId { get; init; }
    public Guid? MessageId { get; init; }
    public string FileName { get; init; } = string.Empty;
    public string ContentType { get; init; } = "application/octet-stream";
    public long SizeBytes { get; init; }
}
