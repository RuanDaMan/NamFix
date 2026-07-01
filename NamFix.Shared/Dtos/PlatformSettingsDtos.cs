using System.ComponentModel.DataAnnotations;

namespace NamFix.Shared.Dtos;

/// <summary>Admin-editable platform settings surfaced/updated via the admin console.</summary>
public record PlatformSettingsDto
{
    /// <summary>Hours before the confirmed start within which a cancellation counts as "late".</summary>
    [Range(0, 168)]
    public int FreeCancellationWindowHours { get; init; } = 24;
}
