using Dapper;
using NamFix.Shared.Domain;
using NamFix.Shared.Dtos;
using NamFix.Shared.Enums;

namespace NamFix.Application.Data.Repositories;

public interface ISupportRepository
{
    Task InsertTicketAsync(SupportTicket ticket);
    Task UpdateTicketAsync(SupportTicket ticket);

    /// <summary>The raw entity, for the service's authorization + state-machine logic.</summary>
    Task<SupportTicket?> GetTicketByIdAsync(Guid id);

    /// <summary>The display shape (requester name/email + counts) for a single ticket.</summary>
    Task<SupportTicketDto?> GetTicketDtoAsync(Guid id);

    /// <summary>All tickets raised by a single user, newest activity first.</summary>
    Task<List<SupportTicketDto>> ListTicketDtosForUserAsync(Guid userId);

    /// <summary>The admin queue: all tickets, optionally filtered by status/priority, newest activity first.</summary>
    Task<List<SupportTicketDto>> ListAllTicketDtosAsync(TicketStatus? status, SupportPriority? priority);

    // ---- Thread ----
    Task InsertMessageAsync(SupportMessage message);
    Task<SupportMessageDto?> GetMessageDtoAsync(Guid messageId);
    Task<List<SupportMessageDto>> ListMessageDtosAsync(Guid ticketId);

    // ---- Attachments (many per ticket, optionally tied to a message) ----
    Task InsertAttachmentAsync(SupportAttachment attachment);
    Task<SupportAttachment?> GetAttachmentAsync(Guid id);
}

public sealed class SupportRepository : ISupportRepository
{
    private readonly IDbConnectionFactory _db;
    public SupportRepository(IDbConnectionFactory db) => _db = db;

    // Ticket display projection: ticket columns + requester display details + activity counts.
    private const string DtoSelect =
        """
        SELECT t.Id, t.RequesterUserId, u.FullName AS RequesterName, u.Email AS RequesterEmail,
               t.Subject, t.Category, t.Priority, t.Status,
               (SELECT COUNT(*) FROM dbo.SupportMessages m WHERE m.TicketId = t.Id) AS MessageCount,
               CAST(CASE WHEN EXISTS (SELECT 1 FROM dbo.SupportAttachments a WHERE a.TicketId = t.Id)
                         THEN 1 ELSE 0 END AS BIT) AS HasAttachments,
               t.CreatedAtUtc, t.UpdatedAtUtc, t.LastMessageAtUtc, t.ClosedAtUtc
        FROM dbo.SupportTickets t
        JOIN dbo.Users u ON u.Id = t.RequesterUserId
        """;

    public async Task InsertTicketAsync(SupportTicket t)
    {
        using var conn = await _db.CreateOpenConnectionAsync();
        await conn.ExecuteAsync(
            """
            INSERT INTO dbo.SupportTickets
                (Id, RequesterUserId, Subject, Category, Priority, Status,
                 CreatedAtUtc, UpdatedAtUtc, LastMessageAtUtc, ClosedAtUtc)
            VALUES
                (@Id, @RequesterUserId, @Subject, @Category, @Priority, @Status,
                 @CreatedAtUtc, @UpdatedAtUtc, @LastMessageAtUtc, @ClosedAtUtc)
            """,
            new
            {
                t.Id, t.RequesterUserId, t.Subject, Category = (int)t.Category, Priority = (int)t.Priority,
                Status = (int)t.Status, t.CreatedAtUtc, t.UpdatedAtUtc, t.LastMessageAtUtc, t.ClosedAtUtc
            });
    }

    public async Task UpdateTicketAsync(SupportTicket t)
    {
        using var conn = await _db.CreateOpenConnectionAsync();
        await conn.ExecuteAsync(
            """
            UPDATE dbo.SupportTickets SET
                Subject = @Subject,
                Category = @Category,
                Priority = @Priority,
                Status = @Status,
                LastMessageAtUtc = @LastMessageAtUtc,
                ClosedAtUtc = @ClosedAtUtc,
                UpdatedAtUtc = SYSUTCDATETIME()
            WHERE Id = @Id
            """,
            new
            {
                t.Id, t.Subject, Category = (int)t.Category, Priority = (int)t.Priority,
                Status = (int)t.Status, t.LastMessageAtUtc, t.ClosedAtUtc
            });
    }

    public async Task<SupportTicket?> GetTicketByIdAsync(Guid id)
    {
        using var conn = await _db.CreateOpenConnectionAsync();
        return await conn.QuerySingleOrDefaultAsync<SupportTicket>(
            "SELECT * FROM dbo.SupportTickets WHERE Id = @id", new { id });
    }

    public async Task<SupportTicketDto?> GetTicketDtoAsync(Guid id)
    {
        using var conn = await _db.CreateOpenConnectionAsync();
        return await conn.QuerySingleOrDefaultAsync<SupportTicketDto>(
            $"{DtoSelect} WHERE t.Id = @id", new { id });
    }

