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
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

public class Review
{
    public Guid Id { get; set; }
    public Guid ProviderId { get; set; }
    public Guid ClientUserId { get; set; }
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
/// A negotiated job between a client and a provider. The proposed time is bounced back and forth
/// (<see cref="Status"/> + <see cref="ProposedByUserId"/> track whose turn it is to approve) until
/// agreed (<see cref="ConfirmedStartUtc"/>). The provider completes it with an invoice; the client
/// pays that exact amount, producing the linked <see cref="TransactionId"/>.
/// <see cref="ProviderUserId"/> is denormalized from the provider's owning user for cheap
/// authorization and notification targeting.
/// </summary>
public class Booking
{
    public Guid Id { get; set; }
    public Guid ProviderId { get; set; }
    public Guid ProviderUserId { get; set; }
    public Guid ClientUserId { get; set; }
    public int? CategoryId { get; set; }
    public string ServiceDescription { get; set; } = string.Empty;
    public BookingStatus Status { get; set; }

    /// <summary>The time currently on the table (awaiting the other party's approval).</summary>
    public DateTime? ProposedStartUtc { get; set; }
    /// <summary>Who proposed <see cref="ProposedStartUtc"/> — the other party is the one who approves.</summary>
    public Guid? ProposedByUserId { get; set; }
    /// <summary>Set once both parties agree on a time.</summary>
    public DateTime? ConfirmedStartUtc { get; set; }

    public string? LocationText { get; set; }
    public double? LocationLat { get; set; }
    public double? LocationLng { get; set; }

    public decimal? InvoiceAmount { get; set; }
    public string? InvoiceNotes { get; set; }
    public string Currency { get; set; } = "NAD";

    /// <summary>The platform transaction created when the client pays the invoice.</summary>
    public Guid? TransactionId { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

/// <summary>A chat message within a booking thread, authored by either participant.</summary>
public class BookingMessage
{
    public Guid Id { get; set; }
    public Guid BookingId { get; set; }
    public Guid SenderUserId { get; set; }
    public string Body { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
}

/// <summary>An uploaded file attached to a booking — currently the provider's invoice document.</summary>
public class BookingAttachment
{
    public Guid Id { get; set; }
    public Guid BookingId { get; set; }
    public Guid UploadedByUserId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/octet-stream";
    public byte[] Content { get; set; } = Array.Empty<byte>();
    public DateTime CreatedAtUtc { get; set; }
}

/// <summary>An in-app notification for a single recipient, usually tied to a booking event.</summary>
public class Notification
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid? BookingId { get; set; }
    public NotificationType Type { get; set; }
    public string Message { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
