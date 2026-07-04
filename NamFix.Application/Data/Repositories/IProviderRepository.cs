using System.Data;
using System.Text;
using Dapper;
using NamFix.Application.Search;
using NamFix.Shared.Domain;
using NamFix.Shared.Dtos;
using NamFix.Shared.Enums;

namespace NamFix.Application.Data.Repositories;

public interface IProviderRepository
{
    Task<Provider?> GetByIdAsync(Guid id);
    Task<Provider?> GetByUserIdAsync(Guid userId);
    Task<IReadOnlyList<string>> GetTagNamesAsync(Guid providerId);
    Task<IReadOnlyList<int>> GetTownIdsAsync(Guid providerId);
    Task UpsertAsync(Provider provider, IReadOnlyList<int> townIds, IReadOnlyList<int> tagIds, string searchKeywords);
    Task SetStatusAsync(Guid providerId, ProviderStatus status);
    Task SetVerifiedAsync(Guid providerId, bool verified);
    Task RecalculateRatingAsync(Guid providerId);

    /// <summary>
    /// Filtered/sorted provider search. <paramref name="fuzzyIds"/>, when supplied, are provider ids
    /// the application's fuzzy matcher found for the text query; they're OR'd with the SQL full-text
    /// predicate so misspelled / oddly-spaced queries still return results.
    /// </summary>
    Task<PagedResult<ProviderSearchResult>> SearchAsync(
        ProviderSearchRequest request, IReadOnlyCollection<Guid>? fuzzyIds = null);

    /// <summary>Lightweight projection of every active provider used to build the fuzzy search index.</summary>
    Task<IReadOnlyList<ProviderIndexRow>> GetSearchIndexAsync();

    /// <summary>Denormalized starting price = min active rate-card price. Maintained on rate-card save.</summary>
    Task SetStartingPriceAsync(Guid providerId, decimal? price);
    /// <summary>Denormalized average response time (minutes). Maintained when a provider quotes.</summary>
    Task SetAvgResponseMinutesAsync(Guid providerId, int? minutes);
    Task IncrementNoShowAsync(Guid providerId);
    Task IncrementLateCancellationAsync(Guid providerId);

    /// <summary>Active providers matching a broadcast job (category + town + available now), optionally
    /// emergency-flagged and within a radius. Returns their id + owning user for fan-out.</summary>
    Task<List<ProviderMatch>> FindMatchingProvidersAsync(
        int? categoryId, int? townId, bool emergencyOnly, double? lat, double? lng, double radiusKm);
}

/// <summary>A provider matched for a broadcast job, with just what fan-out needs.</summary>
public sealed record ProviderMatch
{
    public Guid ProviderId { get; init; }
    public Guid ProviderUserId { get; init; }
    public int? AvgResponseMinutes { get; init; }
    public double? DistanceKm { get; init; }
}

public sealed class ProviderRepository : IProviderRepository
{
    private readonly IDbConnectionFactory _db;
    public ProviderRepository(IDbConnectionFactory db) => _db = db;

    public async Task<Provider?> GetByIdAsync(Guid id)
    {
        using var conn = await _db.CreateOpenConnectionAsync();
        return await conn.QuerySingleOrDefaultAsync<Provider>(
            "SELECT * FROM dbo.Providers WHERE Id = @id", new { id });
    }

    public async Task<Provider?> GetByUserIdAsync(Guid userId)
    {
        using var conn = await _db.CreateOpenConnectionAsync();
        return await conn.QuerySingleOrDefaultAsync<Provider>(
            "SELECT * FROM dbo.Providers WHERE UserId = @userId", new { userId });
    }

    public async Task<IReadOnlyList<string>> GetTagNamesAsync(Guid providerId)
    {
        using var conn = await _db.CreateOpenConnectionAsync();
        return (await conn.QueryAsync<string>(
            """
            SELECT t.Name FROM dbo.ProviderTags pt
            JOIN dbo.Tags t ON t.Id = pt.TagId
            WHERE pt.ProviderId = @providerId AND t.Status = @approved
            ORDER BY t.Name
            """, new { providerId, approved = (int)TagStatus.Approved })).AsList();
    }

