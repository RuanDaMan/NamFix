using Dapper;
using NamFix.Shared.Domain;
using NamFix.Shared.Dtos;
using NamFix.Shared.Enums;

namespace NamFix.Application.Data.Repositories;

public interface IJobRepository
{
    Task InsertAsync(JobRequest job);
    Task UpdateAsync(JobRequest job);

    /// <summary>The raw entity, for the service's authorization + state-machine logic.</summary>
    Task<JobRequest?> GetByIdAsync(Guid id);

    /// <summary>The display shape (names + flags) for a single job.</summary>
    Task<JobRequestDto?> GetDtoByIdAsync(Guid id);

    /// <summary>Jobs the user owns (as client, including still-unmatched broadcasts) or is the chosen provider for.</summary>
    Task<List<JobRequestDto>> ListDtosForUserAsync(Guid userId);

    /// <summary>Open jobs a provider was invited to / matches and can still respond to.</summary>
    Task<List<JobRequestDto>> ListOpenForProviderAsync(Guid providerUserId);

    // ---- Responses (one row per invited provider) ----
    Task InsertResponseAsync(JobRequestResponse response);
    Task UpdateResponseAsync(JobRequestResponse response);
    Task<JobRequestResponse?> GetResponseByIdAsync(Guid responseId);
    Task<JobRequestResponse?> GetResponseForProviderAsync(Guid jobRequestId, Guid providerUserId);
    Task<List<JobResponseDto>> ListResponseDtosAsync(Guid jobRequestId);
    /// <summary>Mark every response other than the accepted one as Rejected (client picked a quote).</summary>
    Task RejectOtherResponsesAsync(Guid jobRequestId, Guid acceptedResponseId);

    // ---- Chat ----
    Task InsertMessageAsync(JobRequestMessage message);
    Task<JobMessageDto?> GetMessageDtoAsync(Guid messageId);
    Task<List<JobMessageDto>> ListMessageDtosAsync(Guid jobRequestId);

    // ---- Attachments ----
    Task ReplaceInvoiceAsync(JobRequestAttachment attachment);
    Task<JobRequestAttachment?> GetInvoiceAsync(Guid jobRequestId);
    Task AddPhotoAsync(JobRequestAttachment attachment);

    // ---- Calendar ----
    Task<List<BookedSlotDto>> ListBookedSlotsAsync(Guid providerId);
}

public sealed class JobRequestRepository : IJobRepository
{
    private readonly IDbConnectionFactory _db;
    public JobRequestRepository(IDbConnectionFactory db) => _db = db;

    // Display projection: job columns + participant/category/town names + flags + response count.
    // Provider join is LEFT because an unmatched broadcast has no chosen provider yet.
    private const string DtoSelect =
        """
        SELECT j.Id, j.ProviderId, j.ProviderUserId, pr.BusinessName AS ProviderBusinessName,
               j.ClientUserId, cu.FullName AS ClientName,
               j.CategoryId, c.Name AS CategoryName, j.TownId, t.Name AS TownName,
               j.ServiceDescription, j.Status, j.TargetMode, j.Urgency,
               j.ProposedStartUtc, j.ProposedByUserId, j.ConfirmedStartUtc, j.ConfirmedEndUtc,
               j.DurationMinutes, j.QuoteExpiresUtc,
               j.LocationText, j.LocationLat, j.LocationLng,
               j.InvoiceAmount, j.InvoiceNotes, j.Currency, j.TransactionId,
               j.CancelledByUserId, j.WasLateCancellation, j.NoShowByUserId,
               j.CreatedAtUtc, j.UpdatedAtUtc,
               CAST(CASE WHEN EXISTS (SELECT 1 FROM dbo.JobRequestAttachments a
                                      WHERE a.JobRequestId = j.Id AND a.Kind = 0)
                         THEN 1 ELSE 0 END AS BIT) AS HasInvoiceFile,
               (SELECT COUNT(*) FROM dbo.JobRequestResponses rr
                WHERE rr.JobRequestId = j.Id AND rr.Status IN (2, 3, 6)) AS ResponseCount
        FROM dbo.JobRequests j
        JOIN dbo.Users cu ON cu.Id = j.ClientUserId
        LEFT JOIN dbo.Providers pr ON pr.Id = j.ProviderId
        LEFT JOIN dbo.Categories c ON c.Id = j.CategoryId
        LEFT JOIN dbo.Towns t ON t.Id = j.TownId
        """;

