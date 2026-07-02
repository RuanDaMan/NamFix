using NamFix.Application.Data.Repositories;
using NamFix.Shared.Contracts;
using NamFix.Shared.Domain;
using NamFix.Shared.Dtos;
using NamFix.Shared.Enums;

namespace NamFix.Application.Services;

public interface IJobService
{
    // Posting
    Task<JobRequestDto> CreateDirectAsync(Guid clientUserId, CreateDirectBookingRequest request);
    Task<JobRequestDto> PostJobAsync(Guid clientUserId, PostJobRequest request);

    // Reading
    Task<List<JobRequestDto>> ListForUserAsync(Guid userId);
    Task<List<JobRequestDto>> ListOpenForProviderAsync(Guid providerUserId);
    Task<JobRequestDto?> GetAsync(Guid userId, Guid jobId);

    // Quotes / matching
    Task<JobResponseDto> SubmitQuoteAsync(Guid providerUserId, Guid jobId, SubmitQuoteRequest request);
    Task WithdrawQuoteAsync(Guid providerUserId, Guid jobId, Guid responseId);
    Task<List<JobResponseDto>> ListResponsesAsync(Guid userId, Guid jobId);
    Task<JobRequestDto> AcceptQuoteAsync(Guid clientUserId, Guid jobId, Guid responseId);

    // Lifecycle
    Task<JobRequestDto> ProposeTimeAsync(Guid userId, Guid jobId, ProposeTimeRequest request);
    Task<JobRequestDto> AcceptAsync(Guid userId, Guid jobId);
    Task<JobRequestDto> DeclineAsync(Guid userId, Guid jobId);
    Task<JobRequestDto> CancelAsync(Guid userId, Guid jobId);
    Task<JobRequestDto> SetLocationAsync(Guid userId, Guid jobId, SetJobLocationRequest request);
    Task<JobRequestDto> StartAsync(Guid userId, Guid jobId);
    Task<JobRequestDto> CompleteAsync(Guid userId, Guid jobId, CompleteJobRequest request);
    Task<JobRequestDto> PayAsync(Guid clientUserId, Guid jobId);
    Task<JobRequestDto> NoShowAsync(Guid userId, Guid jobId);
    Task<JobRequestDto> ReviewAsync(Guid clientUserId, string clientName, Guid jobId, CreateJobReviewRequest request);

    // Files + chat
    Task SaveInvoiceFileAsync(Guid userId, Guid jobId, string fileName, string contentType, byte[] content);
    Task<JobRequestAttachment?> GetInvoiceFileAsync(Guid userId, Guid jobId);
    Task<List<JobMessageDto>> GetMessagesAsync(Guid userId, Guid jobId);
    Task<JobMessageDto> PostMessageAsync(Guid userId, Guid jobId, SendJobMessageRequest request);
}

/// <summary>
/// Orchestrates the full job lifecycle: matching/quoting (broadcast + direct), the back-and-forth time
/// negotiation, delivery (start/complete/pay), reviews, and cancellation/no-show accounting. Enforces
/// that every action is performed by a participant with the right role, that transitions are legal, and
/// raises an in-app notification (plus a realtime push) to the affected party on every change.
///
/// Payment can only be made against a <see cref="JobStatus.Completed"/> job and always uses the invoice
/// amount the provider set — never a caller-supplied figure — routing through the commission flow.
/// </summary>
public sealed class JobRequestService : IJobService
{
    private readonly IJobRepository _jobs;
    private readonly INotificationDispatcher _dispatcher;
    private readonly IProviderRepository _providers;
    private readonly IUserRepository _users;
    private readonly IReviewRepository _reviews;
    private readonly ITransactionService _transactions;
    private readonly IPlatformSettingsService _settings;
    private readonly IJobRealtimeNotifier _realtime;

