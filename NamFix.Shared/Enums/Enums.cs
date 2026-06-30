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

/// <summary>
/// Lifecycle of a booking — the negotiated job between a client and a provider. A time is proposed
/// and bounced back and forth: the status encodes <b>whose turn it is to approve</b>
/// (<see cref="PendingProvider"/> = the client made the latest proposal and the provider must
/// respond; <see cref="PendingClient"/> = the provider proposed and the client must respond). Once a
/// time is agreed it becomes <see cref="Scheduled"/>; the provider then marks it
/// <see cref="Completed"/> with an invoice amount, and the client pays it to reach <see cref="Paid"/>.
/// Payment can only ever be made against a <see cref="Completed"/> booking — never an arbitrary amount.
/// </summary>
public enum BookingStatus
{
    PendingProvider = 0,
    PendingClient = 1,
    Scheduled = 2,
    Completed = 3,
    Paid = 4,
    Cancelled = 5,
    Declined = 6
}

/// <summary>Kind of notification raised for a user, used to pick an icon/label in the UI.</summary>
public enum NotificationType
{
    BookingRequested = 0,
    TimeProposed = 1,
    BookingScheduled = 2,
    BookingCompleted = 3,
    BookingPaid = 4,
    BookingCancelled = 5,
    BookingDeclined = 6,
    NewMessage = 7,
    LocationShared = 8
}
