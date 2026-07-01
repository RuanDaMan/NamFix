using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NamFix.Application.Services;
using NamFix.Shared.Dtos;
using NamFix.Shared.Enums;

namespace NamFix.Api.Controllers;

/// <summary>
/// The job lifecycle: posting (direct booking or broadcast/quote), matching/quoting, time negotiation,
/// delivery, payment, review, and cancellation/no-show. Superseded the old bookings controller;
/// a "booking" is just a job that has advanced past provider selection.
/// </summary>
[Authorize]
[Route("api/jobs")]
public sealed class JobsController : ApiControllerBase
{
    private readonly IJobService _jobs;
    public JobsController(IJobService jobs) => _jobs = jobs;

    // ---- Posting ----

    /// <summary>Client requests a booking with a specific provider, proposing a first time.</summary>
    [Authorize(Roles = nameof(UserRole.Client))]
    [HttpPost("direct")]
    public Task<ActionResult<JobRequestDto>> CreateDirect(CreateDirectBookingRequest request) =>
        Run(() => _jobs.CreateDirectAsync(CurrentUserId, request));

    /// <summary>Client posts a job to gather quotes (targeted or broadcast / urgent).</summary>
    [Authorize(Roles = nameof(UserRole.Client))]
    [HttpPost]
    public Task<ActionResult<JobRequestDto>> Post(PostJobRequest request) =>
        Run(() => _jobs.PostJobAsync(CurrentUserId, request));

    // ---- Reading ----

    /// <summary>All jobs the signed-in user participates in (as client or chosen provider).</summary>
    [HttpGet]
    public async Task<ActionResult<List<JobRequestDto>>> Mine() =>
        Ok(await _jobs.ListForUserAsync(CurrentUserId));

    /// <summary>Open jobs the signed-in provider was invited to / matches and can still quote on.</summary>
    [Authorize(Roles = nameof(UserRole.ServiceProvider))]
    [HttpGet("open")]
    public async Task<ActionResult<List<JobRequestDto>>> Open() =>
        Ok(await _jobs.ListOpenForProviderAsync(CurrentUserId));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<JobRequestDto>> Get(Guid id)
    {
        var job = await _jobs.GetAsync(CurrentUserId, id);
        return job is null ? NotFound(new { error = "Booking not found." }) : Ok(job);
    }

    // ---- Quotes / matching ----

    [Authorize(Roles = nameof(UserRole.ServiceProvider))]
    [HttpPost("{id:guid}/quotes")]
    public Task<ActionResult<JobResponseDto>> SubmitQuote(Guid id, SubmitQuoteRequest request) =>
        Run(() => _jobs.SubmitQuoteAsync(CurrentUserId, id, request));

