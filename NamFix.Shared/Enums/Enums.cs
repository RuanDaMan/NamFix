namespace NamFix.Shared.Enums;

/// <summary>Role chosen at registration. Drives authorization across the platform.</summary>
public enum UserRole
{
    Client = 1,
    ServiceProvider = 2,
    Admin = 3
}

/// <summary>Lifecycle of a provider listing. Admins approve/suspend providers.</summary>
public enum ProviderStatus
{
    PendingApproval = 0,
    Active = 1,
    Suspended = 2,
    Rejected = 3
}

/// <summary>Whether a provider is currently taking work. Powers the "available now" filter.</summary>
public enum AvailabilityStatus
{
    Available = 1,
    Busy = 2,
    Offline = 3
}

/// <summary>Moderation state for self-tagged service tags. Free-text tags queue for admin approval.</summary>
public enum TagStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2
}

/// <summary>
/// Lifecycle of a platform transaction. The "hold then release/payout" pattern lets the
/// platform reliably capture commission before releasing the provider's net payout.
/// </summary>
public enum TransactionStatus
{
    Pending = 0,
    Held = 1,
    PaidOut = 2,
    Refunded = 3,
    Failed = 4
}

/// <summary>Scope at which a commission-rate override applies. Most specific wins.</summary>
public enum CommissionScope
{
    Platform = 0,
    Category = 1,
    Provider = 2
}
