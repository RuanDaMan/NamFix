using NamFix.Application.Data.Repositories;
using NamFix.Shared.Dtos;
using NamFix.Shared.Enums;

namespace NamFix.Application.Services;

public interface ITaxonomyService
{
    Task<IReadOnlyList<TownDto>> GetTownsAsync();
    Task<IReadOnlyList<CategoryDto>> GetCategoriesAsync();
    Task<IReadOnlyList<string>> GetApprovedTagsAsync();
}

public sealed class TaxonomyService : ITaxonomyService
{
    private readonly ITaxonomyRepository _repo;
    public TaxonomyService(ITaxonomyRepository repo) => _repo = repo;

    public async Task<IReadOnlyList<TownDto>> GetTownsAsync() =>
        (await _repo.GetTownsAsync())
        .Select(t => new TownDto
        {
            Id = t.Id, Name = t.Name, Region = t.Region, Latitude = t.Latitude, Longitude = t.Longitude
        }).ToList();

    public async Task<IReadOnlyList<CategoryDto>> GetCategoriesAsync() =>
        (await _repo.GetCategoriesAsync())
        .Select(c => new CategoryDto { Id = c.Id, Name = c.Name, Slug = c.Slug, IconName = c.IconName })
        .ToList();

    public async Task<IReadOnlyList<string>> GetApprovedTagsAsync() =>
        (await _repo.GetTagsAsync(TagStatus.Approved)).Select(t => t.Name).ToList();
}
