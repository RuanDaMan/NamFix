using Dapper;
using NamFix.Shared.Domain;
using NamFix.Shared.Enums;

namespace NamFix.Application.Data.Repositories;

public interface ICommissionRepository
{
    /// <summary>
    /// Resolve the effective commission rate for a provider/category. Most specific active rule wins:
    /// Provider override &gt; Category override &gt; Platform default.
    /// </summary>
    Task<decimal> ResolveRateAsync(Guid providerId, int? categoryId);
    Task UpsertRuleAsync(CommissionRule rule);
    Task<IReadOnlyList<CommissionRule>> GetRulesAsync();
}

public sealed class CommissionRepository : ICommissionRepository
{
    private readonly IDbConnectionFactory _db;
    public CommissionRepository(IDbConnectionFactory db) => _db = db;

    public async Task<decimal> ResolveRateAsync(Guid providerId, int? categoryId)
    {
        using var conn = await _db.CreateOpenConnectionAsync();
        var rate = await conn.QuerySingleOrDefaultAsync<decimal?>(
            """
            SELECT TOP 1 Rate FROM dbo.CommissionRules
            WHERE IsActive = 1 AND (
                  (Scope = @providerScope AND ProviderId = @providerId)
               OR (Scope = @categoryScope AND CategoryId = @categoryId)
               OR (Scope = @platformScope))
            ORDER BY Scope DESC  -- Provider(2) > Category(1) > Platform(0)
            """,
            new
            {
                providerScope = (int)CommissionScope.Provider,
                categoryScope = (int)CommissionScope.Category,
                platformScope = (int)CommissionScope.Platform,
                providerId,
                categoryId
            });

        // Fall back to a sane platform default if no rule is seeded yet.
        return rate ?? 0.10m;
    }

    public async Task UpsertRuleAsync(CommissionRule rule)
    {
        using var conn = await _db.CreateOpenConnectionAsync();
        await conn.ExecuteAsync(
            """
            MERGE dbo.CommissionRules AS target
            USING (SELECT @Scope AS Scope,
                          CAST(@CategoryId AS int) AS CategoryId,
                          CAST(@ProviderId AS uniqueidentifier) AS ProviderId) AS src
                ON  target.Scope = src.Scope
                AND ISNULL(target.CategoryId, -1) = ISNULL(src.CategoryId, -1)
                AND ISNULL(target.ProviderId, '00000000-0000-0000-0000-000000000000') =
                    ISNULL(src.ProviderId, '00000000-0000-0000-0000-000000000000')
            WHEN MATCHED THEN UPDATE SET Rate = @Rate, IsActive = @IsActive
            WHEN NOT MATCHED THEN
                INSERT (Scope, CategoryId, ProviderId, Rate, IsActive)
                VALUES (@Scope, @CategoryId, @ProviderId, @Rate, @IsActive);
            """,
            new { Scope = (int)rule.Scope, rule.CategoryId, rule.ProviderId, rule.Rate, rule.IsActive });
    }

    public async Task<IReadOnlyList<CommissionRule>> GetRulesAsync()
    {
        using var conn = await _db.CreateOpenConnectionAsync();
        return (await conn.QueryAsync<CommissionRule>(
            "SELECT * FROM dbo.CommissionRules ORDER BY Scope DESC")).AsList();
    }
}
