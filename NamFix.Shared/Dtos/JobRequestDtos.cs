using System.ComponentModel.DataAnnotations;
using NamFix.Shared.Enums;

namespace NamFix.Shared.Dtos;

// ---------------------------------------------------------------------------------------------
// Job requests: the single entity across matching → quoting → booking → payment → review.
// ---------------------------------------------------------------------------------------------

/// <summary>Client requests a booking with a specific provider, proposing a first time (direct flow).</summary>
public record CreateDirectBookingRequest
{
    [Required]
    public Guid ProviderId { get; init; }

    [Required, MinLength(3)]
    public string ServiceDescription { get; init; } = string.Empty;

    [Required]
    public DateTime RequestedStartUtc { get; init; }
}

/// <summary>
/// Client posts a job to gather quotes: either targeted at chosen providers (<see cref="TargetMode"/>
/// = Direct with <see cref="TargetProviderIds"/>) or broadcast to all matching providers
/// (<see cref="JobTargetMode.Broadcast"/>). Urgent broadcasts additionally require the provider to be
/// emergency-flagged and available. The job is created in <see cref="JobStatus.Requested"/>.
/// </summary>
public record PostJobRequest
{
    [Required, MinLength(3)]
    public string ServiceDescription { get; set; } = string.Empty;

    public int? CategoryId { get; set; }
    public int? TownId { get; set; }
    public JobUrgency Urgency { get; set; } = JobUrgency.Normal;
    public JobTargetMode TargetMode { get; set; } = JobTargetMode.Broadcast;

    /// <summary>When <see cref="TargetMode"/> is Direct, the specific providers to invite.</summary>
    public List<Guid>? TargetProviderIds { get; set; }

    public DateTime? PreferredStartUtc { get; set; }
    public DateTime? QuoteExpiresUtc { get; set; }
}

/// <summary>Either party proposes a (new) time, bouncing approval back to the other side.</summary>
public record ProposeTimeRequest
{
    [Required]
    public DateTime ProposedStartUtc { get; init; }
}

/// <summary>Client shares the job location (free-text plus optional coordinates).</summary>
public record SetJobLocationRequest
{
    [Required, MinLength(2)]
    public string LocationText { get; init; } = string.Empty;
    public double? Lat { get; init; }
    public double? Lng { get; init; }
}

/// <summary>Provider marks the job done and sets the fee to charge (the invoice).</summary>
public record CompleteJobRequest
{
    [Range(0.01, double.MaxValue)]
    public decimal InvoiceAmount { get; init; }
    public string? InvoiceNotes { get; init; }
}

/// <summary>Client leaves a review after paying, advancing the job to Reviewed.</summary>
public record CreateJobReviewRequest
{
    [Range(1, 5)]
    public int Rating { get; init; }
    public string? Comment { get; init; }
}

/// <summary>A provider's quote/interest response to a posted job.</summary>
public record SubmitQuoteRequest
{
    public decimal? Amount { get; init; }
    public string? Note { get; init; }
    public DateTime? ProposedStartUtc { get; init; }
    public DateTime? ExpiresUtc { get; init; }

    /// <summary>When true the provider is only registering interest, not a firm price.</summary>
    public bool InterestOnly { get; init; }
}

/// <summary>A job in full, with display names and flags. Provider fields are null for an unmatched broadcast.</summary>
public record JobRequestDto
{
    public Guid Id { get; init; }
    public Guid? ProviderId { get; init; }
    public Guid? ProviderUserId { get; init; }
    public string? ProviderBusinessName { get; init; }
    public Guid ClientUserId { get; init; }
    public string ClientName { get; init; } = string.Empty;
    public int? CategoryId { get; init; }
    public string? CategoryName { get; init; }
    public int? TownId { get; init; }
    public string? TownName { get; init; }
    public string ServiceDescription { get; init; } = string.Empty;
    public JobStatus Status { get; init; }
    public JobTargetMode TargetMode { get; init; }
    public JobUrgency Urgency { get; init; }

    public DateTime? ProposedStartUtc { get; init; }
    public Guid? ProposedByUserId { get; init; }
    public DateTime? ConfirmedStartUtc { get; init; }
    public DateTime? ConfirmedEndUtc { get; init; }
    public int? DurationMinutes { get; init; }
    public DateTime? QuoteExpiresUtc { get; init; }

    public string? LocationText { get; init; }
    public double? LocationLat { get; init; }
    public double? LocationLng { get; init; }

    public decimal? InvoiceAmount { get; init; }
    public string? InvoiceNotes { get; init; }
    public bool HasInvoiceFile { get; init; }
    public string Currency { get; init; } = "NAD";

    public Guid? TransactionId { get; init; }

    public Guid? CancelledByUserId { get; init; }
    public bool WasLateCancellation { get; init; }
    public Guid? NoShowByUserId { get; init; }

    /// <summary>Number of provider responses (quotes/interest) received — for the client's list badge.</summary>
    public int ResponseCount { get; init; }

    public DateTime CreatedAtUtc { get; init; }
    public DateTime UpdatedAtUtc { get; init; }
}

/// <summary>One provider's response to a job, with provider display fields for the client's quote list.</summary>
public record JobResponseDto
{
    public Guid Id { get; init; }
    public Guid JobRequestId { get; init; }
    public Guid ProviderId { get; init; }
    public Guid ProviderUserId { get; init; }
    public string ProviderBusinessName { get; init; } = string.Empty;
    public decimal? ProviderRatingAverage { get; init; }
    public int ProviderRatingCount { get; init; }
    public int? ProviderAvgResponseMinutes { get; init; }
    public JobResponseStatus Status { get; init; }
    public decimal? QuoteAmount { get; init; }
    public string? QuoteNote { get; init; }
    public DateTime? QuoteExpiresUtc { get; init; }
    public DateTime? ProposedStartUtc { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime? RespondedAtUtc { get; init; }
}

public record SendJobMessageRequest
{
    [Required, MinLength(1), MaxLength(2000)]
    public string Body { get; init; } = string.Empty;
}

public record JobMessageDto
{
    public Guid Id { get; init; }
    public Guid JobRequestId { get; init; }
    public Guid SenderUserId { get; init; }
    public string SenderName { get; init; } = string.Empty;
    public string Body { get; init; } = string.Empty;
    public DateTime CreatedAtUtc { get; init; }
}