    public async Task InsertAsync(JobRequest j)
    {
        using var conn = await _db.CreateOpenConnectionAsync();
        await conn.ExecuteAsync(
            """
            INSERT INTO dbo.JobRequests
                (Id, ProviderId, ProviderUserId, ClientUserId, CategoryId, TownId, ServiceDescription, Status,
                 TargetMode, Urgency, ProposedStartUtc, ProposedByUserId, ConfirmedStartUtc, ConfirmedEndUtc,
                 DurationMinutes, QuoteExpiresUtc, LocationText, LocationLat, LocationLng,
                 InvoiceAmount, InvoiceNotes, Currency, TransactionId, CreatedAtUtc, UpdatedAtUtc)
            VALUES
                (@Id, @ProviderId, @ProviderUserId, @ClientUserId, @CategoryId, @TownId, @ServiceDescription, @Status,
                 @TargetMode, @Urgency, @ProposedStartUtc, @ProposedByUserId, @ConfirmedStartUtc, @ConfirmedEndUtc,
                 @DurationMinutes, @QuoteExpiresUtc, @LocationText, @LocationLat, @LocationLng,
                 @InvoiceAmount, @InvoiceNotes, @Currency, @TransactionId, @CreatedAtUtc, @UpdatedAtUtc)
            """,
            new
            {
                j.Id, j.ProviderId, j.ProviderUserId, j.ClientUserId, j.CategoryId, j.TownId, j.ServiceDescription,
                Status = (int)j.Status, TargetMode = (int)j.TargetMode, Urgency = (int)j.Urgency,
                j.ProposedStartUtc, j.ProposedByUserId, j.ConfirmedStartUtc, j.ConfirmedEndUtc, j.DurationMinutes,
                j.QuoteExpiresUtc, j.LocationText, j.LocationLat, j.LocationLng, j.InvoiceAmount, j.InvoiceNotes,
                j.Currency, j.TransactionId, j.CreatedAtUtc, j.UpdatedAtUtc
            });
    }

    public async Task UpdateAsync(JobRequest j)
    {
        using var conn = await _db.CreateOpenConnectionAsync();
        await conn.ExecuteAsync(
            """
            UPDATE dbo.JobRequests SET
                ProviderId = @ProviderId,
                ProviderUserId = @ProviderUserId,
                Status = @Status,
                ProposedStartUtc = @ProposedStartUtc,
                ProposedByUserId = @ProposedByUserId,
                ConfirmedStartUtc = @ConfirmedStartUtc,
                ConfirmedEndUtc = @ConfirmedEndUtc,
                DurationMinutes = @DurationMinutes,
                LocationText = @LocationText, LocationLat = @LocationLat, LocationLng = @LocationLng,
                InvoiceAmount = @InvoiceAmount, InvoiceNotes = @InvoiceNotes,
                TransactionId = @TransactionId,
                CancelledByUserId = @CancelledByUserId, CancelledAtUtc = @CancelledAtUtc,
                WasLateCancellation = @WasLateCancellation, NoShowByUserId = @NoShowByUserId,
                UpdatedAtUtc = SYSUTCDATETIME()
            WHERE Id = @Id
            """,
            new
            {
                j.Id, j.ProviderId, j.ProviderUserId, Status = (int)j.Status, j.ProposedStartUtc, j.ProposedByUserId,
                j.ConfirmedStartUtc, j.ConfirmedEndUtc, j.DurationMinutes, j.LocationText, j.LocationLat, j.LocationLng,
                j.InvoiceAmount, j.InvoiceNotes, j.TransactionId, j.CancelledByUserId, j.CancelledAtUtc,
                j.WasLateCancellation, j.NoShowByUserId
            });
    }

    public async Task<JobRequest?> GetByIdAsync(Guid id)
    {
        using var conn = await _db.CreateOpenConnectionAsync();
        return await conn.QuerySingleOrDefaultAsync<JobRequest>(
            "SELECT * FROM dbo.JobRequests WHERE Id = @id", new { id });
    }

    public async Task<JobRequestDto?> GetDtoByIdAsync(Guid id)
    {
        using var conn = await _db.CreateOpenConnectionAsync();
        return await conn.QuerySingleOrDefaultAsync<JobRequestDto>(
            $"{DtoSelect} WHERE j.Id = @id", new { id });
    }

    public async Task<List<JobRequestDto>> ListDtosForUserAsync(Guid userId)
    {
        using var conn = await _db.CreateOpenConnectionAsync();
        return (await conn.QueryAsync<JobRequestDto>(
            $"{DtoSelect} WHERE j.ClientUserId = @userId OR j.ProviderUserId = @userId ORDER BY j.UpdatedAtUtc DESC",
            new { userId })).AsList();
    }

