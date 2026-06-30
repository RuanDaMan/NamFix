using Dapper;
using NamFix.Shared.Domain;
using NamFix.Shared.Dtos;

namespace NamFix.Application.Data.Repositories;

public interface IBookingRepository
{
    Task InsertAsync(Booking booking);
    Task UpdateAsync(Booking booking);

    /// <summary>The raw entity, for the service's authorization + state-machine logic.</summary>
    Task<Booking?> GetByIdAsync(Guid id);

    /// <summary>The display shape (names + has-invoice flag) for a single booking.</summary>
    Task<BookingDto?> GetDtoByIdAsync(Guid id);

    /// <summary>All bookings where the user is either the client or the provider, newest first.</summary>
    Task<List<BookingDto>> ListDtosForUserAsync(Guid userId);

    // ---- Chat ----
    Task InsertMessageAsync(BookingMessage message);
    Task<BookingMessageDto?> GetMessageDtoAsync(Guid messageId);
    Task<List<BookingMessageDto>> ListMessageDtosAsync(Guid bookingId);

    // ---- Invoice attachment (one current file per booking) ----
    Task ReplaceInvoiceAsync(BookingAttachment attachment);
    Task<BookingAttachment?> GetInvoiceAsync(Guid bookingId);
}

public sealed class BookingRepository : IBookingRepository
{
    private readonly IDbConnectionFactory _db;
    public BookingRepository(IDbConnectionFactory db) => _db = db;

    // Display projection: booking columns + the two participant display names + a has-invoice flag.
    private const string DtoSelect =
        """
        SELECT b.Id, b.ProviderId, b.ProviderUserId, pr.BusinessName AS ProviderBusinessName,
               b.ClientUserId, cu.FullName AS ClientName, b.CategoryId, b.ServiceDescription, b.Status,
               b.ProposedStartUtc, b.ProposedByUserId, b.ConfirmedStartUtc,
               b.LocationText, b.LocationLat, b.LocationLng,
               b.InvoiceAmount, b.InvoiceNotes, b.Currency, b.TransactionId,
               b.CreatedAtUtc, b.UpdatedAtUtc,
               CAST(CASE WHEN EXISTS (SELECT 1 FROM dbo.BookingAttachments a WHERE a.BookingId = b.Id)
                         THEN 1 ELSE 0 END AS BIT) AS HasInvoiceFile
        FROM dbo.Bookings b
        JOIN dbo.Users cu ON cu.Id = b.ClientUserId
        JOIN dbo.Providers pr ON pr.Id = b.ProviderId
        """;

    public async Task InsertAsync(Booking b)
    {
        using var conn = await _db.CreateOpenConnectionAsync();
        await conn.ExecuteAsync(
            """
            INSERT INTO dbo.Bookings
                (Id, ProviderId, ProviderUserId, ClientUserId, CategoryId, ServiceDescription, Status,
                 ProposedStartUtc, ProposedByUserId, ConfirmedStartUtc, LocationText, LocationLat, LocationLng,
                 InvoiceAmount, InvoiceNotes, Currency, TransactionId, CreatedAtUtc, UpdatedAtUtc)
            VALUES
                (@Id, @ProviderId, @ProviderUserId, @ClientUserId, @CategoryId, @ServiceDescription, @Status,
                 @ProposedStartUtc, @ProposedByUserId, @ConfirmedStartUtc, @LocationText, @LocationLat, @LocationLng,
                 @InvoiceAmount, @InvoiceNotes, @Currency, @TransactionId, @CreatedAtUtc, @UpdatedAtUtc)
            """,
            new
            {
                b.Id, b.ProviderId, b.ProviderUserId, b.ClientUserId, b.CategoryId, b.ServiceDescription,
                Status = (int)b.Status, b.ProposedStartUtc, b.ProposedByUserId, b.ConfirmedStartUtc,
                b.LocationText, b.LocationLat, b.LocationLng, b.InvoiceAmount, b.InvoiceNotes, b.Currency,
                b.TransactionId, b.CreatedAtUtc, b.UpdatedAtUtc
            });
    }

