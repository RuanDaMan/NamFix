using Microsoft.Extensions.Caching.Memory;
using NamFix.Application.Data.Repositories;

namespace NamFix.Application.Search;

/// <summary>Raw row loaded from the DB to build the fuzzy search index (one active provider).</summary>
public sealed record ProviderIndexRow
{
    public Guid Id { get; init; }
    public string BusinessName { get; init; } = string.Empty;
    public string? CategoryName { get; init; }
    public string? TownName { get; init; }
    public string? SearchKeywords { get; init; }
    /// <summary>Approved tag names for this provider, pipe-delimited (STRING_AGG).</summary>
    public string? TagsBlob { get; init; }
    public bool IsVerified { get; init; }
    public bool IsEmergencyCallout { get; init; }
    public int Availability { get; init; }
    public decimal? RatingAverage { get; init; }
    public int RatingCount { get; init; }
}

/// <summary>An index entry with its normalized forms precomputed once, ready for cheap scoring.</summary>
public sealed class FuzzyEntry
{
    public required Guid Id { get; init; }
    public required string BusinessName { get; init; }
    public string? CategoryName { get; init; }
    public string? TownName { get; init; }
    public bool IsVerified { get; init; }
    public bool IsEmergencyCallout { get; init; }
    public int Availability { get; init; }
    public decimal? RatingAverage { get; init; }
    public int RatingCount { get; init; }

    /// <summary>Business name, lowercased/diacritic-free with all spacing removed.</summary>
    public required string NameSpaceless { get; init; }
    /// <summary>Business-name tokens.</summary>
    public required string[] NameTokens { get; init; }
    /// <summary>Tokens from the denormalized keyword blob (name + category + tags).</summary>
    public required string[] KeywordTokens { get; init; }

    /// <summary>Approved tag names (original casing) for display.</summary>
    public required string[] Tags { get; init; }
    /// <summary>Pre-tokenized form of each entry in <see cref="Tags"/>, aligned by index.</summary>
    public required string[][] TagTokens { get; init; }

    public static FuzzyEntry FromRow(ProviderIndexRow row)
    {
        var nameTokens = FuzzyMatcher.Tokenize(row.BusinessName);
        // Keyword blob already contains the business name; add the category name for recall.
        var keywordSource = string.Join(' ', row.SearchKeywords, row.CategoryName);
        var tags = string.IsNullOrEmpty(row.TagsBlob)
            ? Array.Empty<string>()
            : row.TagsBlob.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return new FuzzyEntry
        {
            Id = row.Id,
            BusinessName = row.BusinessName,
            CategoryName = row.CategoryName,
            TownName = row.TownName,
            IsVerified = row.IsVerified,
            IsEmergencyCallout = row.IsEmergencyCallout,
            Availability = row.Availability,
            RatingAverage = row.RatingAverage,
            RatingCount = row.RatingCount,
            NameSpaceless = string.Concat(nameTokens),
            NameTokens = nameTokens,
            KeywordTokens = FuzzyMatcher.Tokenize(keywordSource),
            Tags = tags,
            TagTokens = tags.Select(FuzzyMatcher.Tokenize).ToArray(),
        };
    }
}

/// <summary>
/// A small, cached, in-memory index of active providers used for typo/spacing-tolerant matching.
/// Cached because typeahead hits it on every keystroke and the directory is small and changes rarely;
/// a short TTL keeps it fresh (e.g. admin approvals) and <see cref="Invalidate"/> drops it immediately
/// when a provider edits their own profile.
/// </summary>
public interface IProviderSearchIndex
{
    Task<IReadOnlyList<FuzzyEntry>> GetEntriesAsync();
    void Invalidate();
}

public sealed class ProviderSearchIndex : IProviderSearchIndex
{
    private const string CacheKey = "provider-fuzzy-search-index";
    private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(60);

    private readonly IProviderRepository _providers;
    private readonly IMemoryCache _cache;

    public ProviderSearchIndex(IProviderRepository providers, IMemoryCache cache)
    {
        _providers = providers;
        _cache = cache;
    }

    public Task<IReadOnlyList<FuzzyEntry>> GetEntriesAsync() =>
        _cache.GetOrCreateAsync(CacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = Ttl;
            var rows = await _providers.GetSearchIndexAsync();
            return (IReadOnlyList<FuzzyEntry>)rows.Select(FuzzyEntry.FromRow).ToList();
        })!;

    public void Invalidate() => _cache.Remove(CacheKey);
}