    public async Task<List<SupportTicketDto>> ListTicketDtosForUserAsync(Guid userId)
    {
        using var conn = await _db.CreateOpenConnectionAsync();
        return (await conn.QueryAsync<SupportTicketDto>(
            $"{DtoSelect} WHERE t.RequesterUserId = @userId ORDER BY t.LastMessageAtUtc DESC",
            new { userId })).AsList();
    }

    public async Task<List<SupportTicketDto>> ListAllTicketDtosAsync(TicketStatus? status, SupportPriority? priority)
    {
        using var conn = await _db.CreateOpenConnectionAsync();
        return (await conn.QueryAsync<SupportTicketDto>(
            $"""
             {DtoSelect}
             WHERE (@status IS NULL OR t.Status = @status)
               AND (@priority IS NULL OR t.Priority = @priority)
             ORDER BY t.LastMessageAtUtc DESC
             """,
            new { status = (int?)status, priority = (int?)priority })).AsList();
    }

    public async Task InsertMessageAsync(SupportMessage m)
    {
        using var conn = await _db.CreateOpenConnectionAsync();
        await conn.ExecuteAsync(
            """
            INSERT INTO dbo.SupportMessages (Id, TicketId, SenderUserId, Body, IsSystem, CreatedAtUtc)
            VALUES (@Id, @TicketId, @SenderUserId, @Body, @IsSystem, @CreatedAtUtc)
            """,
            new { m.Id, m.TicketId, m.SenderUserId, m.Body, m.IsSystem, m.CreatedAtUtc });
    }

    // A message plus the admin flag of its sender; attachments are stitched on separately below.
    private const string MessageSelect =
        """
        SELECT m.Id, m.TicketId, m.SenderUserId, u.FullName AS SenderName,
               CAST(CASE WHEN u.Role = 3 THEN 1 ELSE 0 END AS BIT) AS SenderIsAdmin,
               m.IsSystem, m.Body, m.CreatedAtUtc
        FROM dbo.SupportMessages m
        JOIN dbo.Users u ON u.Id = m.SenderUserId
        """;

    private const string AttachmentMetaSelect =
        "SELECT Id, TicketId, MessageId, FileName, ContentType, SizeBytes FROM dbo.SupportAttachments";

    public async Task<SupportMessageDto?> GetMessageDtoAsync(Guid messageId)
    {
        using var conn = await _db.CreateOpenConnectionAsync();
        var message = await conn.QuerySingleOrDefaultAsync<SupportMessageDto>(
            $"{MessageSelect} WHERE m.Id = @messageId", new { messageId });
        if (message is null) return null;

        var attachments = (await conn.QueryAsync<SupportAttachmentDto>(
            $"{AttachmentMetaSelect} WHERE MessageId = @messageId", new { messageId })).AsList();
        return message with { Attachments = attachments };
    }

    public async Task<List<SupportMessageDto>> ListMessageDtosAsync(Guid ticketId)
    {
        using var conn = await _db.CreateOpenConnectionAsync();
        var messages = (await conn.QueryAsync<SupportMessageDto>(
            $"{MessageSelect} WHERE m.TicketId = @ticketId ORDER BY m.CreatedAtUtc", new { ticketId })).AsList();

        var attachments = (await conn.QueryAsync<SupportAttachmentDto>(
            $"{AttachmentMetaSelect} WHERE TicketId = @ticketId", new { ticketId })).AsList();

        var byMessage = attachments
            .Where(a => a.MessageId is not null)
            .GroupBy(a => a.MessageId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        return messages
            .Select(m => byMessage.TryGetValue(m.Id, out var list) ? m with { Attachments = list } : m)
            .ToList();
    }

    public async Task InsertAttachmentAsync(SupportAttachment a)
    {
        using var conn = await _db.CreateOpenConnectionAsync();
        await conn.ExecuteAsync(
            """
            INSERT INTO dbo.SupportAttachments
                (Id, TicketId, MessageId, UploadedByUserId, FileName, ContentType, Content, SizeBytes, CreatedAtUtc)
            VALUES
                (@Id, @TicketId, @MessageId, @UploadedByUserId, @FileName, @ContentType, @Content, @SizeBytes, @CreatedAtUtc)
            """,
            new { a.Id, a.TicketId, a.MessageId, a.UploadedByUserId, a.FileName, a.ContentType, a.Content, a.SizeBytes, a.CreatedAtUtc });
    }

    public async Task<SupportAttachment?> GetAttachmentAsync(Guid id)
    {
        using var conn = await _db.CreateOpenConnectionAsync();
        return await conn.QuerySingleOrDefaultAsync<SupportAttachment>(
            "SELECT * FROM dbo.SupportAttachments WHERE Id = @id", new { id });
    }
}
