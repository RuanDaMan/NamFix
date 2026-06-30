using System.ComponentModel.DataAnnotations;
using NamFix.Shared.Enums;

namespace NamFix.Shared.Dtos;

/// <summary>Client requests a booking with a provider, proposing a first time.</summary>
public record CreateBookingRequest
{
    [Required]
    public Guid ProviderId { get; init; }

    [Required, MinLength(3)]
    public string ServiceDescription { get; init; } = string.Empty;

    [Required]
    public DateTime RequestedStartUtc { get; init; }
}

/// <summary>Either party proposes a (new) time, bouncing approval back to the other side.</summary>
public record ProposeTimeRequest
{
    [Required]
    public DateTime ProposedStartUtc { get; init; }
}

/// <summary>Client shares the job location (free-text plus optional coordinates).</summary>
public record SetBookingLocationRequest
{
    [Required, MinLength(2)]
    public string LocationText { get; init; } = string.Empty;
    public double? Lat { get; init; }
    public double? Lng { get; init; }
}

/// <summary>Provider marks the job done and sets the fee to charge (the invoice).</summary>
public record CompleteBookingRequest
{
    [Range(0.01, double.MaxValue)]
    public decimal InvoiceAmount { get; init; }
    public string? InvoiceNotes { get; init; }
}

/// <summary>A booking in full, with display names and a flag for whether an invoice file is attached.</summary>
public record BookingDto
{
    public Guid Id { get; init; }
    public Guid ProviderId { get; init; }
    public Guid ProviderUserId { get; init; }
    public string ProviderBusinessName { get; init; } = string.Empty;
    public Guid ClientUserId { get; init; }
    public string ClientName { get; init; } = string.Empty;
    public int? CategoryId { get; init; }
    public string ServiceDescription { get; init; } = string.Empty;
    public BookingStatus Status { get; init; }

    public DateTime? ProposedStartUtc { get; init; }
    public Guid? ProposedByUserId { get; init; }
    public DateTime? ConfirmedStartUtc { get; init; }

    public string? LocationText { get; init; }
    public double? LocationLat { get; init; }
    public double? LocationLng { get; init; }

    public decimal? InvoiceAmount { get; init; }
    public string? InvoiceNotes { get; init; }
    public bool HasInvoiceFile { get; init; }
    public string Currency { get; init; } = "NAD";

    public Guid? TransactionId { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime UpdatedAtUtc { get; init; }
}

public record SendBookingMessageRequest
{
    [Required, MinLength(1), MaxLength(2000)]
    public string Body { get; init; } = string.Empty;
}

public record BookingMessageDto
{
    public Guid Id { get; init; }
    public Guid BookingId { get; init; }
    public Guid SenderUserId { get; init; }
    public string SenderName { get; init; } = string.Empty;
    public string Body { get; init; } = string.Empty;
    public DateTime CreatedAtUtc { get; init; }
}