    public async Task UpdateAsync(Booking b)
    {
        using var conn = await _db.CreateOpenConnectionAsync();
        await conn.ExecuteAsync(
            """
            UPDATE dbo.Bookings SET
                Status = @Status,
                ProposedStartUtc = @ProposedStartUtc,
                ProposedByUserId = @ProposedByUserId,
                ConfirmedStartUtc = @ConfirmedStartUtc,
                LocationText = @LocationText, LocationLat = @LocationLat, LocationLng = @LocationLng,
                InvoiceAmount = @InvoiceAmount, InvoiceNotes = @InvoiceNotes,
                TransactionId = @TransactionId,
                UpdatedAtUtc = SYSUTCDATETIME()
            WHERE Id = @Id
            """,
            new
            {
                b.Id, Status = (int)b.Status, b.ProposedStartUtc, b.ProposedByUserId, b.ConfirmedStartUtc,
                b.LocationText, b.LocationLat, b.LocationLng, b.InvoiceAmount, b.InvoiceNotes, b.TransactionId
            });
    }

    public async Task<Booking?> GetByIdAsync(Guid id)
    {
        using var conn = await _db.CreateOpenConnectionAsync();
        return await conn.QuerySingleOrDefaultAsync<Booking>(
            "SELECT * FROM dbo.Bookings WHERE Id = @id", new { id });
    }

    public async Task<BookingDto?> GetDtoByIdAsync(Guid id)
    {
        using var conn = await _db.CreateOpenConnectionAsync();
        return await conn.QuerySingleOrDefaultAsync<BookingDto>(
            $"{DtoSelect} WHERE b.Id = @id", new { id });
    }

    public async Task<List<BookingDto>> ListDtosForUserAsync(Guid userId)
    {
        using var conn = await _db.CreateOpenConnectionAsync();
        return (await conn.QueryAsync<BookingDto>(
            $"{DtoSelect} WHERE b.ClientUserId = @userId OR b.ProviderUserId = @userId ORDER BY b.UpdatedAtUtc DESC",
            new { userId })).AsList();
    }

    public async Task InsertMessageAsync(BookingMessage m)
    {
        using var conn = await _db.CreateOpenConnectionAsync();
        await conn.ExecuteAsync(
            """
            INSERT INTO dbo.BookingMessages (Id, BookingId, SenderUserId, Body, CreatedAtUtc)
            VALUES (@Id, @BookingId, @SenderUserId, @Body, @CreatedAtUtc)
            """,
            new { m.Id, m.BookingId, m.SenderUserId, m.Body, m.CreatedAtUtc });
    }

    public async Task<BookingMessageDto?> GetMessageDtoAsync(Guid messageId)
    {
        using var conn = await _db.CreateOpenConnectionAsync();
        return await conn.QuerySingleOrDefaultAsync<BookingMessageDto>(
            """
            SELECT m.Id, m.BookingId, m.SenderUserId, u.FullName AS SenderName, m.Body, m.CreatedAtUtc
            FROM dbo.BookingMessages m
            JOIN dbo.Users u ON u.Id = m.SenderUserId
            WHERE m.Id = @messageId
            """, new { messageId });
    }

    public async Task<List<BookingMessageDto>> ListMessageDtosAsync(Guid bookingId)
    {
        using var conn = await _db.CreateOpenConnectionAsync();
        return (await conn.QueryAsync<BookingMessageDto>(
            """
            SELECT m.Id, m.BookingId, m.SenderUserId, u.FullName AS SenderName, m.Body, m.CreatedAtUtc
            FROM dbo.BookingMessages m
            JOIN dbo.Users u ON u.Id = m.SenderUserId
            WHERE m.BookingId = @bookingId
            ORDER BY m.CreatedAtUtc
            """, new { bookingId })).AsList();
    }

    public async Task ReplaceInvoiceAsync(BookingAttachment a)
    {
        using var conn = await _db.CreateOpenConnectionAsync();
        using var tx = conn.BeginTransaction();
        await conn.ExecuteAsync(
            "DELETE FROM dbo.BookingAttachments WHERE BookingId = @bookingId", new { bookingId = a.BookingId }, tx);
        await conn.ExecuteAsync(
            """
            INSERT INTO dbo.BookingAttachments (Id, BookingId, UploadedByUserId, FileName, ContentType, Content, CreatedAtUtc)
            VALUES (@Id, @BookingId, @UploadedByUserId, @FileName, @ContentType, @Content, @CreatedAtUtc)
            """,
            new { a.Id, a.BookingId, a.UploadedByUserId, a.FileName, a.ContentType, a.Content, a.CreatedAtUtc }, tx);
        tx.Commit();
    }

    public async Task<BookingAttachment?> GetInvoiceAsync(Guid bookingId)
    {
        using var conn = await _db.CreateOpenConnectionAsync();
        return await conn.QuerySingleOrDefaultAsync<BookingAttachment>(
            "SELECT TOP 1 * FROM dbo.BookingAttachments WHERE BookingId = @bookingId ORDER BY CreatedAtUtc DESC",
            new { bookingId });
    }
}
