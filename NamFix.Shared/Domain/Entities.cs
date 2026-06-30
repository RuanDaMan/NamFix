using NamFix.Shared.Enums;

namespace NamFix.Shared.Domain;

/// <summary>
/// Domain models mirror the SQL Server tables defined in db/up. They are plain POCOs so Dapper
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
