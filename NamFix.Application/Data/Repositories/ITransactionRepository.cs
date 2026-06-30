using Dapper;
using NamFix.Shared.Domain;
using NamFix.Shared.Dtos;
using NamFix.Shared.Enums;

namespace NamFix.Application.Data.Repositories;

public interface ITransactionRepository
{
    Task InsertAsync(Transaction transaction);
    Task<Transaction?> GetByIdAsync(Guid id);
    Task UpdateStatusAsync(Guid id, TransactionStatus status, string? paymentReference);
    Task<ProviderEarningsDto> GetProviderEarningsAsync(Guid providerId);
    Task<RevenueReportDto> GetRevenueReportAsync(DateTime fromUtc, DateTime toUtc);
}

public sealed class TransactionRepository : ITransactionRepository
{
    private readonly IDbConnectionFactory _db;
    public TransactionRepository(IDbConnectionFactory db) => _db = db;

    public async Task InsertAsync(Transaction t)
    {
        using var conn = await _db.CreateOpenConnectionAsync();
        await conn.ExecuteAsync(
            """
            INSERT INTO dbo.Transactions
                (Id, ProviderId, ClientUserId, CategoryId, GrossAmount, CommissionRate,
                 CommissionAmount, NetPayoutAmount, Status, Currency, PaymentReference,
                 CreatedAtUtc, HeldAtUtc, PaidOutAtUtc)
            VALUES
                (@Id, @ProviderId, @ClientUserId, @CategoryId, @GrossAmount, @CommissionRate,
                 @CommissionAmount, @NetPayoutAmount, @Status, @Currency, @PaymentReference,
                 @CreatedAtUtc, @HeldAtUtc, @PaidOutAtUtc)
            """,
            new
            {
                t.Id, t.ProviderId, t.ClientUserId, t.CategoryId, t.GrossAmount, t.CommissionRate,
                t.CommissionAmount, t.NetPayoutAmount, Status = (int)t.Status, t.Currency,
                t.PaymentReference, t.CreatedAtUtc, t.HeldAtUtc, t.PaidOutAtUtc
            });
    }

    public async Task<Transaction?> GetByIdAsync(Guid id)
    {
        using var conn = await _db.CreateOpenConnectionAsync();
        return await conn.QuerySingleOrDefaultAsync<Transaction>(
            "SELECT * FROM dbo.Transactions WHERE Id = @id", new { id });
    }

    public async Task UpdateStatusAsync(Guid id, TransactionStatus status, string? paymentReference)
    {
        using var conn = await _db.CreateOpenConnectionAsync();
        await conn.ExecuteAsync(
            """
            UPDATE dbo.Transactions
            SET Status = @status,
                PaymentReference = COALESCE(@paymentReference, PaymentReference),
                HeldAtUtc   = CASE WHEN @status = @held   THEN SYSUTCDATETIME() ELSE HeldAtUtc END,
                PaidOutAtUtc = CASE WHEN @status = @paid  THEN SYSUTCDATETIME() ELSE PaidOutAtUtc END
            WHERE Id = @id
            """,
            new
            {
                id, status = (int)status, paymentReference,
                held = (int)TransactionStatus.Held, paid = (int)TransactionStatus.PaidOut
            });
    }

    public async Task<ProviderEarningsDto> GetProviderEarningsAsync(Guid providerId)
    {
        using var conn = await _db.CreateOpenConnectionAsync();
        return await conn.QuerySingleAsync<ProviderEarningsDto>(
            """
            SELECT
                ISNULL(SUM(GrossAmount), 0)      AS TotalGross,
                ISNULL(SUM(CommissionAmount), 0) AS TotalCommission,
                ISNULL(SUM(CASE WHEN Status = 2 THEN NetPayoutAmount ELSE 0 END), 0) AS TotalNetPayout,
                ISNULL(SUM(CASE WHEN Status = 1 THEN NetPayoutAmount ELSE 0 END), 0) AS PendingPayout,
                COUNT(*)                          AS TransactionCount
            FROM dbo.Transactions
            WHERE ProviderId = @providerId AND Status IN (1, 2)  -- Held or PaidOut
            """, new { providerId });
    }

    public async Task<RevenueReportDto> GetRevenueReportAsync(DateTime fromUtc, DateTime toUtc)
    {
        using var conn = await _db.CreateOpenConnectionAsync();
        var args = new { fromUtc, toUtc };

        const string sql =
            """
            -- Totals (Held + PaidOut count as captured commission)
            SELECT
                ISNULL(SUM(GrossAmount), 0)      AS TotalGross,
                ISNULL(SUM(CommissionAmount), 0) AS TotalCommission,
                COUNT(*)                          AS TransactionCount
            FROM dbo.Transactions
            WHERE Status IN (1, 2) AND CreatedAtUtc >= @fromUtc AND CreatedAtUtc < @toUtc;

            -- By category
            SELECT ISNULL(c.Name, 'Uncategorised') AS Label,
                   ISNULL(SUM(tr.CommissionAmount), 0) AS Commission,
                   ISNULL(SUM(tr.GrossAmount), 0) AS Gross,
                   COUNT(*) AS Count
            FROM dbo.Transactions tr
            LEFT JOIN dbo.Categories c ON c.Id = tr.CategoryId
            WHERE tr.Status IN (1, 2) AND tr.CreatedAtUtc >= @fromUtc AND tr.CreatedAtUtc < @toUtc
            GROUP BY c.Name
            ORDER BY Commission DESC;

            -- By town (provider's primary town)
            SELECT ISNULL(t.Name, 'Unknown') AS Label,
                   ISNULL(SUM(tr.CommissionAmount), 0) AS Commission,
                   ISNULL(SUM(tr.GrossAmount), 0) AS Gross,
                   COUNT(*) AS Count
            FROM dbo.Transactions tr
            JOIN dbo.Providers pr ON pr.Id = tr.ProviderId
            LEFT JOIN dbo.Towns t ON t.Id = pr.PrimaryTownId
            WHERE tr.Status IN (1, 2) AND tr.CreatedAtUtc >= @fromUtc AND tr.CreatedAtUtc < @toUtc
            GROUP BY t.Name
            ORDER BY Commission DESC;
            """;

        using var multi = await conn.QueryMultipleAsync(sql, args);
        var totals = await multi.ReadSingleAsync<(decimal TotalGross, decimal TotalCommission, int TransactionCount)>();
        var byCategory = (await multi.ReadAsync<RevenueBreakdownRow>()).AsList();
        var byTown = (await multi.ReadAsync<RevenueBreakdownRow>()).AsList();

        return new RevenueReportDto
        {
            FromUtc = fromUtc,
            ToUtc = toUtc,
            TotalGross = totals.TotalGross,
            TotalCommission = totals.TotalCommission,
            TransactionCount = totals.TransactionCount,
            ByCategory = byCategory,
            ByTown = byTown
        };
    }
}
