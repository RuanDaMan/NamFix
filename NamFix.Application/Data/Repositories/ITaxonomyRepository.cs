using Dapper;
using NamFix.Shared.Domain;
using NamFix.Shared.Enums;

namespace NamFix.Application.Data.Repositories;

public interface ITaxonomyRepository
{
    Task<IReadOnlyList<Town>> GetTownsAsync();
    Task<IReadOnlyList<Category>> GetCategoriesAsync();
    Task<IReadOnlyList<Tag>> GetTagsAsync(TagStatus? status = null);

    /// <summary>Resolve approved tag ids by name, queuing unknown free-text tags for moderation.</summary>
    Task<IReadOnlyList<int>> EnsureTagsAsync(IEnumerable<string> names, Guid createdByUserId);
    Task SetTagStatusAsync(int tagId, TagStatus status);
    Task<int> AddTownAsync(Town town);
    Task<int> AddCategoryAsync(Category category);
}

public sealed class TaxonomyRepository : ITaxonomyRepository
{
    private readonly IDbConnectionFactory _db;
    public TaxonomyRepository(IDbConnectionFactory db) => _db = db;

    public async Task<IReadOnlyList<Town>> GetTownsAsync()
    {
        using var conn = await _db.CreateOpenConnectionAsync();
        return (await conn.QueryAsync<Town>(
            "SELECT * FROM dbo.Towns WHERE IsActive = 1 ORDER BY Name")).AsList();
    }

    public async Task<IReadOnlyList<Category>> GetCategoriesAsync()
    {
        using var conn = await _db.CreateOpenConnectionAsync();
        return (await conn.QueryAsync<Category>(
            "SELECT * FROM dbo.Categories WHERE IsActive = 1 ORDER BY Name")).AsList();
    }

    public async Task<IReadOnlyList<Tag>> GetTagsAsync(TagStatus? status = null)
    {
        using var conn = await _db.CreateOpenConnectionAsync();
        var sql = status is null
            ? "SELECT * FROM dbo.Tags ORDER BY Name"
            : "SELECT * FROM dbo.Tags WHERE Status = @status ORDER BY Name";
        return (await conn.QueryAsync<Tag>(sql, new { status = (int?)status })).AsList();
    }

    public async Task<IReadOnlyList<int>> EnsureTagsAsync(IEnumerable<string> names, Guid createdByUserId)
    {
        var clean = names
            .Select(n => n.Trim())
            .Where(n => n.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (clean.Count == 0) return Array.Empty<int>();

        using var conn = await _db.CreateOpenConnectionAsync();
        var ids = new List<int>();
        foreach (var name in clean)
        {
            // Insert as Pending if new (free-text tags queue for admin approval); always return the id.
            var id = await conn.QuerySingleAsync<int>(
                """
                MERGE dbo.Tags AS target
                USING (SELECT @name AS Name) AS src ON target.Name = src.Name
                WHEN NOT MATCHED THEN
                    INSERT (Name, Status, CreatedByUserId) VALUES (@name, @pending, @createdByUserId)
                OUTPUT inserted.Id;
                """,
                new { name, pending = (int)TagStatus.Pending, createdByUserId });
            ids.Add(id);
        }
        return ids;
    }

    public async Task SetTagStatusAsync(int tagId, TagStatus status)
    {
        using var conn = await _db.CreateOpenConnectionAsync();
        await conn.ExecuteAsync(
            "UPDATE dbo.Tags SET Status = @status WHERE Id = @tagId",
            new { status = (int)status, tagId });
    }

    public async Task<int> AddTownAsync(Town town)
    {
        using var conn = await _db.CreateOpenConnectionAsync();
        return await conn.QuerySingleAsync<int>(
            """
            INSERT INTO dbo.Towns (Name, Region, Latitude, Longitude, IsActive)
            OUTPUT inserted.Id
            VALUES (@Name, @Region, @Latitude, @Longitude, @IsActive)
            """, town);
    }

    public async Task<int> AddCategoryAsync(Category category)
    {
        using var conn = await _db.CreateOpenConnectionAsync();
        return await conn.QuerySingleAsync<int>(
            """
            INSERT INTO dbo.Categories (Name, Slug, IconName, IsActive)
            OUTPUT inserted.Id
            VALUES (@Name, @Slug, @IconName, @IsActive)
            """, category);
    }
}
