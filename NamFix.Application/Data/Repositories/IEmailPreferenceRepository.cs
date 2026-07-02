using Dapper;
using NamFix.Shared.Domain;
using NamFix.Shared.Enums;

namespace NamFix.Application.Data.Repositories;

public interface IEmailPreferenceRepository
{
    /// <summary>Stored preference rows for a user. Absent categories default to subscribed.</summary>
    Task<List<UserEmailPreference>> ListForUserAsync(Guid userId);

    /// <summary>True unless the user has an explicit opt-out row for the category.</summary>
    Task<bool> IsSubscribedAsync(Guid userId, EmailNotificationCategory category);

    Task UpsertAsync(Guid userId, EmailNotificationCategory category, bool isSubscribed);
}

public sealed class EmailPreferenceRepository : IEmailPreferenceRepository
{
    private readonly IDbConnectionFactory _db;
    public EmailPreferenceRepository(IDbConnectionFactory db) => _db = db;

    public async Task<List<UserEmailPreference>> ListForUserAsync(Guid userId)
    {
        using var conn = await _db.CreateOpenConnectionAsync();
        return (await conn.QueryAsync<UserEmailPreference>(
            "SELECT UserId, Category, IsSubscribed FROM dbo.UserEmailPreferences WHERE UserId = @userId",
            new { userId })).AsList();
    }

    public async Task<bool> IsSubscribedAsync(Guid userId, EmailNotificationCategory category)
    {
        using var conn = await _db.CreateOpenConnectionAsync();
        var value = await conn.QuerySingleOrDefaultAsync<bool?>(
            "SELECT IsSubscribed FROM dbo.UserEmailPreferences WHERE UserId = @userId AND Category = @category",
            new { userId, category = (int)category });
        return value ?? true; // no row == subscribed by default
    }

    public async Task UpsertAsync(Guid userId, EmailNotificationCategory category, bool isSubscribed)
    {
        using var conn = await _db.CreateOpenConnectionAsync();
        await conn.ExecuteAsync(
            """
            MERGE dbo.UserEmailPreferences AS t
            USING (SELECT @userId AS UserId, @category AS Category) AS s
              ON t.UserId = s.UserId AND t.Category = s.Category
            WHEN MATCHED THEN UPDATE SET IsSubscribed = @isSubscribed
            WHEN NOT MATCHED THEN INSERT (UserId, Category, IsSubscribed) VALUES (@userId, @category, @isSubscribed);
            """,
            new { userId, category = (int)category, isSubscribed });
    }
}
