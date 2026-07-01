using Dapper;

namespace NamFix.Application.Data.Repositories;

public interface IPlatformSettingsRepository
{
    Task<Dictionary<string, string>> GetAllAsync();
    Task SetAsync(string key, string value);
}

public sealed class PlatformSettingsRepository : IPlatformSettingsRepository
{
    private readonly IDbConnectionFactory _db;
    public PlatformSettingsRepository(IDbConnectionFactory db) => _db = db;

    public async Task<Dictionary<string, string>> GetAllAsync()
    {
        using var conn = await _db.CreateOpenConnectionAsync();
        var rows = await conn.QueryAsync<(string Key, string Value)>(
            "SELECT [Key], [Value] FROM dbo.PlatformSettings");
        return rows.ToDictionary(r => r.Key, r => r.Value, StringComparer.OrdinalIgnoreCase);
    }

    public async Task SetAsync(string key, string value)
    {
        using var conn = await _db.CreateOpenConnectionAsync();
        await conn.ExecuteAsync(
            """
            MERGE dbo.PlatformSettings AS target
            USING (SELECT @key AS [Key]) AS src ON target.[Key] = src.[Key]
            WHEN MATCHED THEN UPDATE SET [Value] = @value, UpdatedAtUtc = SYSUTCDATETIME()
            WHEN NOT MATCHED THEN INSERT ([Key], [Value]) VALUES (@key, @value);
            """, new { key, value });
    }
}
