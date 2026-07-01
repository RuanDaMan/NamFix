using System.ComponentModel.DataAnnotations;
using NamFix.Shared.Enums;

namespace NamFix.Shared.Dtos;

/// <summary>A user as shown on the admin User Management page, with presence and activity counts.</summary>
public record AdminUserDto
{
    public Guid Id { get; init; }
    public string Email { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public string? PhoneNumber { get; init; }
    public UserRole Role { get; init; }
    public bool IsActive { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime? LastSeenUtc { get; init; }

    /// <summary>True when the user currently holds a live authenticated realtime connection.</summary>
    public bool IsOnline { get; init; }

    public int BookingCount { get; init; }
    public int TicketCount { get; init; }
}

/// <summary>Admin changes a user's role.</summary>
public record UpdateUserRoleRequest
{
    [Required]
    public UserRole Role { get; init; }
}

/// <summary>Admin sets a new password for a user.</summary>
public record ResetPasswordRequest
{
    [Required, MinLength(8), MaxLength(200)]
    public string NewPassword { get; init; } = string.Empty;
}
