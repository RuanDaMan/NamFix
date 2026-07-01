using System.ComponentModel.DataAnnotations;
using NamFix.Shared.Enums;

namespace NamFix.Shared.Dtos;

/// <summary>A published rate-card line shown on a provider profile and used to seed quotes.</summary>
public record RateCardDto
{
    public Guid Id { get; init; }
    public Guid ProviderId { get; init; }
    public int? CategoryId { get; init; }
    public string? CategoryName { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public decimal Price { get; init; }
    public RateUnit Unit { get; init; }
    public bool IsActive { get; init; }
    public int SortOrder { get; init; }
}

/// <summary>Create/update payload for a provider managing a rate-card line (bound by EditForm).</summary>
public record SaveRateCardRequest
{
    public Guid? Id { get; set; }
    public int? CategoryId { get; set; }

    [Required]
    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    [Range(0.01, double.MaxValue)]
    public decimal Price { get; set; }

    public RateUnit Unit { get; set; } = RateUnit.PerJob;
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
}
