using Dapper;
using NamFix.Shared.Domain;
using NamFix.Shared.Dtos;

namespace NamFix.Application.Data.Repositories;

public interface IReviewRepository
{
    Task InsertAsync(Review review);
    Task<IReadOnlyList<ReviewDto>> GetForProviderAsync(Guid providerId);

    /// <summary>True if this client has a completed platform transaction with the provider (verified-review flag).</summary>
    Task<bool> HasCompletedTransactionAsync(Guid providerId, Guid clientUserId);
}

public sealed class ReviewRepository : IReviewRepository
{
    private readonly IDbConnectionFactory _db;
    public ReviewRepository(IDbConnectionFactory db) => _db = db;

    public async Task InsertAsync(Review review)
    {
        using var conn = await _db.CreateOpenConnectionAsync();
        await conn.ExecuteAsync(
            """
            INSERT INTO dbo.Reviews (Id, ProviderId, ClientUserId, Rating, Comment, IsVerified, CreatedAtUtc)
            VALUES (@Id, @ProviderId, @ClientUserId, @Rating, @Comment, @IsVerified, @CreatedAtUtc)
            """, review);
    }

    public async Task<IReadOnlyList<ReviewDto>> GetForProviderAsync(Guid providerId)
    {
        using var conn = await _db.CreateOpenConnectionAsync();
        return (await conn.QueryAsync<ReviewDto>(
            """
            SELECT r.Id, r.ProviderId, u.FullName AS ClientName, r.Rating, r.Comment, r.IsVerified, r.CreatedAtUtc
            FROM dbo.Reviews r
            JOIN dbo.Users u ON u.Id = r.ClientUserId
            WHERE r.ProviderId = @providerId
            ORDER BY r.CreatedAtUtc DESC
            """, new { providerId })).AsList();
    }

    public async Task<bool> HasCompletedTransactionAsync(Guid providerId, Guid clientUserId)
    {
        using var conn = await _db.CreateOpenConnectionAsync();
        return await conn.ExecuteScalarAsync<bool>(
            """
            SELECT CASE WHEN EXISTS (
                SELECT 1 FROM dbo.Transactions
                WHERE ProviderId = @providerId AND ClientUserId = @clientUserId
                  AND Status IN (1, 2)  -- Held or PaidOut
            ) THEN 1 ELSE 0 END
            """, new { providerId, clientUserId });
    }
}