    public async Task<List<JobRequestDto>> ListOpenForProviderAsync(Guid providerUserId)
    {
        using var conn = await _db.CreateOpenConnectionAsync();
        // Jobs still gathering quotes (Requested/Quoted) that this provider was invited to and
        // hasn't declined/withdrawn on.
        return (await conn.QueryAsync<JobRequestDto>(
            $"""
            {DtoSelect}
            WHERE j.Status IN (7, 8)
              AND EXISTS (SELECT 1 FROM dbo.JobRequestResponses rr
                          WHERE rr.JobRequestId = j.Id AND rr.ProviderUserId = @providerUserId
                            AND rr.Status IN (0, 1, 2, 3))
            ORDER BY j.Urgency DESC, j.CreatedAtUtc DESC
            """, new { providerUserId })).AsList();
    }

    // ---- Responses ----

    public async Task InsertResponseAsync(JobRequestResponse r)
    {
        using var conn = await _db.CreateOpenConnectionAsync();
        await conn.ExecuteAsync(
            """
            INSERT INTO dbo.JobRequestResponses
                (Id, JobRequestId, ProviderId, ProviderUserId, Status, QuoteAmount, QuoteNote,
                 QuoteExpiresUtc, ProposedStartUtc, CreatedAtUtc, RespondedAtUtc)
            VALUES
                (@Id, @JobRequestId, @ProviderId, @ProviderUserId, @Status, @QuoteAmount, @QuoteNote,
                 @QuoteExpiresUtc, @ProposedStartUtc, @CreatedAtUtc, @RespondedAtUtc)
            """,
            new
            {
                r.Id, r.JobRequestId, r.ProviderId, r.ProviderUserId, Status = (int)r.Status, r.QuoteAmount,
                r.QuoteNote, r.QuoteExpiresUtc, r.ProposedStartUtc, r.CreatedAtUtc, r.RespondedAtUtc
            });
    }

    public async Task UpdateResponseAsync(JobRequestResponse r)
    {
        using var conn = await _db.CreateOpenConnectionAsync();
        await conn.ExecuteAsync(
            """
            UPDATE dbo.JobRequestResponses SET
                Status = @Status, QuoteAmount = @QuoteAmount, QuoteNote = @QuoteNote,
                QuoteExpiresUtc = @QuoteExpiresUtc, ProposedStartUtc = @ProposedStartUtc,
                RespondedAtUtc = @RespondedAtUtc
            WHERE Id = @Id
            """,
            new
            {
                r.Id, Status = (int)r.Status, r.QuoteAmount, r.QuoteNote, r.QuoteExpiresUtc,
                r.ProposedStartUtc, r.RespondedAtUtc
            });
    }

    public async Task<JobRequestResponse?> GetResponseByIdAsync(Guid responseId)
    {
        using var conn = await _db.CreateOpenConnectionAsync();
        return await conn.QuerySingleOrDefaultAsync<JobRequestResponse>(
            "SELECT * FROM dbo.JobRequestResponses WHERE Id = @responseId", new { responseId });
    }

    public async Task<JobRequestResponse?> GetResponseForProviderAsync(Guid jobRequestId, Guid providerUserId)
    {
        using var conn = await _db.CreateOpenConnectionAsync();
        return await conn.QuerySingleOrDefaultAsync<JobRequestResponse>(
            "SELECT * FROM dbo.JobRequestResponses WHERE JobRequestId = @jobRequestId AND ProviderUserId = @providerUserId",
            new { jobRequestId, providerUserId });
    }

    public async Task<List<JobResponseDto>> ListResponseDtosAsync(Guid jobRequestId)
    {
        using var conn = await _db.CreateOpenConnectionAsync();
        return (await conn.QueryAsync<JobResponseDto>(
            """
            SELECT rr.Id, rr.JobRequestId, rr.ProviderId, rr.ProviderUserId,
                   pr.BusinessName AS ProviderBusinessName, pr.RatingAverage AS ProviderRatingAverage,
                   pr.RatingCount AS ProviderRatingCount, pr.AvgResponseMinutes AS ProviderAvgResponseMinutes,
                   rr.Status, rr.QuoteAmount, rr.QuoteNote, rr.QuoteExpiresUtc, rr.ProposedStartUtc,
                   rr.CreatedAtUtc, rr.RespondedAtUtc
            FROM dbo.JobRequestResponses rr
            JOIN dbo.Providers pr ON pr.Id = rr.ProviderId
            WHERE rr.JobRequestId = @jobRequestId AND rr.Status IN (2, 3, 6, 7)
            ORDER BY rr.Status DESC, rr.RespondedAtUtc DESC
            """, new { jobRequestId })).AsList();
    }

    public async Task RejectOtherResponsesAsync(Guid jobRequestId, Guid acceptedResponseId)
    {
        using var conn = await _db.CreateOpenConnectionAsync();
        await conn.ExecuteAsync(
            """
            UPDATE dbo.JobRequestResponses SET Status = 7
            WHERE JobRequestId = @jobRequestId AND Id <> @acceptedResponseId AND Status <> 7
            """, new { jobRequestId, acceptedResponseId });
    }

