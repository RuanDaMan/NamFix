using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NamFix.Application.Services;
using NamFix.Shared.Dtos;
using NamFix.Shared.Enums;

namespace NamFix.Api.Controllers;

[Authorize]
public sealed class BookingsController : ApiControllerBase
{
    private readonly IBookingService _bookings;
    public BookingsController(IBookingService bookings) => _bookings = bookings;

    /// <summary>Client requests a booking with a provider, proposing a first time.</summary>
    [Authorize(Roles = nameof(UserRole.Client))]
    [HttpPost]
    public Task<ActionResult<BookingDto>> Create(CreateBookingRequest request) =>
        Run(() => _bookings.CreateAsync(CurrentUserId, request));

    /// <summary>All bookings the signed-in user participates in (as client or provider).</summary>
    [HttpGet]
    public async Task<ActionResult<List<BookingDto>>> Mine() =>
        Ok(await _bookings.ListForUserAsync(CurrentUserId));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<BookingDto>> Get(Guid id)
    {
        var booking = await _bookings.GetAsync(CurrentUserId, id);
        return booking is null ? NotFound(new { error = "Booking not found." }) : Ok(booking);
    }

    [HttpPost("{id:guid}/propose-time")]
    public Task<ActionResult<BookingDto>> ProposeTime(Guid id, ProposeTimeRequest request) =>
        Run(() => _bookings.ProposeTimeAsync(CurrentUserId, id, request));

    [HttpPost("{id:guid}/accept")]
    public Task<ActionResult<BookingDto>> Accept(Guid id) =>
        Run(() => _bookings.AcceptAsync(CurrentUserId, id));

    [HttpPost("{id:guid}/decline")]
    public Task<ActionResult<BookingDto>> Decline(Guid id) =>
        Run(() => _bookings.DeclineAsync(CurrentUserId, id));

    [HttpPost("{id:guid}/cancel")]
    public Task<ActionResult<BookingDto>> Cancel(Guid id) =>
        Run(() => _bookings.CancelAsync(CurrentUserId, id));

    [HttpPost("{id:guid}/location")]
    public Task<ActionResult<BookingDto>> SetLocation(Guid id, SetBookingLocationRequest request) =>
        Run(() => _bookings.SetLocationAsync(CurrentUserId, id, request));

    /// <summary>Provider marks the job done and sets the fee to charge.</summary>
    [HttpPost("{id:guid}/complete")]
    public Task<ActionResult<BookingDto>> Complete(Guid id, CompleteBookingRequest request) =>
        Run(() => _bookings.CompleteAsync(CurrentUserId, id, request));

    /// <summary>Client pays the booking's invoice amount (the only way to pay a provider).</summary>
    [HttpPost("{id:guid}/pay")]
    public Task<ActionResult<BookingDto>> Pay(Guid id) =>
        Run(() => _bookings.PayAsync(CurrentUserId, id));

    // ---- Invoice file ----

    /// <summary>Provider uploads the invoice document for a booking (replaces any existing one).</summary>
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
            await _bookings.SaveInvoiceFileAsync(CurrentUserId, id, file.FileName, file.ContentType, ms.ToArray());
            return NoContent();
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpGet("{id:guid}/invoice")]
    public async Task<ActionResult> DownloadInvoice(Guid id)
    {
        var attachment = await _bookings.GetInvoiceFileAsync(CurrentUserId, id);
        if (attachment is null) return NotFound(new { error = "No invoice has been uploaded." });
        return File(attachment.Content, attachment.ContentType, attachment.FileName);
    }

    // ---- Chat ----

    [HttpGet("{id:guid}/messages")]
    public async Task<ActionResult<List<BookingMessageDto>>> Messages(Guid id) =>
        Ok(await _bookings.GetMessagesAsync(CurrentUserId, id));

    [HttpPost("{id:guid}/messages")]
    public Task<ActionResult<BookingMessageDto>> PostMessage(Guid id, SendBookingMessageRequest request) =>
        Run(() => _bookings.PostMessageAsync(CurrentUserId, id, request));

    /// <summary>Runs a service call, mapping expected validation failures to a 400 ErrorResponse shape.</summary>
    private async Task<ActionResult<T>> Run<T>(Func<Task<T>> action)
    {
        try { return Ok(await action()); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }
}
