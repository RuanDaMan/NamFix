using NamFix.Application.Data.Repositories;
using NamFix.Application.Search;
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

    /// <summary>Typo/spacing-tolerant autocomplete suggestions for the search box dropdown.</summary>
    Task<IReadOnlyList<ProviderSuggestion>> TypeaheadAsync(string? term, int take = 8);
}

public sealed class ProviderService : IProviderService
{
    // Fuzzy candidates fed to the SQL search are capped so a very loose query can't OR in a huge id list.
    private const int MaxFuzzyIds = 100;

    private readonly IProviderRepository _providers;
    private readonly ITaxonomyRepository _taxonomy;
    private readonly IUserRepository _users;
    private readonly IRateCardRepository _rateCards;
    private readonly IProviderSearchIndex _searchIndex;

    public ProviderService(IProviderRepository providers, ITaxonomyRepository taxonomy, IUserRepository users,
        IRateCardRepository rateCards, IProviderSearchIndex searchIndex)
    {
        _providers = providers;
        _taxonomy = taxonomy;
        _users = users;
        _rateCards = rateCards;
        _searchIndex = searchIndex;
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
        provider.YearsExperience = request.YearsExperience;
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

        // The provider's searchable text just changed — drop the cached fuzzy index so it rebuilds.
        _searchIndex.Invalidate();
        return await ToDtoAsync(provider);
    }

    public async Task<PagedResult<ProviderSearchResult>> SearchAsync(ProviderSearchRequest request)
    {
        // For a text query, pre-compute fuzzy matches so misspelled / oddly-spaced searches still
        // return results; the repository OR's these ids with its SQL full-text predicate.
        IReadOnlyCollection<Guid>? fuzzyIds = null;
        if (!string.IsNullOrWhiteSpace(request.Query))
            fuzzyIds = await FuzzyMatchIdsAsync(request.Query, MaxFuzzyIds);

        return await _providers.SearchAsync(request, fuzzyIds);
    }

    public async Task<IReadOnlyList<ProviderSuggestion>> TypeaheadAsync(string? term, int take = 8)
    {
        var query = FuzzyMatcher.PrepareQuery(term);
        // Require a couple of characters before suggesting anything, matching the old sproc's behaviour.
        if (query.IsEmpty || query.Spaceless.Length < 2)
            return Array.Empty<ProviderSuggestion>();

        take = Math.Clamp(take, 1, 20);
        var entries = await _searchIndex.GetEntriesAsync();

        return RankByScore(query, entries)
            .Take(take)
            .Select(m => new ProviderSuggestion
            {
                Id = m.Entry.Id,
                BusinessName = m.Entry.BusinessName,
                PrimaryCategoryName = m.Entry.CategoryName,
                PrimaryTownName = m.Entry.TownName,
                RatingAverage = m.Entry.RatingAverage,
                RatingCount = m.Entry.RatingCount,
                IsVerified = m.Entry.IsVerified,
                IsEmergencyCallout = m.Entry.IsEmergencyCallout,
                Availability = (AvailabilityStatus)m.Entry.Availability,
                MatchedTag = BestMatchedTag(query, m.Entry),
            })
            .ToList();
    }

    /// <summary>The tag that best matches the query, if a tag matched well — shown so the user sees why
    /// a provider surfaced (e.g. "geyser" matching a plumber). Null when the name itself carried it.</summary>
    private static string? BestMatchedTag(in FuzzyMatcher.PreparedQuery query, FuzzyEntry entry)
    {
        const double tagThreshold = 0.7;
        string? best = null;
        var bestScore = tagThreshold;
        for (var i = 0; i < entry.Tags.Length; i++)
        {
            var score = FuzzyMatcher.ScoreTerm(query, entry.TagTokens[i]);
            if (score > bestScore)
            {
                bestScore = score;
                best = entry.Tags[i];
            }
        }
        return best;
    }

    /// <summary>Fuzzy-match the query against the cached index and return the best provider ids.</summary>
    private async Task<IReadOnlyCollection<Guid>> FuzzyMatchIdsAsync(string term, int max)
    {
        var query = FuzzyMatcher.PrepareQuery(term);
        if (query.IsEmpty) return Array.Empty<Guid>();

        var entries = await _searchIndex.GetEntriesAsync();
        return RankByScore(query, entries).Take(max).Select(m => m.Entry.Id).ToList();
    }

    /// <summary>Score entries, keep matches, and order by score then verified/rating for stable ranking.</summary>
    private static IEnumerable<(FuzzyEntry Entry, double Score)> RankByScore(
        in FuzzyMatcher.PreparedQuery query, IReadOnlyList<FuzzyEntry> entries)
    {
        var matches = new List<(FuzzyEntry Entry, double Score)>();
        foreach (var entry in entries)
        {
            var score = FuzzyMatcher.Score(query, entry);
            if (score >= FuzzyMatcher.MatchThreshold)
                matches.Add((entry, score));
        }

        return matches
            .OrderByDescending(m => m.Score)
            .ThenByDescending(m => m.Entry.IsVerified)
            .ThenByDescending(m => m.Entry.RatingAverage ?? 0m);
    }

    private async Task<ProviderDto> ToDtoAsync(Provider p)
    {
        var tags = await _providers.GetTagNamesAsync(p.Id);
        var townIds = await _providers.GetTownIdsAsync(p.Id);
        var categories = await _taxonomy.GetCategoriesAsync();
        var towns = await _taxonomy.GetTownsAsync();
        var owner = await _users.GetByIdAsync(p.UserId);
        var rateCards = await _rateCards.ListDtosForProviderAsync(p.Id, activeOnly: true);

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
            YearsExperience = p.YearsExperience,
            AvgResponseMinutes = p.AvgResponseMinutes,
            StartingPrice = p.StartingPrice,
            Tags = tags.ToList(),
            TownIds = townIds.ToList(),
            RateCards = rateCards
        };
    }
}