    public async Task<IReadOnlyList<int>> GetTownIdsAsync(Guid providerId)
    {
        using var conn = await _db.CreateOpenConnectionAsync();
        return (await conn.QueryAsync<int>(
            "SELECT TownId FROM dbo.ProviderTowns WHERE ProviderId = @providerId",
            new { providerId })).AsList();
    }

    public async Task UpsertAsync(Provider p, IReadOnlyList<int> townIds, IReadOnlyList<int> tagIds, string searchKeywords)
    {
        using var conn = await _db.CreateOpenConnectionAsync();
        using var tx = conn.BeginTransaction();

        await conn.ExecuteAsync(
            """
            MERGE dbo.Providers AS target
            USING (SELECT @Id AS Id) AS src ON target.Id = src.Id
            WHEN MATCHED THEN UPDATE SET
                BusinessName = @BusinessName, Description = @Description, PrimaryCategoryId = @PrimaryCategoryId,
                Availability = @Availability, IsEmergencyCallout = @IsEmergencyCallout,
                Latitude = @Latitude, Longitude = @Longitude, PrimaryTownId = @PrimaryTownId,
                YearsExperience = @YearsExperience,
                SearchKeywords = @SearchKeywords, UpdatedAtUtc = SYSUTCDATETIME()
            WHEN NOT MATCHED THEN INSERT
                (Id, UserId, BusinessName, Description, PrimaryCategoryId, Status, Availability,
                 IsVerified, IsEmergencyCallout, Latitude, Longitude, PrimaryTownId, YearsExperience,
                 RatingAverage, RatingCount, SearchKeywords, CreatedAtUtc, UpdatedAtUtc)
            VALUES
                (@Id, @UserId, @BusinessName, @Description, @PrimaryCategoryId, @Status, @Availability,
                 @IsVerified, @IsEmergencyCallout, @Latitude, @Longitude, @PrimaryTownId, @YearsExperience,
                 NULL, 0, @SearchKeywords, SYSUTCDATETIME(), SYSUTCDATETIME());
            """,
            new
            {
                p.Id, p.UserId, p.BusinessName, p.Description, p.PrimaryCategoryId,
                Status = (int)p.Status, Availability = (int)p.Availability,
                p.IsVerified, p.IsEmergencyCallout, p.Latitude, p.Longitude, p.PrimaryTownId,
                p.YearsExperience, SearchKeywords = searchKeywords
            }, tx);

        await conn.ExecuteAsync("DELETE FROM dbo.ProviderTowns WHERE ProviderId = @id", new { id = p.Id }, tx);
        await conn.ExecuteAsync("DELETE FROM dbo.ProviderTags WHERE ProviderId = @id", new { id = p.Id }, tx);

        foreach (var townId in townIds.Distinct())
            await conn.ExecuteAsync(
                "INSERT INTO dbo.ProviderTowns (ProviderId, TownId) VALUES (@pid, @tid)",
                new { pid = p.Id, tid = townId }, tx);

        foreach (var tagId in tagIds.Distinct())
            await conn.ExecuteAsync(
                "INSERT INTO dbo.ProviderTags (ProviderId, TagId) VALUES (@pid, @tid)",
                new { pid = p.Id, tid = tagId }, tx);

        tx.Commit();
    }

    public async Task SetStatusAsync(Guid providerId, ProviderStatus status)
    {
        using var conn = await _db.CreateOpenConnectionAsync();
        await conn.ExecuteAsync(
            "UPDATE dbo.Providers SET Status = @status, UpdatedAtUtc = SYSUTCDATETIME() WHERE Id = @providerId",
            new { status = (int)status, providerId });
    }

    public async Task SetVerifiedAsync(Guid providerId, bool verified)
    {
        using var conn = await _db.CreateOpenConnectionAsync();
        await conn.ExecuteAsync(
            "UPDATE dbo.Providers SET IsVerified = @verified, UpdatedAtUtc = SYSUTCDATETIME() WHERE Id = @providerId",
            new { verified, providerId });
    }

    public async Task RecalculateRatingAsync(Guid providerId)
    {
        using var conn = await _db.CreateOpenConnectionAsync();
        await conn.ExecuteAsync(
            """
            UPDATE dbo.Providers
            SET RatingAverage = (SELECT AVG(CAST(Rating AS decimal(3,2))) FROM dbo.Reviews WHERE ProviderId = @providerId),
                RatingCount   = (SELECT COUNT(*) FROM dbo.Reviews WHERE ProviderId = @providerId)
            WHERE Id = @providerId
            """, new { providerId });
    }

