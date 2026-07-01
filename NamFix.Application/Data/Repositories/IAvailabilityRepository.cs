using Dapper;
using NamFix.Shared.Domain;

namespace NamFix.Application.Data.Repositories;

public interface IAvailabilityRepository
{
    Task<List<ProviderAvailabilityRule>> GetRulesAsync(Guid providerId);
    Task ReplaceRulesAsync(Guid providerId, IEnumerable<ProviderAvailabilityRule> rules);
    Task<List<ProviderTimeOff>> GetTimeOffAsync(Guid providerId);
    Task AddTimeOffAsync(ProviderTimeOff timeOff);
    Task DeleteTimeOffAsync(Guid providerId, Guid id);
}

public sealed class AvailabilityRepository : IAvailabilityRepository
{
    private readonly IDbConnectionFactory _db;
    public AvailabilityRepository(IDbConnectionFactory db) => _db = db;

    public async Task<List<ProviderAvailabilityRule>> GetRulesAsync(Guid providerId)
    {
        using var conn = await _db.CreateOpenConnectionAsync();
        return (await conn.QueryAsync<ProviderAvailabilityRule>(
            "SELECT Id, ProviderId, [DayOfWeek], StartTime, EndTime FROM dbo.ProviderAvailabilityRules WHERE ProviderId = @providerId ORDER BY [DayOfWeek], StartTime",
            new { providerId })).AsList();
    }

    public async Task ReplaceRulesAsync(Guid providerId, IEnumerable<ProviderAvailabilityRule> rules)
    {
        using var conn = await _db.CreateOpenConnectionAsync();
        using var tx = conn.BeginTransaction();
        await conn.ExecuteAsync("DELETE FROM dbo.ProviderAvailabilityRules WHERE ProviderId = @providerId",
            new { providerId }, tx);
        foreach (var r in rules)
            await conn.ExecuteAsync(
                """
                INSERT INTO dbo.ProviderAvailabilityRules (Id, ProviderId, [DayOfWeek], StartTime, EndTime)
                VALUES (@Id, @ProviderId, @DayOfWeek, @StartTime, @EndTime)
                """,
                new { Id = Guid.NewGuid(), ProviderId = providerId, DayOfWeek = (byte)r.DayOfWeek, r.StartTime, r.EndTime }, tx);
        tx.Commit();
    }

    public async Task<List<ProviderTimeOff>> GetTimeOffAsync(Guid providerId)
    {
        using var conn = await _db.CreateOpenConnectionAsync();
        return (await conn.QueryAsync<ProviderTimeOff>(
            "SELECT * FROM dbo.ProviderTimeOff WHERE ProviderId = @providerId ORDER BY StartUtc",
            new { providerId })).AsList();
    }

    public async Task AddTimeOffAsync(ProviderTimeOff t)
    {
        using var conn = await _db.CreateOpenConnectionAsync();
        await conn.ExecuteAsync(
            """
            INSERT INTO dbo.ProviderTimeOff (Id, ProviderId, StartUtc, EndUtc, Reason, CreatedAtUtc)
            VALUES (@Id, @ProviderId, @StartUtc, @EndUtc, @Reason, @CreatedAtUtc)
            """, t);
    }

    public async Task DeleteTimeOffAsync(Guid providerId, Guid id)
    {
        using var conn = await _db.CreateOpenConnectionAsync();
        await conn.ExecuteAsync(
            "DELETE FROM dbo.ProviderTimeOff WHERE Id = @id AND ProviderId = @providerId",
            new { id, providerId });
    }
}
