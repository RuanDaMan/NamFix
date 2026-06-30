namespace NamFix.Shared.Dtos;

public record TownDto
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Region { get; init; } = string.Empty;
    public double Latitude { get; init; }
    public double Longitude { get; init; }
}

public record CategoryDto
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Slug { get; init; } = string.Empty;
    public string? IconName { get; init; }
}

public record ReviewDto
{
    public Guid Id { get; init; }
    public Guid ProviderId { get; init; }
    public string ClientName { get; init; } = string.Empty;
    public int Rating { get; init; }
    public string? Comment { get; init; }
    public bool IsVerified { get; init; }
    public DateTime CreatedAtUtc { get; init; }
}

public record CreateReviewRequest
{
    public Guid ProviderId { get; set; }
    public int Rating { get; set; }
    public string? Comment { get; set; }
}