    [Authorize(Roles = nameof(UserRole.ServiceProvider))]
    [HttpPost("{id:guid}/quotes/{responseId:guid}/withdraw")]
    public async Task<ActionResult> WithdrawQuote(Guid id, Guid responseId)
    {
        try { await _jobs.WithdrawQuoteAsync(CurrentUserId, id, responseId); return NoContent(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpGet("{id:guid}/quotes")]
    public async Task<ActionResult<List<JobResponseDto>>> Quotes(Guid id)
    {
        try { return Ok(await _jobs.ListResponsesAsync(CurrentUserId, id)); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [Authorize(Roles = nameof(UserRole.Client))]
    [HttpPost("{id:guid}/accept-quote/{responseId:guid}")]
    public Task<ActionResult<JobRequestDto>> AcceptQuote(Guid id, Guid responseId) =>
        Run(() => _jobs.AcceptQuoteAsync(CurrentUserId, id, responseId));

    // ---- Lifecycle ----

    [HttpPost("{id:guid}/propose-time")]
    public Task<ActionResult<JobRequestDto>> ProposeTime(Guid id, ProposeTimeRequest request) =>
        Run(() => _jobs.ProposeTimeAsync(CurrentUserId, id, request));

    [HttpPost("{id:guid}/accept")]
    public Task<ActionResult<JobRequestDto>> Accept(Guid id) =>
        Run(() => _jobs.AcceptAsync(CurrentUserId, id));

    [HttpPost("{id:guid}/decline")]
    public Task<ActionResult<JobRequestDto>> Decline(Guid id) =>
        Run(() => _jobs.DeclineAsync(CurrentUserId, id));

    [HttpPost("{id:guid}/cancel")]
    public Task<ActionResult<JobRequestDto>> Cancel(Guid id) =>
        Run(() => _jobs.CancelAsync(CurrentUserId, id));

    [HttpPost("{id:guid}/location")]
    public Task<ActionResult<JobRequestDto>> SetLocation(Guid id, SetJobLocationRequest request) =>
        Run(() => _jobs.SetLocationAsync(CurrentUserId, id, request));

    /// <summary>Provider marks the scheduled job as started.</summary>
    [HttpPost("{id:guid}/start")]
    public Task<ActionResult<JobRequestDto>> Start(Guid id) =>
        Run(() => _jobs.StartAsync(CurrentUserId, id));

    /// <summary>Provider marks the job done and sets the fee to charge.</summary>
    [HttpPost("{id:guid}/complete")]
    public Task<ActionResult<JobRequestDto>> Complete(Guid id, CompleteJobRequest request) =>
        Run(() => _jobs.CompleteAsync(CurrentUserId, id, request));

    /// <summary>Client pays the job's invoice amount (the only way to pay a provider).</summary>
    [HttpPost("{id:guid}/pay")]
    public Task<ActionResult<JobRequestDto>> Pay(Guid id) =>
        Run(() => _jobs.PayAsync(CurrentUserId, id));

    /// <summary>Either party flags the other as a no-show after the confirmed start.</summary>
    [HttpPost("{id:guid}/no-show")]
    public Task<ActionResult<JobRequestDto>> NoShow(Guid id) =>
        Run(() => _jobs.NoShowAsync(CurrentUserId, id));

    /// <summary>Client leaves a review on a paid job, advancing it to Reviewed.</summary>
    [Authorize(Roles = nameof(UserRole.Client))]
    [HttpPost("{id:guid}/review")]
    public Task<ActionResult<JobRequestDto>> Review(Guid id, CreateJobReviewRequest request) =>
        Run(() => _jobs.ReviewAsync(CurrentUserId, CurrentUserName, id, request));

    // ---- Invoice file ----

    [HttpPost("{id:guid}/invoice")]
    [RequestSizeLimit(10 * 1024 * 1024)] // 10 MB
    public async Task<ActionResult> UploadInvoice(Guid id, IFormFile file)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "Choose a file to upload." });

        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);

        try
        {
            await _jobs.SaveInvoiceFileAsync(CurrentUserId, id, file.FileName, file.ContentType, ms.ToArray());
            return NoContent();
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpGet("{id:guid}/invoice")]
    public async Task<ActionResult> DownloadInvoice(Guid id)
    {
        var attachment = await _jobs.GetInvoiceFileAsync(CurrentUserId, id);
        if (attachment is null) return NotFound(new { error = "No invoice has been uploaded." });
        return File(attachment.Content, attachment.ContentType, attachment.FileName);
    }

    // ---- Chat ----

    [HttpGet("{id:guid}/messages")]
    public async Task<ActionResult<List<JobMessageDto>>> Messages(Guid id) =>
        Ok(await _jobs.GetMessagesAsync(CurrentUserId, id));

    [HttpPost("{id:guid}/messages")]
    public Task<ActionResult<JobMessageDto>> PostMessage(Guid id, SendJobMessageRequest request) =>
        Run(() => _jobs.PostMessageAsync(CurrentUserId, id, request));

    /// <summary>Runs a service call, mapping expected validation failures to a 400 ErrorResponse shape.</summary>
    private async Task<ActionResult<T>> Run<T>(Func<Task<T>> action)
    {
        try { return Ok(await action()); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }
}
