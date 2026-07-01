using Dapper;
using NamFix.Shared.Domain;
using NamFix.Shared.Dtos;

namespace NamFix.Application.Data.Repositories;

public interface IRateCardRepository
{
    Task<List<RateCardDto>> ListDtosForProviderAsync(Guid providerId, bool activeOnly);
    Task<ProviderRateCard?> GetByIdAsync(Guid id);
    Task UpsertAsync(ProviderRateCard card);
    Task DeleteAsync(Guid providerId, Guid id);
    /// <summary>Lowest active rate-card price for the provider (drives Providers.StartingPrice + price filter).</summary>
    Task<decimal?> GetMinActivePriceAsync(Guid providerId);
}

public sealed class RateCardRepository : IRateCardRepository
{
    private readonly IDbConnectionFactory _db;
    public RateCardRepository(IDbConnectionFactory db) => _db = db;

    public async Task<List<RateCardDto>> ListDtosForProviderAsync(Guid providerId, bool activeOnly)
    {
        using var conn = await _db.CreateOpenConnectionAsync();
        var activeFilter = activeOnly ? "AND rc.IsActive = 1" : "";
        return (await conn.QueryAsync<RateCardDto>(
            $"""
            SELECT rc.Id, rc.ProviderId, rc.CategoryId, c.Name AS CategoryName, rc.Title, rc.Description,
                   rc.Price, rc.Unit, rc.IsActive, rc.SortOrder
            FROM dbo.ProviderRateCards rc
            LEFT JOIN dbo.Categories c ON c.Id = rc.CategoryId
            WHERE rc.ProviderId = @providerId {activeFilter}
            ORDER BY rc.SortOrder, rc.Title
            """, new { providerId })).AsList();
    }

    public async Task<ProviderRateCard?> GetByIdAsync(Guid id)
    {
        using var conn = await _db.CreateOpenConnectionAsync();
        return await conn.QuerySingleOrDefaultAsync<ProviderRateCard>(
            "SELECT * FROM dbo.ProviderRateCards WHERE Id = @id", new { id });
    }

    public async Task UpsertAsync(ProviderRateCard c)
    {
        using var conn = await _db.CreateOpenConnectionAsync();
        await conn.ExecuteAsync(
            """
            MERGE dbo.ProviderRateCards AS target
            USING (SELECT @Id AS Id) AS src ON target.Id = src.Id
            WHEN MATCHED THEN UPDATE SET
                CategoryId = @CategoryId, Title = @Title, Description = @Description, Price = @Price,
                Unit = @Unit, IsActive = @IsActive, SortOrder = @SortOrder, UpdatedAtUtc = SYSUTCDATETIME()
            WHEN NOT MATCHED THEN INSERT
                (Id, ProviderId, CategoryId, Title, Description, Price, Unit, IsActive, SortOrder, CreatedAtUtc, UpdatedAtUtc)
            VALUES
                (@Id, @ProviderId, @CategoryId, @Title, @Description, @Price, @Unit, @IsActive, @SortOrder, SYSUTCDATETIME(), SYSUTCDATETIME());
            """,
            new
            {
                c.Id, c.ProviderId, c.CategoryId, c.Title, c.Description, c.Price, Unit = (int)c.Unit,
                c.IsActive, c.SortOrder
            });
    }

    public async Task DeleteAsync(Guid providerId, Guid id)
    {
        using var conn = await _db.CreateOpenConnectionAsync();
        await conn.ExecuteAsync(
            "DELETE FROM dbo.ProviderRateCards WHERE Id = @id AND ProviderId = @providerId",
            new { id, providerId });
    }

    public async Task<decimal?> GetMinActivePriceAsync(Guid providerId)
    {
        using var conn = await _db.CreateOpenConnectionAsync();
        return await conn.ExecuteScalarAsync<decimal?>(
            "SELECT MIN(Price) FROM dbo.ProviderRateCards WHERE ProviderId = @providerId AND IsActive = 1",
            new { providerId });
    }
}