    public async Task SetStartingPriceAsync(Guid providerId, decimal? price)
    {
        using var conn = await _db.CreateOpenConnectionAsync();
        await conn.ExecuteAsync(
            "UPDATE dbo.Providers SET StartingPrice = @price, UpdatedAtUtc = SYSUTCDATETIME() WHERE Id = @providerId",
            new { price, providerId });
    }

    public async Task SetAvgResponseMinutesAsync(Guid providerId, int? minutes)
    {
        using var conn = await _db.CreateOpenConnectionAsync();
        await conn.ExecuteAsync(
            "UPDATE dbo.Providers SET AvgResponseMinutes = @minutes, UpdatedAtUtc = SYSUTCDATETIME() WHERE Id = @providerId",
            new { minutes, providerId });
    }

    public async Task IncrementNoShowAsync(Guid providerId)
    {
        using var conn = await _db.CreateOpenConnectionAsync();
        await conn.ExecuteAsync(
            "UPDATE dbo.Providers SET NoShowCount = NoShowCount + 1 WHERE Id = @providerId", new { providerId });
    }

    public async Task IncrementLateCancellationAsync(Guid providerId)
    {
        using var conn = await _db.CreateOpenConnectionAsync();
        await conn.ExecuteAsync(
            "UPDATE dbo.Providers SET LateCancellationCount = LateCancellationCount + 1 WHERE Id = @providerId",
            new { providerId });
    }

    public async Task<List<ProviderMatch>> FindMatchingProvidersAsync(
        int? categoryId, int? townId, bool emergencyOnly, double? lat, double? lng, double radiusKm)
    {
        var p = new DynamicParameters();
        var where = new StringBuilder("WHERE pr.Status = @activeStatus AND pr.Availability = @available");
        p.Add("activeStatus", (int)ProviderStatus.Active);
        p.Add("available", (int)AvailabilityStatus.Available);

        if (categoryId is { } catId)
        {
            where.Append(" AND pr.PrimaryCategoryId = @catId");
            p.Add("catId", catId);
        }
        if (townId is { } tid)
        {
            where.Append(" AND EXISTS (SELECT 1 FROM dbo.ProviderTowns ptw WHERE ptw.ProviderId = pr.Id AND ptw.TownId = @tid)");
            p.Add("tid", tid);
        }
        if (emergencyOnly)
            where.Append(" AND pr.IsEmergencyCallout = 1");

        var hasGeo = lat is not null && lng is not null;
        const string distanceExpr =
            "6371 * 2 * ASIN(SQRT(POWER(SIN(RADIANS(pr.Latitude - @lat) / 2), 2) + " +
            "COS(RADIANS(@lat)) * COS(RADIANS(pr.Latitude)) * POWER(SIN(RADIANS(pr.Longitude - @lng) / 2), 2)))";
        var distanceSelect = hasGeo
            ? $", CASE WHEN pr.Latitude IS NULL OR pr.Longitude IS NULL THEN NULL ELSE {distanceExpr} END AS DistanceKm"
            : ", CAST(NULL AS float) AS DistanceKm";
        if (hasGeo)
        {
            p.Add("lat", lat);
            p.Add("lng", lng);
            if (radiusKm > 0)
            {
                where.Append($" AND pr.Latitude IS NOT NULL AND pr.Longitude IS NOT NULL AND {distanceExpr} <= @radiusKm");
                p.Add("radiusKm", radiusKm);
            }
        }

        // Prioritize by proximity when known, then by fastest historical response time.
        var orderBy = hasGeo
            ? "ORDER BY DistanceKm, pr.AvgResponseMinutes"
            : "ORDER BY pr.AvgResponseMinutes, pr.RatingAverage DESC";

        var sql =
            $"""
            SELECT pr.Id AS ProviderId, pr.UserId AS ProviderUserId, pr.AvgResponseMinutes
                   {distanceSelect}
            FROM dbo.Providers pr
            {where}
            {orderBy}
            """;

        using var conn = await _db.CreateOpenConnectionAsync();
        return (await conn.QueryAsync<ProviderMatch>(sql, p)).AsList();
    }

