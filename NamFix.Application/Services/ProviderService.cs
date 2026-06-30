using NamFix.Application.Data.Repositories;
using NamFix.Shared.Domain;
using NamFix.Shared.Dtos;
using NamFix.Shared.Enums;

namespace NamFix.Application.Services;

public interface IProviderService
{
    Task<ProviderDto?> GetByIdAsync(Guid id);
    Task<ProviderDto?> GetForUserAsync(Guid userId);
    Task<ProviderDto> SaveAsync(Guid userId, SaveProviderRequest request);
    Task<PagedResult<ProviderSearchResult>> SearchAsync(ProviderSearchRequest request);
}

public sealed class ProviderService : IProviderService
{
    private readonly IProviderRepository _providers;
    private readonly ITaxonomyRepository _taxonomy;
    private readonly IUserRepository _users;

    public ProviderService(IProviderRepository providers, ITaxonomyRepository taxonomy, IUserRepository users)
    {
        _providers = providers;
        _taxonomy = taxonomy;
        _users = users;
    }

    public async Task<ProviderDto?> GetByIdAsync(Guid id)
    {
        var provider = await _providers.GetByIdAsync(id);
        return provider is null ? null : await ToDtoAsync(provider);
    }

    public async Task<ProviderDto?> GetForUserAsync(Guid userId)
    {
        var provider = await _providers.GetByUserIdAsync(userId);
        return provider is null ? null : await ToDtoAsync(provider);
    }

    public async Task<ProviderDto> SaveAsync(Guid userId, SaveProviderRequest request)
    {
        var existing = await _providers.GetByUserIdAsync(userId);

        var provider = existing ?? new Provider
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            // New providers start pending admin approval and are not searchable until Active.
            Status = ProviderStatus.PendingApproval,
            IsVerified = false,
            CreatedAtUtc = DateTime.UtcNow
        };

        provider.BusinessName = request.BusinessName.Trim();
        provider.Description = request.Description;
        provider.PrimaryCategoryId = request.PrimaryCategoryId;
        provider.Availability = request.Availability;
        provider.IsEmergencyCallout = request.IsEmergencyCallout;
        provider.Latitude = request.Latitude;
        provider.Longitude = request.Longitude;
        provider.PrimaryTownId = request.PrimaryTownId;
        provider.UpdatedAtUtc = DateTime.UtcNow;

        // Free-text tags are queued for moderation; only approved tags end up searchable.
        var tagIds = await _taxonomy.EnsureTagsAsync(request.Tags, userId);

        // Build the denormalized keyword blob that feeds the full-text index (name + tags + category).
        var categories = await _taxonomy.GetCategoriesAsync();
        var categoryName = categories.FirstOrDefault(c => c.Id == request.PrimaryCategoryId)?.Name ?? "";
        var searchKeywords = string.Join(' ',
            new[] { provider.BusinessName, categoryName }
                .Concat(request.Tags)
                .Where(s => !string.IsNullOrWhiteSpace(s)));

        await _providers.UpsertAsync(provider, request.TownIds, tagIds, searchKeywords);
        return await ToDtoAsync(provider);
    }

    public Task<PagedResult<ProviderSearchResult>> SearchAsync(ProviderSearchRequest request) =>
        _providers.SearchAsync(request);

    private async Task<ProviderDto> ToDtoAsync(Provider p)
    {
        var tags = await _providers.GetTagNamesAsync(p.Id);
        var townIds = await _providers.GetTownIdsAsync(p.Id);
        var categories = await _taxonomy.GetCategoriesAsync();
        var towns = await _taxonomy.GetTownsAsync();
        var owner = await _users.GetByIdAsync(p.UserId);

        return new ProviderDto
        {
            Id = p.Id,
            UserId = p.UserId,
            BusinessName = p.BusinessName,
            Description = p.Description,
            PrimaryCategoryId = p.PrimaryCategoryId,
            PrimaryCategoryName = categories.FirstOrDefault(c => c.Id == p.PrimaryCategoryId)?.Name,
            Status = p.Status,
            Availability = p.Availability,
            IsVerified = p.IsVerified,
            IsEmergencyCallout = p.IsEmergencyCallout,
            Latitude = p.Latitude,
            Longitude = p.Longitude,
            PrimaryTownId = p.PrimaryTownId,
            PrimaryTownName = towns.FirstOrDefault(t => t.Id == p.PrimaryTownId)?.Name,
            RatingAverage = p.RatingAverage,
            RatingCount = p.RatingCount,
            OwnerPhoneNumber = owner?.PhoneNumber,
            Tags = tags.ToList(),
            TownIds = townIds.ToList()
        };
    }
}
