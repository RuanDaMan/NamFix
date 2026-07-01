using Dapper;
using NamFix.Shared.Domain;
using NamFix.Shared.Dtos;

namespace NamFix.Application.Data.Repositories;

public interface INotificationRepository
{
    Task InsertAsync(Notification notification);
    Task<List<NotificationDto>> ListForUserAsync(Guid userId, int take = 30);
    Task MarkReadAsync(Guid id, Guid userId);
    Task MarkAllReadAsync(Guid userId);
}

public sealed class NotificationRepository : INotificationRepository
{
    private readonly IDbConnectionFactory _db;
    public NotificationRepository(IDbConnectionFactory db) => _db = db;

    public async Task InsertAsync(Notification n)
    {
        using var conn = await _db.CreateOpenConnectionAsync();
        await conn.ExecuteAsync(
            """
            INSERT INTO dbo.Notifications (Id, UserId, JobRequestId, TicketId, Type, Message, IsRead, CreatedAtUtc)
            VALUES (@Id, @UserId, @JobRequestId, @TicketId, @Type, @Message, @IsRead, @CreatedAtUtc)
            """,
            new { n.Id, n.UserId, n.JobRequestId, n.TicketId, Type = (int)n.Type, n.Message, n.IsRead, n.CreatedAtUtc });
    }

    public async Task<List<NotificationDto>> ListForUserAsync(Guid userId, int take = 30)
    {
        using var conn = await _db.CreateOpenConnectionAsync();
        return (await conn.QueryAsync<NotificationDto>(
            """
            SELECT TOP (@take) Id, JobRequestId, TicketId, Type, Message, IsRead, CreatedAtUtc
            FROM dbo.Notifications
            WHERE UserId = @userId
            ORDER BY CreatedAtUtc DESC
            """, new { userId, take })).AsList();
    }

    public async Task MarkReadAsync(Guid id, Guid userId)
    {
        using var conn = await _db.CreateOpenConnectionAsync();
        await conn.ExecuteAsync(
            "UPDATE dbo.Notifications SET IsRead = 1 WHERE Id = @id AND UserId = @userId",
            new { id, userId });
    }

    public async Task MarkAllReadAsync(Guid userId)
    {
        using var conn = await _db.CreateOpenConnectionAsync();
        await conn.ExecuteAsync(
            "UPDATE dbo.Notifications SET IsRead = 1 WHERE UserId = @userId AND IsRead = 0",
            new { userId });
    }
}
