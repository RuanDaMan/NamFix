using Dapper;
using NamFix.Shared.Domain;
using NamFix.Shared.Dtos;

namespace NamFix.Application.Data.Repositories;

public interface IInboxRepository
{
    Task<bool> ExistsByMessageIdAsync(string messageId);
    Task InsertAsync(InboxMessage message);
    Task<List<InboxMessageDto>> ListAsync(int take = 100);
    Task<InboxMessageDetailDto?> GetAsync(Guid id);
}

public sealed class InboxRepository : IInboxRepository
{
    private readonly IDbConnectionFactory _db;
    public InboxRepository(IDbConnectionFactory db) => _db = db;

    public async Task<bool> ExistsByMessageIdAsync(string messageId)
    {
        using var conn = await _db.CreateOpenConnectionAsync();
        return await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM dbo.InboxMessages WHERE MessageId = @messageId", new { messageId }) > 0;
    }

    public async Task InsertAsync(InboxMessage m)
    {
        using var conn = await _db.CreateOpenConnectionAsync();
        await conn.ExecuteAsync(
            """
            INSERT INTO dbo.InboxMessages (Id, MessageId, FromName, FromAddress, Subject, ReceivedAtUtc, TextBody, HtmlBody, FetchedAtUtc)
            VALUES (@Id, @MessageId, @FromName, @FromAddress, @Subject, @ReceivedAtUtc, @TextBody, @HtmlBody, @FetchedAtUtc)
            """, m);
    }

    public async Task<List<InboxMessageDto>> ListAsync(int take = 100)
    {
        using var conn = await _db.CreateOpenConnectionAsync();
        return (await conn.QueryAsync<InboxMessageDto>(
            """
            SELECT TOP (@take) Id, FromName, FromAddress, Subject, ReceivedAtUtc,
                   CAST(CASE WHEN HtmlBody IS NOT NULL AND LEN(HtmlBody) > 0 THEN 1 ELSE 0 END AS BIT) AS HasHtml
            FROM dbo.InboxMessages
            ORDER BY ReceivedAtUtc DESC
            """, new { take })).AsList();
    }

    public async Task<InboxMessageDetailDto?> GetAsync(Guid id)
    {
        using var conn = await _db.CreateOpenConnectionAsync();
        return await conn.QuerySingleOrDefaultAsync<InboxMessageDetailDto>(
            """
            SELECT Id, FromName, FromAddress, Subject, ReceivedAtUtc, TextBody, HtmlBody
            FROM dbo.InboxMessages WHERE Id = @id
            """, new { id });
    }
}