    public JobRequestService(
        IJobRepository jobs,
        INotificationDispatcher dispatcher,
        IProviderRepository providers,
        IUserRepository users,
        IReviewRepository reviews,
        ITransactionService transactions,
        IPlatformSettingsService settings,
        IJobRealtimeNotifier realtime)
    {
        _jobs = jobs;
        _dispatcher = dispatcher;
        _providers = providers;
        _users = users;
        _reviews = reviews;
        _transactions = transactions;
        _settings = settings;
        _realtime = realtime;
    }

    // ---- Posting ----

    public async Task<JobRequestDto> CreateDirectAsync(Guid clientUserId, CreateDirectBookingRequest request)
    {
        var provider = await _providers.GetByIdAsync(request.ProviderId)
            ?? throw new InvalidOperationException("Provider not found.");

        var now = DateTime.UtcNow;
        var job = new JobRequest
        {
            Id = Guid.NewGuid(),
            ProviderId = provider.Id,
            ProviderUserId = provider.UserId,
            ClientUserId = clientUserId,
            CategoryId = provider.PrimaryCategoryId,
            TownId = provider.PrimaryTownId,
            ServiceDescription = request.ServiceDescription.Trim(),
            Status = JobStatus.PendingProvider,
            TargetMode = JobTargetMode.Direct,
            Urgency = JobUrgency.Normal,
            ProposedStartUtc = request.RequestedStartUtc,
            ProposedByUserId = clientUserId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        await _jobs.InsertAsync(job);
        var dto = await _jobs.GetDtoByIdAsync(job.Id) ?? throw NotFound();

        await NotifyAsync(provider.UserId, job.Id, NotificationType.BookingRequested,
            $"{dto.ClientName} requested a booking: {Trim(dto.ServiceDescription)}");
        await RaiseChangedAsync(job);
        return dto;
    }

    public async Task<JobRequestDto> PostJobAsync(Guid clientUserId, PostJobRequest request)
    {
        var now = DateTime.UtcNow;
        var urgent = request.Urgency == JobUrgency.Urgent;
        var job = new JobRequest
        {
            Id = Guid.NewGuid(),
            ClientUserId = clientUserId,
            CategoryId = request.CategoryId,
            TownId = request.TownId,
            ServiceDescription = request.ServiceDescription.Trim(),
            Status = JobStatus.Requested,
            TargetMode = request.TargetMode,
            Urgency = request.Urgency,
            ProposedStartUtc = request.PreferredStartUtc,
            QuoteExpiresUtc = request.QuoteExpiresUtc,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        await _jobs.InsertAsync(job);

        // Fan out to targeted providers (Direct) or matching providers (Broadcast / urgent).
        var targets = await ResolveTargetsAsync(request);
        var client = await _users.GetByIdAsync(clientUserId);
        var clientName = client?.FullName ?? "A client";
        var notifType = urgent ? NotificationType.UrgentJobBroadcast : NotificationType.JobPosted;
        var label = urgent ? "Urgent job" : "New job";

        foreach (var (providerId, providerUserId) in targets)
        {
            await _jobs.InsertResponseAsync(new JobRequestResponse
            {
                Id = Guid.NewGuid(),
                JobRequestId = job.Id,
                ProviderId = providerId,
                ProviderUserId = providerUserId,
                Status = JobResponseStatus.Invited,
                CreatedAtUtc = now
            });
            await NotifyAsync(providerUserId, job.Id, notifType,
                $"{label} from {clientName}: {Trim(job.ServiceDescription)}");
        }

        return await _jobs.GetDtoByIdAsync(job.Id) ?? throw NotFound();
    }

    /// <summary>Resolve the provider set to invite: explicit list for Direct, matching query for Broadcast.</summary>
    private async Task<List<(Guid ProviderId, Guid ProviderUserId)>> ResolveTargetsAsync(PostJobRequest request)
    {
        var result = new List<(Guid, Guid)>();
        if (request.TargetMode == JobTargetMode.Direct && request.TargetProviderIds is { Count: > 0 })
        {
            foreach (var pid in request.TargetProviderIds.Distinct())
            {
                var provider = await _providers.GetByIdAsync(pid);
                if (provider is not null)
                    result.Add((provider.Id, provider.UserId));
            }
        }
        else
        {
            var matches = await _providers.FindMatchingProvidersAsync(
                request.CategoryId, request.TownId,
                emergencyOnly: request.Urgency == JobUrgency.Urgent,
                lat: null, lng: null, radiusKm: 0);
            result.AddRange(matches.Select(m => (m.ProviderId, m.ProviderUserId)));
        }
        return result;
    }

    // ---- Reading ----

    public Task<List<JobRequestDto>> ListForUserAsync(Guid userId) => _jobs.ListDtosForUserAsync(userId);

    public Task<List<JobRequestDto>> ListOpenForProviderAsync(Guid providerUserId) =>
        _jobs.ListOpenForProviderAsync(providerUserId);

    public async Task<JobRequestDto?> GetAsync(Guid userId, Guid jobId)
    {
        var job = await _jobs.GetByIdAsync(jobId);
        if (job is null) return null;
        // Participants can always view; an invited provider can view an open job they can respond to.
        if (IsParticipant(job, userId) || await IsInvitedProviderAsync(job, userId))
            return await _jobs.GetDtoByIdAsync(jobId);
        return null;
    }

    // ---- Quotes / matching ----

    public async Task<JobResponseDto> SubmitQuoteAsync(Guid providerUserId, Guid jobId, SubmitQuoteRequest request)
    {
        var job = await _jobs.GetByIdAsync(jobId) ?? throw NotFound();
        if (job.Status is not (JobStatus.Requested or JobStatus.Quoted))
            throw new InvalidOperationException("This job is no longer accepting quotes.");
        if (!request.InterestOnly && (request.Amount is not { } amt || amt <= 0))
            throw new InvalidOperationException("Enter a quote amount (or register interest only).");

        var provider = await _providers.GetByUserIdAsync(providerUserId)
            ?? throw new InvalidOperationException("You don't have a provider profile.");

        var existing = await _jobs.GetResponseForProviderAsync(jobId, providerUserId);
        if (existing is not null && existing.Status is JobResponseStatus.Accepted or JobResponseStatus.Rejected)
            throw new InvalidOperationException("This job has already been decided.");

        var now = DateTime.UtcNow;
        var response = existing ?? new JobRequestResponse
        {
            Id = Guid.NewGuid(),
            JobRequestId = jobId,
            ProviderId = provider.Id,
            ProviderUserId = providerUserId,
            CreatedAtUtc = now
        };
        response.Status = request.InterestOnly ? JobResponseStatus.Interested : JobResponseStatus.Quoted;
        response.QuoteAmount = request.InterestOnly ? null : request.Amount;
        response.QuoteNote = request.Note;
        response.QuoteExpiresUtc = request.ExpiresUtc;
        response.ProposedStartUtc = request.ProposedStartUtc;
        response.RespondedAtUtc = now;

        if (existing is null) await _jobs.InsertResponseAsync(response);
        else await _jobs.UpdateResponseAsync(response);

        // Maintain the denormalized response-time signal (blended minutes since the job was posted).
        await UpdateResponseTimeAsync(provider.Id, provider.AvgResponseMinutes, job.CreatedAtUtc, now);

        // Advance the job so the client sees it has quotes.
        if (job.Status == JobStatus.Requested)
        {
            job.Status = JobStatus.Quoted;
            await _jobs.UpdateAsync(job);
        }

        var dto = (await _jobs.ListResponseDtosAsync(jobId)).FirstOrDefault(x => x.Id == response.Id)
                  ?? throw new InvalidOperationException("Quote not found.");
        await NotifyAsync(job.ClientUserId, jobId, NotificationType.QuoteReceived,
            $"{provider.BusinessName} responded to your job{(response.QuoteAmount is { } q ? $": {job.Currency} {q:0.00}" : ".")}");
        await _realtime.JobChangedAsync(jobId, new[] { job.ClientUserId });
        return dto;
    }

    public async Task WithdrawQuoteAsync(Guid providerUserId, Guid jobId, Guid responseId)
    {
        var response = await _jobs.GetResponseByIdAsync(responseId) ?? throw new InvalidOperationException("Quote not found.");
        if (response.JobRequestId != jobId || response.ProviderUserId != providerUserId)
            throw new InvalidOperationException("You can't withdraw this quote.");
        if (response.Status is JobResponseStatus.Accepted)
            throw new InvalidOperationException("An accepted quote can't be withdrawn.");
        response.Status = JobResponseStatus.Withdrawn;
        await _jobs.UpdateResponseAsync(response);
    }

    public async Task<List<JobResponseDto>> ListResponsesAsync(Guid userId, Guid jobId)
    {
        var job = await _jobs.GetByIdAsync(jobId) ?? throw NotFound();
        if (job.ClientUserId != userId)
            throw new InvalidOperationException("Only the client can view the quotes for this job.");
        return await _jobs.ListResponseDtosAsync(jobId);
    }

    public async Task<JobRequestDto> AcceptQuoteAsync(Guid clientUserId, Guid jobId, Guid responseId)
    {
        var job = await _jobs.GetByIdAsync(jobId) ?? throw NotFound();
        if (job.ClientUserId != clientUserId)
            throw new InvalidOperationException("Only the client can accept a quote.");
        if (job.Status is not (JobStatus.Requested or JobStatus.Quoted))
            throw new InvalidOperationException("This job is no longer open for selection.");

        var response = await _jobs.GetResponseByIdAsync(responseId);
        if (response is null || response.JobRequestId != jobId)
            throw new InvalidOperationException("Quote not found.");
        if (response.Status is JobResponseStatus.Withdrawn or JobResponseStatus.Declined)
            throw new InvalidOperationException("That quote is no longer available.");

        // Choose the provider in place — no new row is created.
        job.ProviderId = response.ProviderId;
        job.ProviderUserId = response.ProviderUserId;
        if (response.ProposedStartUtc is { } slot)
        {
            job.ProposedStartUtc = slot;
            job.ProposedByUserId = response.ProviderUserId;
            job.ConfirmedStartUtc = slot;
            job.Status = JobStatus.Scheduled;
        }
        else
        {
            job.ProposedByUserId = clientUserId;
            job.Status = JobStatus.PendingProvider;
        }
        await _jobs.UpdateAsync(job);

        response.Status = JobResponseStatus.Accepted;
        await _jobs.UpdateResponseAsync(response);
        await _jobs.RejectOtherResponsesAsync(jobId, responseId);

        var dto = await _jobs.GetDtoByIdAsync(jobId) ?? throw NotFound();
        await NotifyAsync(response.ProviderUserId, jobId, NotificationType.QuoteAccepted,
            $"{dto.ClientName} accepted your quote for: {Trim(dto.ServiceDescription)}");
        // Let the declined providers know.
        foreach (var other in await _jobs.ListResponseDtosAsync(jobId))
            if (other.Status == JobResponseStatus.Rejected)
                await NotifyAsync(other.ProviderUserId, jobId, NotificationType.QuoteDeclined,
                    "A job you quoted on went to another provider.");

        await RaiseChangedAsync(job);
        return dto;
    }

    // ---- Lifecycle ----

    public async Task<JobRequestDto> ProposeTimeAsync(Guid userId, Guid jobId, ProposeTimeRequest request)
    {
        var job = await LoadParticipantAsync(userId, jobId);
        RequireNegotiable(job, "A time can only be proposed on an open booking.");

        var byProvider = userId == job.ProviderUserId;
        job.ProposedStartUtc = request.ProposedStartUtc;
        job.ProposedByUserId = userId;
        job.ConfirmedStartUtc = null;
        job.Status = byProvider ? JobStatus.PendingClient : JobStatus.PendingProvider;

        var dto = await PersistAsync(job);
        var recipient = Other(job, userId);
        var who = byProvider ? dto.ProviderBusinessName : dto.ClientName;
        await NotifyAsync(recipient, jobId, NotificationType.TimeProposed,
            $"{who} proposed a new time: {Local(request.ProposedStartUtc)}");
        await RaiseChangedAsync(job);
        return dto;
    }

    public async Task<JobRequestDto> AcceptAsync(Guid userId, Guid jobId)
    {
        var job = await LoadParticipantAsync(userId, jobId);
        var canAccept = job.Status switch
        {
            JobStatus.PendingProvider => userId == job.ProviderUserId,
            JobStatus.PendingClient => userId == job.ClientUserId,
            _ => false
        };
        if (!canAccept)
            throw new InvalidOperationException("There is no proposed time awaiting your approval.");

        job.Status = JobStatus.Scheduled;
        job.ConfirmedStartUtc = job.ProposedStartUtc;

        var dto = await PersistAsync(job);
        await NotifyAsync(Other(job, userId), jobId, NotificationType.BookingScheduled,
            $"Booking confirmed for {Local(job.ConfirmedStartUtc)}.");
        await RaiseChangedAsync(job);
        return dto;
    }

    public async Task<JobRequestDto> DeclineAsync(Guid userId, Guid jobId)
    {
        var job = await LoadParticipantAsync(userId, jobId);
        if (userId != job.ProviderUserId)
            throw new InvalidOperationException("Only the provider can decline a booking request.");
        if (job.Status is not (JobStatus.PendingProvider or JobStatus.PendingClient))
            throw new InvalidOperationException("This booking can no longer be declined.");

        job.Status = JobStatus.Declined;
        var dto = await PersistAsync(job);
        await NotifyAsync(job.ClientUserId, jobId, NotificationType.BookingDeclined,
            $"{dto.ProviderBusinessName} declined your booking request.");
        await RaiseChangedAsync(job);
        return dto;
    }

    public async Task<JobRequestDto> CancelAsync(Guid userId, Guid jobId)
    {
        var job = await LoadParticipantAsync(userId, jobId);
        RequireActive(job, "This booking can no longer be cancelled.");

        // Late cancellation: inside the free window before a confirmed start counts against the canceller.
        var late = false;
        if (job.ConfirmedStartUtc is { } start)
        {
            var windowHours = await _settings.GetFreeCancellationWindowHoursAsync();
            late = DateTime.UtcNow > start.AddHours(-windowHours);
        }

        job.Status = JobStatus.Cancelled;
        job.CancelledByUserId = userId;
        job.CancelledAtUtc = DateTime.UtcNow;
        job.WasLateCancellation = late;
        var dto = await PersistAsync(job);

        if (late) await IncrementLateCancellationAsync(job, userId);

        var canceller = userId == job.ProviderUserId ? dto.ProviderBusinessName : dto.ClientName;
        await NotifyAsync(Other(job, userId), jobId, NotificationType.BookingCancelled,
            $"{canceller} cancelled the booking{(late ? " (late cancellation)" : "")}.");
        await RaiseChangedAsync(job);
        return dto;
    }

    public async Task<JobRequestDto> SetLocationAsync(Guid userId, Guid jobId, SetJobLocationRequest request)
    {
        var job = await LoadParticipantAsync(userId, jobId);
        if (userId != job.ClientUserId)
            throw new InvalidOperationException("Only the client can share the job location.");
        RequireActive(job, "Location can't be set on a closed booking.");

        job.LocationText = request.LocationText.Trim();
        job.LocationLat = request.Lat;
        job.LocationLng = request.Lng;

        var dto = await PersistAsync(job);
        if (job.ProviderUserId is { } providerUserId)
            await NotifyAsync(providerUserId, jobId, NotificationType.LocationShared,
                $"{dto.ClientName} shared the job location: {Trim(job.LocationText)}");
        await RaiseChangedAsync(job);
        return dto;
    }

    public async Task<JobRequestDto> StartAsync(Guid userId, Guid jobId)
    {
        var job = await LoadParticipantAsync(userId, jobId);
        if (userId != job.ProviderUserId)
            throw new InvalidOperationException("Only the provider can start the job.");
        if (job.Status != JobStatus.Scheduled)
            throw new InvalidOperationException("Only a scheduled booking can be started.");

        job.Status = JobStatus.InProgress;
        var dto = await PersistAsync(job);
        await NotifyAsync(job.ClientUserId, jobId, NotificationType.JobStarted,
            $"{dto.ProviderBusinessName} has started the job.");
        await RaiseChangedAsync(job);
        return dto;
    }

    public async Task<JobRequestDto> CompleteAsync(Guid userId, Guid jobId, CompleteJobRequest request)
    {
        var job = await LoadParticipantAsync(userId, jobId);
        if (userId != job.ProviderUserId)
            throw new InvalidOperationException("Only the provider can mark a booking complete.");
        if (job.Status is not (JobStatus.Scheduled or JobStatus.InProgress))
            throw new InvalidOperationException("Only a scheduled or in-progress booking can be marked complete.");
        if (request.InvoiceAmount <= 0)
            throw new InvalidOperationException("Enter the amount to charge.");

        job.Status = JobStatus.Completed;
        job.InvoiceAmount = request.InvoiceAmount;
        job.InvoiceNotes = request.InvoiceNotes;

        var dto = await PersistAsync(job);
        await NotifyAsync(job.ClientUserId, jobId, NotificationType.BookingCompleted,
            $"{dto.ProviderBusinessName} completed the job. Amount due: {job.Currency} {request.InvoiceAmount:0.00}.");
        await RaiseChangedAsync(job);
        return dto;
    }

    public async Task<JobRequestDto> PayAsync(Guid clientUserId, Guid jobId)
    {
        var job = await LoadParticipantAsync(clientUserId, jobId);
        if (clientUserId != job.ClientUserId)
            throw new InvalidOperationException("Only the client can pay a booking.");
        if (job.Status != JobStatus.Completed)
            throw new InvalidOperationException("Only a completed booking with an invoice can be paid.");
        if (job.InvoiceAmount is not { } amount || amount <= 0)
            throw new InvalidOperationException("This booking has no invoice amount to pay.");
        if (job.ProviderId is not { } providerId)
            throw new InvalidOperationException("This booking has no provider to pay.");

        // Payment is locked to the booking's invoice amount — never a caller-supplied figure.
        var transaction = await _transactions.CreateAsync(clientUserId, new CreateTransactionRequest
        {
            ProviderId = providerId,
            GrossAmount = amount,
            CategoryId = job.CategoryId,
            Currency = job.Currency
        });

        job.Status = JobStatus.Paid;
        job.TransactionId = transaction.Id;

        var dto = await PersistAsync(job);
        if (job.ProviderUserId is { } providerUserId)
            await NotifyAsync(providerUserId, jobId, NotificationType.BookingPaid,
                $"{dto.ClientName} paid {job.Currency} {amount:0.00} for the booking.");
        await RaiseChangedAsync(job);
        return dto;
    }

    public async Task<JobRequestDto> NoShowAsync(Guid userId, Guid jobId)
    {
        var job = await LoadParticipantAsync(userId, jobId);
        if (job.Status is not (JobStatus.Scheduled or JobStatus.InProgress))
            throw new InvalidOperationException("Only a scheduled or in-progress booking can be flagged as a no-show.");
        if (job.ConfirmedStartUtc is { } start && DateTime.UtcNow < start)
            throw new InvalidOperationException("The booking time hasn't arrived yet.");

        var absentee = Other(job, userId); // the reporter marks the OTHER party as absent
        job.Status = JobStatus.NoShow;
        job.NoShowByUserId = absentee;
        var dto = await PersistAsync(job);

        // Charge the reliability counter against the absent party (provider mirror + user counter).
        if (absentee == job.ProviderUserId && job.ProviderId is { } pid)
            await _providers.IncrementNoShowAsync(pid);
        await _users.IncrementNoShowAsync(absentee);

        await NotifyAsync(absentee, jobId, NotificationType.NoShowFlagged,
            "You were flagged as a no-show for a booking.");
        await RaiseChangedAsync(job);
        return dto;
    }

    public async Task<JobRequestDto> ReviewAsync(Guid clientUserId, string clientName, Guid jobId, CreateJobReviewRequest request)
    {
        var job = await LoadParticipantAsync(clientUserId, jobId);
        if (clientUserId != job.ClientUserId)
            throw new InvalidOperationException("Only the client can review a booking.");
        if (job.Status != JobStatus.Paid)
            throw new InvalidOperationException("Only a paid booking can be reviewed.");
        if (job.ProviderId is not { } providerId)
            throw new InvalidOperationException("This booking has no provider to review.");
        if (request.Rating is < 1 or > 5)
            throw new InvalidOperationException("Rating must be between 1 and 5.");

        await _reviews.InsertAsync(new Review
        {
            Id = Guid.NewGuid(),
            ProviderId = providerId,
            ClientUserId = clientUserId,
            JobRequestId = jobId,
            Rating = request.Rating,
            Comment = request.Comment,
            IsVerified = true, // reviewing a paid on-platform booking is inherently verified
            CreatedAtUtc = DateTime.UtcNow
        });
        await _providers.RecalculateRatingAsync(providerId);

        job.Status = JobStatus.Reviewed;
        var dto = await PersistAsync(job);
        if (job.ProviderUserId is { } providerUserId)
            await NotifyAsync(providerUserId, jobId, NotificationType.ReviewRequested,
                $"{clientName} left you a {request.Rating}★ review.");
        await RaiseChangedAsync(job);
        return dto;
    }

    // ---- Files ----

    public async Task SaveInvoiceFileAsync(Guid userId, Guid jobId, string fileName, string contentType, byte[] content)
    {
        var job = await LoadParticipantAsync(userId, jobId);
        if (userId != job.ProviderUserId)
            throw new InvalidOperationException("Only the provider can upload an invoice.");
        if (job.Status is not (JobStatus.Scheduled or JobStatus.InProgress or JobStatus.Completed))
            throw new InvalidOperationException("An invoice can only be attached to an active booking.");
        if (content.Length == 0)
            throw new InvalidOperationException("The uploaded file is empty.");

        await _jobs.ReplaceInvoiceAsync(new JobRequestAttachment
        {
            Id = Guid.NewGuid(),
            JobRequestId = jobId,
            UploadedByUserId = userId,
            Kind = AttachmentKind.Invoice,
            FileName = fileName,
            ContentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType,
            Content = content,
            CreatedAtUtc = DateTime.UtcNow
        });

        await RaiseChangedAsync(job);
    }

    public async Task<JobRequestAttachment?> GetInvoiceFileAsync(Guid userId, Guid jobId)
    {
        await LoadParticipantAsync(userId, jobId); // authorize
        return await _jobs.GetInvoiceAsync(jobId);
    }

    // ---- Chat ----

    public async Task<List<JobMessageDto>> GetMessagesAsync(Guid userId, Guid jobId)
    {
        await LoadParticipantAsync(userId, jobId); // authorize
        return await _jobs.ListMessageDtosAsync(jobId);
    }

    public async Task<JobMessageDto> PostMessageAsync(Guid userId, Guid jobId, SendJobMessageRequest request)
    {
        var job = await LoadParticipantAsync(userId, jobId);
        if (job.ProviderUserId is null)
            throw new InvalidOperationException("You can't message a job before a provider is chosen.");

        var message = new JobRequestMessage
        {
            Id = Guid.NewGuid(),
            JobRequestId = jobId,
            SenderUserId = userId,
            Body = request.Body.Trim(),
            CreatedAtUtc = DateTime.UtcNow
        };
        await _jobs.InsertMessageAsync(message);

        var dto = await _jobs.GetMessageDtoAsync(message.Id) ?? throw NotFound();
        var participants = new[] { job.ClientUserId, job.ProviderUserId.Value };
        await _realtime.MessagePostedAsync(jobId, participants, dto);

        await NotifyAsync(Other(job, userId), jobId, NotificationType.NewMessage,
            $"{dto.SenderName}: {Trim(dto.Body)}");
        return dto;
    }

    // ---- helpers ----

    private async Task<JobRequest> LoadParticipantAsync(Guid userId, Guid jobId)
    {
        var job = await _jobs.GetByIdAsync(jobId) ?? throw NotFound();
        if (!IsParticipant(job, userId))
            throw new InvalidOperationException("You don't have access to this booking.");
        return job;
    }

    private async Task<bool> IsInvitedProviderAsync(JobRequest job, Guid userId) =>
        await _jobs.GetResponseForProviderAsync(job.Id, userId) is not null;

    private async Task<JobRequestDto> PersistAsync(JobRequest job)
    {
        await _jobs.UpdateAsync(job);
        return await _jobs.GetDtoByIdAsync(job.Id) ?? throw NotFound();
    }

    private async Task UpdateResponseTimeAsync(Guid providerId, int? existing, DateTime postedUtc, DateTime respondedUtc)
    {
        var minutes = Math.Max(0, (int)Math.Round((respondedUtc - postedUtc).TotalMinutes));
        var blended = existing is { } e ? (int)Math.Round((e + minutes) / 2.0) : minutes;
        await _providers.SetAvgResponseMinutesAsync(providerId, blended);
    }

    private async Task IncrementLateCancellationAsync(JobRequest job, Guid cancellerUserId)
    {
        if (cancellerUserId == job.ProviderUserId && job.ProviderId is { } pid)
            await _providers.IncrementLateCancellationAsync(pid);
        await _users.IncrementLateCancellationAsync(cancellerUserId);
    }

    private async Task NotifyAsync(Guid recipientUserId, Guid jobId, NotificationType type, string message)
    {
        // The dispatcher persists the notification and enqueues the matching email (respecting the
        // user's unsubscribe settings); we then push it live over SignalR.
        var dto = await _dispatcher.DispatchAsync(new Notification
        {
            Id = Guid.NewGuid(),
            UserId = recipientUserId,
            JobRequestId = jobId,
            Type = type,
            Message = message,
            IsRead = false,
            CreatedAtUtc = DateTime.UtcNow
        });
        await _realtime.NotificationAsync(recipientUserId, dto);
    }

    private Task RaiseChangedAsync(JobRequest job)
    {
        var recipients = job.ProviderUserId is { } pu
            ? new[] { job.ClientUserId, pu }
            : new[] { job.ClientUserId };
        return _realtime.JobChangedAsync(job.Id, recipients);
    }

    private static bool IsParticipant(JobRequest j, Guid userId) =>
        userId == j.ClientUserId || userId == j.ProviderUserId;

    private static Guid Other(JobRequest j, Guid userId) =>
        userId == j.ClientUserId ? (j.ProviderUserId ?? j.ClientUserId) : j.ClientUserId;

    /// <summary>Open enough to still cancel/edit (not a terminal state).</summary>
    private static void RequireActive(JobRequest j, string message)
    {
        if (j.Status is JobStatus.Paid or JobStatus.Reviewed or JobStatus.Cancelled
            or JobStatus.Declined or JobStatus.NoShow)
            throw new InvalidOperationException(message);
    }

    /// <summary>In the time-negotiation window (a provider is chosen and no delivery has started).</summary>
    private static void RequireNegotiable(JobRequest j, string message)
    {
        if (j.Status is not (JobStatus.PendingProvider or JobStatus.PendingClient or JobStatus.Scheduled))
            throw new InvalidOperationException(message);
    }

    private static InvalidOperationException NotFound() => new("Booking not found.");

    private static string Local(DateTime? utc) =>
        utc is { } d ? d.ToString("ddd d MMM, HH:mm") + " UTC" : "—";

    private static string Trim(string? s, int max = 60) =>
        string.IsNullOrEmpty(s) ? "" : (s.Length <= max ? s : s[..max] + "…");
}