    public async Task<PagedResult<ProviderSearchResult>> SearchAsync(
        ProviderSearchRequest r, IReadOnlyCollection<Guid>? fuzzyIds = null)
    {
        var p = new DynamicParameters();
        var where = new StringBuilder("WHERE pr.Status = @activeStatus");
        p.Add("activeStatus", (int)ProviderStatus.Active);

        // Text predicate: FREETEXT gives natural-language, inflection-tolerant matching across the
        // provider name, description, and denormalized tag/category keywords. When the application's
        // fuzzy matcher also found candidates (typos / odd spacing that full-text misses), OR them in
        // so those providers still surface.
        if (!string.IsNullOrWhiteSpace(r.Query))
        {
            var hasFuzzy = fuzzyIds is { Count: > 0 };
            if (hasFuzzy)
            {
                where.Append(" AND (FREETEXT((pr.BusinessName, pr.Description, pr.SearchKeywords), @query) OR pr.Id IN @fuzzyIds)");
                p.Add("fuzzyIds", fuzzyIds);
            }
            else
            {
                where.Append(" AND FREETEXT((pr.BusinessName, pr.Description, pr.SearchKeywords), @query)");
            }
            p.Add("query", r.Query.Trim());
        }

        if (r.CategoryId is { } catId)
        {
            where.Append(" AND pr.PrimaryCategoryId = @catId");
            p.Add("catId", catId);
        }

        if (r.TownId is { } townId)
        {
            where.Append(" AND EXISTS (SELECT 1 FROM dbo.ProviderTowns ptw WHERE ptw.ProviderId = pr.Id AND ptw.TownId = @townId)");
            p.Add("townId", townId);
        }

        if (r.MinRating is { } minRating)
        {
            where.Append(" AND pr.RatingAverage >= @minRating");
            p.Add("minRating", minRating);
        }

        if (r.EmergencyOnly == true)
            where.Append(" AND pr.IsEmergencyCallout = 1");

        if (r.AvailableNowOnly == true)
        {
            where.Append(" AND pr.Availability = @available");
            p.Add("available", (int)AvailabilityStatus.Available);
        }

        // Extended filters (price range via denormalized StartingPrice, experience, response time).
        if (r.MinPrice is { } minPrice)
        {
            where.Append(" AND pr.StartingPrice >= @minPrice");
            p.Add("minPrice", minPrice);
        }
        if (r.MaxPrice is { } maxPrice)
        {
            where.Append(" AND pr.StartingPrice IS NOT NULL AND pr.StartingPrice <= @maxPrice");
            p.Add("maxPrice", maxPrice);
        }
        if (r.MinYearsExperience is { } minYears)
        {
            where.Append(" AND pr.YearsExperience >= @minYears");
            p.Add("minYears", minYears);
        }
        if (r.MaxResponseMinutes is { } maxResp)
        {
            where.Append(" AND pr.AvgResponseMinutes IS NOT NULL AND pr.AvgResponseMinutes <= @maxResp");
            p.Add("maxResp", maxResp);
        }

        if (r.Tags is { Count: > 0 })
        {
            where.Append(
                """
                 AND EXISTS (
                    SELECT 1 FROM dbo.ProviderTags pt2
                    JOIN dbo.Tags t2 ON t2.Id = pt2.TagId
                    WHERE pt2.ProviderId = pr.Id AND t2.Name IN @tags)
                """);
            p.Add("tags", r.Tags);
        }

        // Distance (haversine, km) when a reference point is supplied — enables "near me" sorting.
        var hasGeo = r.NearLatitude is not null && r.NearLongitude is not null;
        const string distanceExpr =
            "6371 * 2 * ASIN(SQRT(POWER(SIN(RADIANS(pr.Latitude - @lat) / 2), 2) + " +
            "COS(RADIANS(@lat)) * COS(RADIANS(pr.Latitude)) * POWER(SIN(RADIANS(pr.Longitude - @lng) / 2), 2)))";
        var distanceSelect = hasGeo
            ? $", CASE WHEN pr.Latitude IS NULL OR pr.Longitude IS NULL THEN NULL ELSE {distanceExpr} END AS DistanceKm"
            : ", CAST(NULL AS float) AS DistanceKm";
        if (hasGeo)
        {
            p.Add("lat", r.NearLatitude);
            p.Add("lng", r.NearLongitude);

            // "Near me" is bounded to a radius so distant providers aren't returned. A provider with no
            // coordinates can't be measured, so it's excluded when searching by location.
            if (r.RadiusKm > 0)
            {
                where.Append($" AND pr.Latitude IS NOT NULL AND pr.Longitude IS NOT NULL AND {distanceExpr} <= @radiusKm");
                p.Add("radiusKm", r.RadiusKm);
            }
        }

        // Order: nearest first when geo given, otherwise verified + rating.
        var orderBy = hasGeo
            ? "ORDER BY CASE WHEN pr.Latitude IS NULL OR pr.Longitude IS NULL THEN 1 ELSE 0 END, DistanceKm, pr.RatingAverage DESC"
            : "ORDER BY pr.IsVerified DESC, pr.RatingAverage DESC, pr.RatingCount DESC";

        var page = Math.Max(1, r.Page);
        var pageSize = Math.Clamp(r.PageSize, 1, 100);
        p.Add("offset", (page - 1) * pageSize);
        p.Add("pageSize", pageSize);

        var sql =
            $"""
            SELECT COUNT(*) FROM dbo.Providers pr {where};

            SELECT
                pr.Id, pr.BusinessName,
                c.Name AS PrimaryCategoryName,
                t.Name AS PrimaryTownName,
                pr.Availability, pr.IsVerified, pr.IsEmergencyCallout,
                pr.RatingAverage, pr.RatingCount,
                pr.YearsExperience, pr.AvgResponseMinutes, pr.StartingPrice,
                pr.Latitude, pr.Longitude
                {distanceSelect}
            FROM dbo.Providers pr
            LEFT JOIN dbo.Categories c ON c.Id = pr.PrimaryCategoryId
            LEFT JOIN dbo.Towns t ON t.Id = pr.PrimaryTownId
            {where}
            {orderBy}
            OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY;
            """;

        using var conn = await _db.CreateOpenConnectionAsync();
        using var multi = await conn.QueryMultipleAsync(sql, p);
        var total = await multi.ReadSingleAsync<int>();
        var rows = (await multi.ReadAsync<ProviderSearchResult>()).AsList();

        // Tags are loaded per page to keep the main query lean and the payload data-light.
        if (rows.Count > 0)
        {
            var ids = rows.Select(x => x.Id).ToArray();
            var tagLookup = (await conn.QueryAsync<(Guid ProviderId, string Name)>(
                """
                SELECT pt.ProviderId, t.Name
                FROM dbo.ProviderTags pt
                JOIN dbo.Tags t ON t.Id = pt.TagId
                WHERE pt.ProviderId IN @ids AND t.Status = @approved
                """, new { ids, approved = (int)TagStatus.Approved }))
                .GroupBy(x => x.ProviderId)
                .ToDictionary(g => g.Key, g => g.Select(x => x.Name).ToList());

            foreach (var row in rows)
                if (tagLookup.TryGetValue(row.Id, out var tags))
                    row.Tags.AddRange(tags);
        }

        return new PagedResult<ProviderSearchResult>
        {
            Items = rows,
            Page = page,
            PageSize = pageSize,
            TotalCount = total
        };
    }

    public async Task<IReadOnlyList<ProviderIndexRow>> GetSearchIndexAsync()
    {
        using var conn = await _db.CreateOpenConnectionAsync();
        return (await conn.QueryAsync<ProviderIndexRow>(
            """
            SELECT pr.Id, pr.BusinessName, c.Name AS CategoryName, t.Name AS TownName,
                   pr.SearchKeywords, pr.IsVerified, pr.IsEmergencyCallout, pr.Availability,
                   pr.RatingAverage, pr.RatingCount,
                   (SELECT STRING_AGG(tg.Name, '|')
                    FROM dbo.ProviderTags ptg
                    JOIN dbo.Tags tg ON tg.Id = ptg.TagId
                    WHERE ptg.ProviderId = pr.Id AND tg.Status = @approved) AS TagsBlob
            FROM dbo.Providers pr
            LEFT JOIN dbo.Categories c ON c.Id = pr.PrimaryCategoryId
            LEFT JOIN dbo.Towns t ON t.Id = pr.PrimaryTownId
            WHERE pr.Status = @activeStatus
            """,
            new { activeStatus = (int)ProviderStatus.Active, approved = (int)TagStatus.Approved })).AsList();
    }
}
