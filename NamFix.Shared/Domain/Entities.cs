using NamFix.Shared.Enums;

namespace NamFix.Shared.Domain;

/// <summary>
/// Domain models mirror the SQL Server tables defined in NamFix.Api/Migrations/up. They are plain POCOs so Dapper
/// can map result sets directly. Keep these free of persistence attributes; the repository layer
/// owns all SQL.
/// </summary>

public class User
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string FullName { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public string PasswordHash { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; }

    /// <summary>Last time the user held an authenticated realtime connection (for presence/last-seen).</summary>
    public DateTime? LastSeenUtc { get; set; }

    // Reliability counters (client side); the provider mirror lives on Provider.
    public int NoShowCount { get; set; }
    public int LateCancellationCount { get; set; }
}

public class RefreshToken
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
    public bool IsActive => RevokedAtUtc is null && DateTime.UtcNow < ExpiresAtUtc;
}

public class Town
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public bool IsActive { get; set; } = true;
}

public class Category
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? IconName { get; set; }
    public bool IsActive { get; set; } = true;
}

public class Tag
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public TagStatus Status { get; set; }
    public Guid? CreatedByUserId { get; set; }
}

public class Provider
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string BusinessName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int PrimaryCategoryId { get; set; }
    public ProviderStatus Status { get; set; }
    public AvailabilityStatus Availability { get; set; }
    public bool IsVerified { get; set; }
    public bool IsEmergencyCallout { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public int? PrimaryTownId { get; set; }
    public decimal? RatingAverage { get; set; }
    public int RatingCount { get; set; }

    // Extended search signals + reliability counters (see migrations 0013/0014).
    public int? YearsExperience { get; set; }
    /// <summary>Denormalized median/avg response time in minutes, powering the response-time badge.</summary>
    public int? AvgResponseMinutes { get; set; }
    /// <summary>Denormalized min active rate-card price, powering the price-range filter.</summary>
    public decimal? StartingPrice { get; set; }
    public int NoShowCount { get; set; }
    public int LateCancellationCount { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

public class Review
{
    public Guid Id { get; set; }
    public Guid ProviderId { get; set; }
    public Guid ClientUserId { get; set; }
    public Guid? JobRequestId { get; set; }
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public bool IsVerified { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

/// <summary>
/// A platform-processed payment. Commission is captured here: the platform deducts
/// <see cref="CommissionAmount"/> and pays the provider <see cref="NetPayoutAmount"/>.
/// </summary>
public class Transaction
{
    public Guid Id { get; set; }
    public Guid ProviderId { get; set; }
    public Guid ClientUserId { get; set; }
    public int? CategoryId { get; set; }
    public decimal GrossAmount { get; set; }
    public decimal CommissionRate { get; set; }
    public decimal CommissionAmount { get; set; }
    public decimal NetPayoutAmount { get; set; }
    public TransactionStatus Status { get; set; }
    public string Currency { get; set; } = "NAD";
    public string? PaymentReference { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? HeldAtUtc { get; set; }
    public DateTime? PaidOutAtUtc { get; set; }
}

/// <summary>
/// Configurable commission rate. A single platform default row plus optional per-category /
/// per-provider overrides. Resolution picks the most specific active rule.
/// </summary>
public class CommissionRule
{
    public int Id { get; set; }
    public CommissionScope Scope { get; set; }
    public int? CategoryId { get; set; }
    public Guid? ProviderId { get; set; }
    public decimal Rate { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>
/// The single job entity spanning matching, quoting, booking and payment. A client posts it as a
/// direct request to one provider or a broadcast to many (<see cref="TargetMode"/>); matching
/// providers respond via <see cref="JobRequestResponse"/> until the client accepts one, which sets
/// <see cref="ProviderId"/>. The time is then bounced back and forth (<see cref="Status"/> +
/// <see cref="ProposedByUserId"/> track whose turn it is to approve) until agreed
/// (<see cref="ConfirmedStartUtc"/>). The provider completes it with an invoice; the client pays that
/// exact amount, producing the linked <see cref="TransactionId"/>. <see cref="ProviderUserId"/> is
/// denormalized from the provider's owning user for cheap authorization and notification targeting.
/// Both provider fields are null while a broadcast job is still gathering quotes.
/// </summary>
public class JobRequest
{
    public Guid Id { get; set; }
    public Guid? ProviderId { get; set; }
    public Guid? ProviderUserId { get; set; }
    public Guid ClientUserId { get; set; }
    public int? CategoryId { get; set; }
    public int? TownId { get; set; }
    public string ServiceDescription { get; set; } = string.Empty;
    public JobStatus Status { get; set; }
    public JobTargetMode TargetMode { get; set; }
    public JobUrgency Urgency { get; set; }

    /// <summary>The time currently on the table (awaiting the other party's approval).</summary>
    public DateTime? ProposedStartUtc { get; set; }
    /// <summary>Who proposed <see cref="ProposedStartUtc"/> — the other party is the one who approves.</summary>
    public Guid? ProposedByUserId { get; set; }
    /// <summary>Set once both parties agree on a time.</summary>
    public DateTime? ConfirmedStartUtc { get; set; }
    public DateTime? ConfirmedEndUtc { get; set; }
    public int? DurationMinutes { get; set; }

    /// <summary>Optional expiry on the quote-gathering window for a posted job.</summary>
    public DateTime? QuoteExpiresUtc { get; set; }

    public string? LocationText { get; set; }
    public double? LocationLat { get; set; }
    public double? LocationLng { get; set; }

    public decimal? InvoiceAmount { get; set; }
    public string? InvoiceNotes { get; set; }
    public string Currency { get; set; } = "NAD";

    /// <summary>The platform transaction created when the client pays the invoice.</summary>
    public Guid? TransactionId { get; set; }

    // Cancellation / no-show accounting.
    public Guid? CancelledByUserId { get; set; }
    public DateTime? CancelledAtUtc { get; set; }
    public bool WasLateCancellation { get; set; }
    public Guid? NoShowByUserId { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

/// <summary>A single provider's response to a job request: invitation, interest, or a quote.</summary>
public class JobRequestResponse
{
    public Guid Id { get; set; }
    public Guid JobRequestId { get; set; }
    public Guid ProviderId { get; set; }
    public Guid ProviderUserId { get; set; }
    public JobResponseStatus Status { get; set; }
    public decimal? QuoteAmount { get; set; }
    public string? QuoteNote { get; set; }
    public DateTime? QuoteExpiresUtc { get; set; }
    public DateTime? ProposedStartUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? RespondedAtUtc { get; set; }
}

/// <summary>A chat message within a job thread, authored by either participant.</summary>
public class JobRequestMessage
{
    public Guid Id { get; set; }
    public Guid JobRequestId { get; set; }
    public Guid SenderUserId { get; set; }
    public string Body { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
}

/// <summary>An uploaded file attached to a job — the provider's invoice or a client's job photo.</summary>
public class JobRequestAttachment
{
    public Guid Id { get; set; }
    public Guid JobRequestId { get; set; }
    public Guid UploadedByUserId { get; set; }
    public AttachmentKind Kind { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/octet-stream";
    public byte[] Content { get; set; } = Array.Empty<byte>();
    public DateTime CreatedAtUtc { get; set; }
}

/// <summary>A recurring weekly availability window for a provider (local time-of-day).</summary>
public class ProviderAvailabilityRule
{
    public Guid Id { get; set; }
    public Guid ProviderId { get; set; }
    public DayOfWeek DayOfWeek { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
}

/// <summary>A one-off blocked date range when a provider is unavailable.</summary>
public class ProviderTimeOff
{
    public Guid Id { get; set; }
    public Guid ProviderId { get; set; }
    public DateTime StartUtc { get; set; }
    public DateTime EndUtc { get; set; }
    public string? Reason { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

/// <summary>A published price-list line on a provider's profile; seeds the quote form.</summary>
public class ProviderRateCard
{
    public Guid Id { get; set; }
    public Guid ProviderId { get; set; }
    public int? CategoryId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public RateUnit Unit { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

/// <summary>An admin-editable platform tunable (key/value).</summary>
public class PlatformSetting
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public DateTime UpdatedAtUtc { get; set; }
}

/// <summary>An in-app notification for a single recipient, tied to a job or a support ticket.</summary>
public class Notification
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid? JobRequestId { get; set; }
    public Guid? TicketId { get; set; }
    public NotificationType Type { get; set; }
    public string Message { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

/// <summary>
/// A support/helpdesk request raised by a user. The requester and admins hold a threaded
/// conversation (<see cref="SupportMessage"/>) with optional file attachments until it is resolved.
/// <see cref="Status"/> tracks the lifecycle; <see cref="LastMessageAtUtc"/> orders the admin queue.
/// </summary>
public class SupportTicket
{
    public Guid Id { get; set; }
    public Guid RequesterUserId { get; set; }
    public string Subject { get; set; } = string.Empty;
    public SupportCategory Category { get; set; }
    public SupportPriority Priority { get; set; }
    public TicketStatus Status { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public DateTime LastMessageAtUtc { get; set; }
    public DateTime? ClosedAtUtc { get; set; }
}

/// <summary>A message within a support ticket thread. <see cref="IsSystem"/> flags auto-generated notices.</summary>
public class SupportMessage
{
    public Guid Id { get; set; }
    public Guid TicketId { get; set; }
    public Guid SenderUserId { get; set; }
    public string Body { get; set; } = string.Empty;
    public bool IsSystem { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

/// <summary>A file attached to a support ticket, optionally tied to a specific message in the thread.</summary>
public class SupportAttachment
{
    public Guid Id { get; set; }
    public Guid TicketId { get; set; }
    public Guid? MessageId { get; set; }
    public Guid UploadedByUserId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/octet-stream";
    public byte[] Content { get; set; } = Array.Empty<byte>();
    public long SizeBytes { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

/// <summary>
/// A single-use password-reset token emailed to a user who forgot their password. Expires after a
/// configured window; <see cref="UsedAtUtc"/> is stamped once redeemed so it cannot be replayed.
/// </summary>
public class PasswordResetToken
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UsedAtUtc { get; set; }
    public bool IsRedeemable => UsedAtUtc is null && DateTime.UtcNow < ExpiresAtUtc;
}

/// <summary>A user's opt-in/out state for one grouped email-notification category. Absent rows default
/// to subscribed. Keyed by (<see cref="UserId"/>, <see cref="Category"/>).</summary>
public class UserEmailPreference
{
    public Guid UserId { get; set; }
    public EmailNotificationCategory Category { get; set; }
    public bool IsSubscribed { get; set; } = true;
}

/// <summary>An email fetched from the configured mailbox over POP3 and stored for the admin inbox.
/// <see cref="MessageId"/> is the RFC message-id, used to dedupe repeated fetches.</summary>
public class InboxMessage
{
    public Guid Id { get; set; }
    public string MessageId { get; set; } = string.Empty;
    public string FromName { get; set; } = string.Empty;
    public string FromAddress { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public DateTime ReceivedAtUtc { get; set; }
    public string? TextBody { get; set; }
    public string? HtmlBody { get; set; }
    public DateTime FetchedAtUtc { get; set; }
}
