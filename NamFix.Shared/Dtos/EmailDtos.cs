using System.ComponentModel.DataAnnotations;
using NamFix.Shared.Enums;

namespace NamFix.Shared.Dtos;

/// <summary>A single email recipient (To/Cc/Bcc). Display name is optional.</summary>
public sealed record EmailRecipientDto
{
    public string Email { get; init; } = string.Empty;
    public string? DisplayName { get; init; }

    public EmailRecipientDto() { }
    public EmailRecipientDto(string email, string? displayName = null)
    {
        Email = email;
        DisplayName = displayName;
    }
}

/// <summary>A file attached to an outgoing email.</summary>
public sealed record AttachmentDto
{
    public string FileName { get; init; } = string.Empty;
    public string ContentType { get; init; } = "application/octet-stream";
    public byte[] Content { get; init; } = Array.Empty<byte>();
}

/// <summary>One row in the admin inbox list (no body — kept light for the list view).</summary>
public sealed record InboxMessageDto
{
    public Guid Id { get; init; }
    public string FromName { get; init; } = string.Empty;
    public string FromAddress { get; init; } = string.Empty;
    public string Subject { get; init; } = string.Empty;
    public DateTime ReceivedAtUtc { get; init; }
    public bool HasHtml { get; init; }
}

/// <summary>A single inbox message including its body, for the admin read pane.</summary>
public sealed record InboxMessageDetailDto
{
    public Guid Id { get; init; }
    public string FromName { get; init; } = string.Empty;
    public string FromAddress { get; init; } = string.Empty;
    public string Subject { get; init; } = string.Empty;
    public DateTime ReceivedAtUtc { get; init; }
    public string? TextBody { get; init; }
    public string? HtmlBody { get; init; }
}

/// <summary>A user's subscription state for one email category.</summary>
public sealed record EmailPreferenceDto
{
    public EmailNotificationCategory Category { get; init; }
    public string Label { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public bool IsSubscribed { get; init; }

    /// <summary>Transactional categories (e.g. account security) that cannot be turned off.</summary>
    public bool Locked { get; init; }
}

/// <summary>Bulk update of the signed-in user's email preferences.</summary>
public sealed record UpdateEmailPreferencesRequest
{
    public List<EmailPreferenceDto> Preferences { get; set; } = new();
}
