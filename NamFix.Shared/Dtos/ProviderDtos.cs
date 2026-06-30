using System.ComponentModel.DataAnnotations;
using NamFix.Shared.Enums;

namespace NamFix.Shared.Dtos;

/// <summary>Full provider profile returned to clients viewing a listing.</summary>
public record ProviderDto
{
    public Guid Id { get; init; }
    public Guid UserId { get; init; }
    public string BusinessName { get; init; } = string.Empty;
    public string? Description { get; init; }
    public int PrimaryCategoryId { get; init; }
    public string? PrimaryCategoryName { get; init; }
    public ProviderStatus Status { get; init; }
    public AvailabilityStatus Availability { get; init; }
    public bool IsVerified { get; init; }
    public bool IsEmergencyCallout { get; init; }
    public double? Latitude { get; init; }
    public double? Longitude { get; init; }
    public int? PrimaryTownId { get; init; }
    public string? PrimaryTownName { get; init; }
    public decimal? RatingAverage { get; init; }
    public int RatingCount { get; init; }
    public string? OwnerPhoneNumber { get; init; }
    public List<string> Tags { get; init; } = new();
    public List<int> TownIds { get; init; } = new();
}

/// <summary>Create/update payload for a provider managing their own profile (bound by EditForm).</summary>
public record SaveProviderRequest
{
    [Required]
    public string BusinessName { get; set; } = string.Empty;

    public string? Description { get; set; }

    [Required]
    public int PrimaryCategoryId { get; set; }

    public bool IsEmergencyCallout { get; set; }
    public AvailabilityStatus Availability { get; set; } = AvailabilityStatus.Available;
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public int? PrimaryTownId { get; set; }
    public List<int> TownIds { get; set; } = new();
    public List<string> Tags { get; set; } = new();
}

/// <summary>Compact card shape used in search results to keep payloads data-light.</summary>
public record ProviderSearchResult
{
    public Guid Id { get; init; }
    public string BusinessName { get; init; } = string.Empty;
    public string? PrimaryCategoryName { get; init; }
    public string? PrimaryTownName { get; init; }
    public AvailabilityStatus Availability { get; init; }
    public bool IsVerified { get; init; }
    public bool IsEmergencyCallout { get; init; }
    public decimal? RatingAverage { get; init; }
    public int RatingCount { get; init; }
    public double? Latitude { get; init; }
    public double? Longitude { get; init; }
    public double? DistanceKm { get; init; }
    public List<string> Tags { get; init; } = new();
}

/// <summary>
/// Search/filter criteria. All filters are optional and combine with AND semantics.
/// Mutable so Blazor forms can two-way bind directly to it.
/// </summary>
public record ProviderSearchRequest
{
    /// <summary>Free-text query run through SQL Server full-text search (CONTAINS/FREETEXT).</summary>
    public string? Query { get; set; }
    public int? CategoryId { get; set; }
    public int? TownId { get; set; }
    public List<string>? Tags { get; set; }
    public decimal? MinRating { get; set; }
    public bool? EmergencyOnly { get; set; }
    public bool? AvailableNowOnly { get; set; }

    /// <summary>When supplied, results can be distance-sorted for "near me".</summary>
    public double? NearLatitude { get; set; }
    public double? NearLongitude { get; set; }

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public record PagedResult<T>
{
    public IReadOnlyList<T> Items { get; init; } = Array.Empty<T>();
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}