    // ---- Chat ----

    public async Task InsertMessageAsync(JobRequestMessage m)
    {
        using var conn = await _db.CreateOpenConnectionAsync();
        await conn.ExecuteAsync(
            """
            INSERT INTO dbo.JobRequestMessages (Id, JobRequestId, SenderUserId, Body, CreatedAtUtc)
            VALUES (@Id, @JobRequestId, @SenderUserId, @Body, @CreatedAtUtc)
            """,
            new { m.Id, m.JobRequestId, m.SenderUserId, m.Body, m.CreatedAtUtc });
    }

    public async Task<JobMessageDto?> GetMessageDtoAsync(Guid messageId)
    {
        using var conn = await _db.CreateOpenConnectionAsync();
        return await conn.QuerySingleOrDefaultAsync<JobMessageDto>(
            """
            SELECT m.Id, m.JobRequestId, m.SenderUserId, u.FullName AS SenderName, m.Body, m.CreatedAtUtc
            FROM dbo.JobRequestMessages m
            JOIN dbo.Users u ON u.Id = m.SenderUserId
            WHERE m.Id = @messageId
            """, new { messageId });
    }

    public async Task<List<JobMessageDto>> ListMessageDtosAsync(Guid jobRequestId)
    {
        using var conn = await _db.CreateOpenConnectionAsync();
        return (await conn.QueryAsync<JobMessageDto>(
            """
            SELECT m.Id, m.JobRequestId, m.SenderUserId, u.FullName AS SenderName, m.Body, m.CreatedAtUtc
            FROM dbo.JobRequestMessages m
            JOIN dbo.Users u ON u.Id = m.SenderUserId
            WHERE m.JobRequestId = @jobRequestId
            ORDER BY m.CreatedAtUtc
            """, new { jobRequestId })).AsList();
    }

    // ---- Attachments ----

    public async Task ReplaceInvoiceAsync(JobRequestAttachment a)
    {
        using var conn = await _db.CreateOpenConnectionAsync();
        using var tx = conn.BeginTransaction();
        await conn.ExecuteAsync(
            "DELETE FROM dbo.JobRequestAttachments WHERE JobRequestId = @jobRequestId AND Kind = 0",
            new { jobRequestId = a.JobRequestId }, tx);
        await conn.ExecuteAsync(
            """
            INSERT INTO dbo.JobRequestAttachments (Id, JobRequestId, UploadedByUserId, Kind, FileName, ContentType, Content, CreatedAtUtc)
            VALUES (@Id, @JobRequestId, @UploadedByUserId, 0, @FileName, @ContentType, @Content, @CreatedAtUtc)
            """,
            new { a.Id, a.JobRequestId, a.UploadedByUserId, a.FileName, a.ContentType, a.Content, a.CreatedAtUtc }, tx);
        tx.Commit();
    }

    public async Task<JobRequestAttachment?> GetInvoiceAsync(Guid jobRequestId)
    {
        using var conn = await _db.CreateOpenConnectionAsync();
        return await conn.QuerySingleOrDefaultAsync<JobRequestAttachment>(
            "SELECT TOP 1 * FROM dbo.JobRequestAttachments WHERE JobRequestId = @jobRequestId AND Kind = 0 ORDER BY CreatedAtUtc DESC",
            new { jobRequestId });
    }

    public async Task AddPhotoAsync(JobRequestAttachment a)
    {
        using var conn = await _db.CreateOpenConnectionAsync();
        await conn.ExecuteAsync(
            """
            INSERT INTO dbo.JobRequestAttachments (Id, JobRequestId, UploadedByUserId, Kind, FileName, ContentType, Content, CreatedAtUtc)
            VALUES (@Id, @JobRequestId, @UploadedByUserId, 1, @FileName, @ContentType, @Content, @CreatedAtUtc)
            """,
            new { a.Id, a.JobRequestId, a.UploadedByUserId, a.FileName, a.ContentType, a.Content, a.CreatedAtUtc });
    }

    // ---- Calendar ----

    public async Task<List<BookedSlotDto>> ListBookedSlotsAsync(Guid providerId)
    {
        using var conn = await _db.CreateOpenConnectionAsync();
        return (await conn.QueryAsync<BookedSlotDto>(
            """
            SELECT ConfirmedStartUtc AS StartUtc, ConfirmedEndUtc AS EndUtc
            FROM dbo.JobRequests
            WHERE ProviderId = @providerId AND ConfirmedStartUtc IS NOT NULL AND Status IN (2, 9)
            ORDER BY ConfirmedStartUtc
            """, new { providerId })).AsList();
    }
}
