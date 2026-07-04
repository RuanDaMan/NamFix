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

/// <summary>One selectable email type on the admin "send test emails" tool. <see cref="Key"/> is the
/// stable identifier passed back in <see cref="SendTestEmailsRequest"/>; <see cref="Group"/> is used
/// only to bucket the checkboxes in the UI.</summary>
public sealed record TestEmailTypeDto
{
    public string Key { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public string Group { get; init; } = string.Empty;
}

/// <summary>Admin request to send a dummy sample of each selected email type to one address.</summary>
public sealed record SendTestEmailsRequest
{
    [Required, EmailAddress]
    public string ToEmail { get; set; } = string.Empty;

    /// <summary>The <see cref="TestEmailTypeDto.Key"/> values the admin ticked.</summary>
    public List<string> Keys { get; set; } = new();
}

/// <summary>Outcome of a test-email send: how many went out and which ones (labels) failed.</summary>
public sealed record SendTestEmailsResultDto
{
    public int Sent { get; init; }
    public List<string> Failed { get; init; } = new();
}
