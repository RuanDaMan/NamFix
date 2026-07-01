using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NamFix.Application.Services;
using NamFix.Shared.Dtos;
using NamFix.Shared.Enums;

namespace NamFix.Api.Controllers;

/// <summary>
/// Support/helpdesk endpoints. Any authenticated user can open and converse on their own tickets;
/// admins can view every ticket, reply, and change status/priority. Attachments are stored in the DB
/// (like booking invoices) and streamed back through the download endpoint.
/// </summary>
[Authorize]
public sealed class SupportController : ApiControllerBase
{
    private readonly ISupportService _support;
    public SupportController(ISupportService support) => _support = support;

    private bool IsAdmin => User.IsInRole(nameof(UserRole.Admin));

    // ---- Tickets ----

    [HttpPost("tickets")]
    public Task<ActionResult<SupportTicketDto>> Create(CreateTicketRequest request) =>
        Run(() => _support.CreateTicketAsync(CurrentUserId, request));

    /// <summary>The signed-in user's own tickets, newest activity first.</summary>
    [HttpGet("tickets/mine")]
    public async Task<ActionResult<List<SupportTicketDto>>> Mine() =>
        Ok(await _support.ListMyTicketsAsync(CurrentUserId));

    /// <summary>Admin queue: every ticket, optionally filtered by status/priority.</summary>
    [Authorize(Roles = nameof(UserRole.Admin))]
    [HttpGet("tickets")]
    public async Task<ActionResult<List<SupportTicketDto>>> All(
        [FromQuery] TicketStatus? status, [FromQuery] SupportPriority? priority) =>
        Ok(await _support.ListAllTicketsAsync(status, priority));

    [HttpGet("tickets/{id:guid}")]
    public async Task<ActionResult<SupportTicketDto>> Get(Guid id)
    {
        var ticket = await _support.GetTicketAsync(CurrentUserId, IsAdmin, id);
        return ticket is null ? NotFound(new { error = "Ticket not found." }) : Ok(ticket);
    }

    /// <summary>Admin updates a ticket's status and/or priority (resolving auto-replies to the user).</summary>
    [Authorize(Roles = nameof(UserRole.Admin))]
    [HttpPost("tickets/{id:guid}")]
    public Task<ActionResult<SupportTicketDto>> Update(Guid id, UpdateTicketRequest request) =>
        Run(() => _support.UpdateTicketAsync(CurrentUserId, id, request));

    // ---- Thread ----

    [HttpGet("tickets/{id:guid}/messages")]
    public async Task<ActionResult<List<SupportMessageDto>>> Messages(Guid id) =>
        Ok(await _support.GetMessagesAsync(CurrentUserId, IsAdmin, id));

    [HttpPost("tickets/{id:guid}/messages")]
    public Task<ActionResult<SupportMessageDto>> PostMessage(Guid id, PostSupportMessageRequest request) =>
        Run(() => _support.PostMessageAsync(CurrentUserId, IsAdmin, id, request));

    // ---- Attachments ----

    /// <summary>Attaches a file to a ticket (optionally tied to a specific message in the thread).</summary>
    [HttpPost("tickets/{id:guid}/attachments")]
    [RequestSizeLimit(10 * 1024 * 1024)] // 10 MB
    public async Task<ActionResult<SupportAttachmentDto>> UploadAttachment(
        Guid id, IFormFile file, [FromQuery] Guid? messageId)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "Choose a file to upload." });

        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);

        try
        {
            var dto = await _support.AttachFileAsync(
                CurrentUserId, IsAdmin, id, messageId, file.FileName, file.ContentType, ms.ToArray());
            return Ok(dto);
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpGet("attachments/{id:guid}")]
    public async Task<ActionResult> DownloadAttachment(Guid id)
    {
        var attachment = await _support.GetAttachmentAsync(CurrentUserId, IsAdmin, id);
        if (attachment is null) return NotFound(new { error = "Attachment not found." });
        return File(attachment.Content, attachment.ContentType, attachment.FileName);
    }

    /// <summary>Runs a service call, mapping expected validation failures to a 400 ErrorResponse shape.</summary>
    private async Task<ActionResult<T>> Run<T>(Func<Task<T>> action)
    {
        try { return Ok(await action()); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }
}
