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
/// Lifecycle of a job request — the single entity that spans matching, quoting, booking and payment.
///
/// <para><b>Pre-provider (matching/quoting) phase.</b> A broadcast/"get matched" or multi-target quote
/// job starts <see cref="Requested"/> (no chosen provider); once at least one provider responds with a
/// quote it is <see cref="Quoted"/>. The client accepts one quote to choose a provider.</para>
///
/// <para><b>Time negotiation.</b> A direct request (or an accepted quote without an agreed slot) uses
/// the back-and-forth: the status encodes <b>whose turn it is to approve</b>
/// (<see cref="PendingProvider"/> = the client made the latest proposal and the provider must respond;
/// <see cref="PendingClient"/> = the provider proposed and the client must respond). Once a time is
/// agreed it becomes <see cref="Scheduled"/>.</para>
///
/// <para><b>Delivery.</b> The provider marks it <see cref="InProgress"/> when work starts, then
/// <see cref="Completed"/> with an invoice amount; the client pays to reach <see cref="Paid"/> and may
/// leave a review to reach <see cref="Reviewed"/>. Payment can only ever be made against a
/// <see cref="Completed"/> job — never an arbitrary amount.</para>
///
/// <para>Terminal off-ramps: <see cref="Cancelled"/> (either party), <see cref="Declined"/> (provider),
/// <see cref="NoShow"/> (a party did not show). Stored int values 0-6 are unchanged from the original
/// BookingStatus; new states are appended so existing rows stay valid.</para>
/// </summary>
public enum JobStatus
{
    PendingProvider = 0,
    PendingClient = 1,
    Scheduled = 2,
    Completed = 3,
    Paid = 4,
    Cancelled = 5,
    Declined = 6,

    // Appended (stored ints stay stable):
    Requested = 7,
    Quoted = 8,
    InProgress = 9,
    Reviewed = 10,
    NoShow = 11
}

/// <summary>Whether a job targets one chosen provider (direct quote/booking) or is broadcast to many
/// matching providers ("get matched" / urgent).</summary>
public enum JobTargetMode
{
    Direct = 0,
    Broadcast = 1
}

/// <summary>How urgent a job is. Urgent jobs are broadcast to emergency-flagged, available providers.</summary>
public enum JobUrgency
{
    Normal = 0,
    Urgent = 1
}

/// <summary>
/// State of a single provider's response to a job request (one row per invited provider). Starts
/// <see cref="Invited"/> at broadcast/fan-out; the provider moves it to <see cref="Interested"/> or
/// <see cref="Quoted"/> (or <see cref="Declined"/>/<see cref="Withdrawn"/>). When the client picks a
/// quote that provider's row becomes <see cref="Accepted"/> and the rest <see cref="Rejected"/>.
/// </summary>
public enum JobResponseStatus
{
    Invited = 0,
    Viewed = 1,
    Interested = 2,
    Quoted = 3,
    Declined = 4,
    Withdrawn = 5,
    Accepted = 6,
    Rejected = 7
}

/// <summary>Pricing unit for a rate-card line.</summary>
public enum RateUnit
{
    PerJob = 0,
    PerHour = 1,
    Callout = 2,
    PerUnit = 3
}

/// <summary>Kind of file attached to a job. Invoice is single (replaced); job photos allow multiple.</summary>
public enum AttachmentKind
{
    Invoice = 0,
    JobPhoto = 1
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
    LocationShared = 8,

    // Support/helpdesk (appended so existing stored ints stay stable).
    SupportTicketCreated = 9,
    SupportReply = 10,
    SupportStatusChanged = 11,
    SupportResolved = 12,

    // Job matching / quoting (appended).
    JobPosted = 13,
    QuoteReceived = 14,
    QuoteAccepted = 15,
    QuoteDeclined = 16,
    JobStarted = 17,
    NoShowFlagged = 18,
    ReviewRequested = 19,
    UrgentJobBroadcast = 20
}

/// <summary>
/// Grouped email-notification categories a user can unsubscribe from. Every <see cref="NotificationType"/>
/// maps to exactly one of these (see the map in <c>NotificationDispatcher</c>). <see cref="AccountSecurity"/>
/// (password reset and similar) is transactional and is <b>always</b> sent — it is not user-toggleable.
/// Stored as INT per the DB convention.
/// </summary>
public enum EmailNotificationCategory
{
    JobUpdates = 0,
    Messages = 1,
    Quotes = 2,
    Support = 3,
    AccountSecurity = 4
}

/// <summary>Broad subject area a support ticket falls under, chosen by the requester.</summary>
public enum SupportCategory
{
    General = 0,
    Booking = 1,
    Payment = 2,
    Account = 3,
    Bug = 4,
    Other = 5
}

/// <summary>How urgent a support ticket is. Drives ordering/triage in the admin queue.</summary>
public enum SupportPriority
{
    Low = 0,
    Normal = 1,
    High = 2,
    Urgent = 3
}

/// <summary>
/// Lifecycle of a support ticket. <see cref="Open"/> = needs an admin; <see cref="AwaitingUser"/> =
/// admin replied and is waiting on the requester; <see cref="Resolved"/> = marked solved (the
/// requester can reply to reopen); <see cref="Closed"/> = finalised.
/// </summary>
public enum TicketStatus
{
    Open = 0,
    AwaitingUser = 1,
    Resolved = 2,
    Closed = 3
}
