using System.ComponentModel.DataAnnotations;

namespace NamFix.Shared.Dtos;

// ---------------------------------------------------------------------------------------------
// Provider availability calendar: recurring weekly windows + one-off blocked ranges + booked slots.
// ---------------------------------------------------------------------------------------------

/// <summary>One recurring weekly availability window (times are the provider's local time-of-day).</summary>
public record AvailabilityRuleDto
{
    public Guid Id { get; init; }
    /// <summary>0=Sunday .. 6=Saturday (matches System.DayOfWeek).</summary>
    [Range(0, 6)]
    public int DayOfWeek { get; init; }
    public TimeSpan StartTime { get; init; }
    public TimeSpan EndTime { get; init; }
}

/// <summary>Full weekly availability, replaced as a set by the provider.</summary>
public record SaveAvailabilityRequest
{
    public List<AvailabilityRuleDto> Rules { get; init; } = new();
}

public record TimeOffDto
{
    public Guid Id { get; init; }
    public DateTime StartUtc { get; init; }
    public DateTime EndUtc { get; init; }
    public string? Reason { get; init; }
}

public record AddTimeOffRequest
{
    [Required]
    public DateTime StartUtc { get; init; }
    [Required]
    public DateTime EndUtc { get; init; }
    public string? Reason { get; init; }
}

/// <summary>A confirmed booking slot occupying the provider's calendar (opaque to non-participants).</summary>
public record BookedSlotDto
{
    public DateTime StartUtc { get; init; }
    public DateTime? EndUtc { get; init; }
}

/// <summary>The full availability picture for a provider: weekly rules, time-off, and booked slots.</summary>
public record ProviderAvailabilityDto
{
    public List<AvailabilityRuleDto> Rules { get; init; } = new();
    public List<TimeOffDto> TimeOff { get; init; } = new();
    public List<BookedSlotDto> BookedSlots { get; init; } = new();
}
