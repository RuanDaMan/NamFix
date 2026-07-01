using NamFix.Application.Data.Repositories;
using NamFix.Shared.Contracts;
using NamFix.Shared.Domain;
using NamFix.Shared.Dtos;
using NamFix.Shared.Enums;

namespace NamFix.Application.Services;

public interface ISupportService
{
    Task<SupportTicketDto> CreateTicketAsync(Guid requesterUserId, CreateTicketRequest request);
    Task<List<SupportTicketDto>> ListMyTicketsAsync(Guid userId);
    Task<List<SupportTicketDto>> ListAllTicketsAsync(TicketStatus? status, SupportPriority? priority);
    Task<SupportTicketDto?> GetTicketAsync(Guid userId, bool isAdmin, Guid ticketId);
    Task<List<SupportMessageDto>> GetMessagesAsync(Guid userId, bool isAdmin, Guid ticketId);
    Task<SupportMessageDto> PostMessageAsync(Guid userId, bool isAdmin, Guid ticketId, PostSupportMessageRequest request);
    Task<SupportTicketDto> UpdateTicketAsync(Guid adminUserId, Guid ticketId, UpdateTicketRequest request);

    Task<SupportAttachmentDto> AttachFileAsync(Guid userId, bool isAdmin, Guid ticketId, Guid? messageId,
        string fileName, string contentType, byte[] content);
    Task<SupportAttachment?> GetAttachmentAsync(Guid userId, bool isAdmin, Guid attachmentId);
}

/// <summary>
/// Orchestrates the support/helpdesk workflow: a user opens a ticket, the user and admins hold a
/// threaded conversation with file attachments, and an admin triages (status/priority) and resolves
/// it. Every message/status change raises an in-app notification (plus a realtime push) to the other
/// side, and resolving a ticket posts an automatic system reply to the requester.
///
/// Authorization: the requester and any admin can view/reply; only admins can change status/priority.
/// </summary>
public sealed class SupportService : ISupportService
{
    private readonly ISupportRepository _support;
    private readonly INotificationRepository _notifications;
    private readonly IUserRepository _users;
    private readonly ISupportRealtimeNotifier _realtime;

    public SupportService(
        ISupportRepository support,
        INotificationRepository notifications,
        IUserRepository users,
        ISupportRealtimeNotifier realtime)
    {
        _support = support;
        _notifications = notifications;
        _users = users;
        _realtime = realtime;
    }

    public async Task<SupportTicketDto> CreateTicketAsync(Guid requesterUserId, CreateTicketRequest request)
    {
        var now = DateTime.UtcNow;
        var ticket = new SupportTicket
        {
            Id = Guid.NewGuid(),
            RequesterUserId = requesterUserId,
            Subject = request.Subject.Trim(),
            Category = request.Category,
            Priority = request.Priority,
            Status = TicketStatus.Open,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            LastMessageAtUtc = now
        };
        await _support.InsertTicketAsync(ticket);

        var opening = new SupportMessage
        {
            Id = Guid.NewGuid(),
            TicketId = ticket.Id,
            SenderUserId = requesterUserId,
            Body = request.Body.Trim(),
            IsSystem = false,
            CreatedAtUtc = now
        };
        await _support.InsertMessageAsync(opening);

        var dto = await _support.GetTicketDtoAsync(ticket.Id) ?? throw NotFound();
        dto = dto with { OpeningMessageId = opening.Id };

        // Fan the new ticket out to the whole support team.
        foreach (var adminId in await _users.ListAdminIdsAsync())
            await NotifyAsync(adminId, ticket.Id, NotificationType.SupportTicketCreated,
                $"New support ticket from {dto.RequesterName}: {Trim(dto.Subject)}");

        await RaiseChangedAsync(ticket.Id, dto.RequesterUserId);
        return dto;
    }

    public Task<List<SupportTicketDto>> ListMyTicketsAsync(Guid userId) =>
        _support.ListTicketDtosForUserAsync(userId);

    public Task<List<SupportTicketDto>> ListAllTicketsAsync(TicketStatus? status, SupportPriority? priority) =>
        _support.ListAllTicketDtosAsync(status, priority);

    public async Task<SupportTicketDto?> GetTicketAsync(Guid userId, bool isAdmin, Guid ticketId)
    {
        var ticket = await _support.GetTicketByIdAsync(ticketId);
        if (ticket is null || !CanAccess(ticket, userId, isAdmin)) return null;
        return await _support.GetTicketDtoAsync(ticketId);
    }

    public async Task<List<SupportMessageDto>> GetMessagesAsync(Guid userId, bool isAdmin, Guid ticketId)
    {
        await LoadAuthorizedAsync(userId, isAdmin, ticketId);
        return await _support.ListMessageDtosAsync(ticketId);
    }

    public async Task<SupportMessageDto> PostMessageAsync(Guid userId, bool isAdmin, Guid ticketId, PostSupportMessageRequest request)
    {
        var ticket = await LoadAuthorizedAsync(userId, isAdmin, ticketId);
        if (ticket.Status == TicketStatus.Closed)
            throw new InvalidOperationException("This ticket is closed. Open a new ticket if you still need help.");

        var message = new SupportMessage
        {
            Id = Guid.NewGuid(),
            TicketId = ticketId,
            SenderUserId = userId,
            Body = request.Body.Trim(),
            IsSystem = false,
            CreatedAtUtc = DateTime.UtcNow
        };
        await _support.InsertMessageAsync(message);

        // An admin reply hands the ball back to the user; a user reply (re)opens the ticket for the team.
        ticket.Status = isAdmin ? TicketStatus.AwaitingUser : TicketStatus.Open;
        ticket.LastMessageAtUtc = message.CreatedAtUtc;
        await _support.UpdateTicketAsync(ticket);

        var dto = await _support.GetMessageDtoAsync(message.Id) ?? throw NotFound();
        await _realtime.SupportMessagePostedAsync(ticketId, await ParticipantsAsync(ticket), dto);

        if (isAdmin)
        {
            await NotifyAsync(ticket.RequesterUserId, ticketId, NotificationType.SupportReply,
                $"Support replied to \"{Trim(ticket.Subject)}\": {Trim(dto.Body)}");
        }
        else
        {
            foreach (var adminId in await _users.ListAdminIdsAsync())
                await NotifyAsync(adminId, ticketId, NotificationType.SupportReply,
                    $"{dto.SenderName} replied to \"{Trim(ticket.Subject)}\": {Trim(dto.Body)}");
        }

        await RaiseChangedAsync(ticketId, ticket.RequesterUserId);
        return dto;
    }

    public async Task<SupportTicketDto> UpdateTicketAsync(Guid adminUserId, Guid ticketId, UpdateTicketRequest request)
    {
        var ticket = await _support.GetTicketByIdAsync(ticketId) ?? throw NotFound();

        var previousStatus = ticket.Status;
        if (request.Priority is { } priority) ticket.Priority = priority;
        if (request.Status is { } status)
        {
            ticket.Status = status;
            ticket.ClosedAtUtc = status is TicketStatus.Resolved or TicketStatus.Closed
                ? DateTime.UtcNow
                : null;
        }
        await _support.UpdateTicketAsync(ticket);

        // On resolve/close, auto-respond in the thread and let the requester know.
        var nowResolved = request.Status is TicketStatus.Resolved or TicketStatus.Closed
            && previousStatus is not (TicketStatus.Resolved or TicketStatus.Closed);
        if (nowResolved)
        {
            var wording = ticket.Status == TicketStatus.Closed ? "closed" : "resolved";
            var system = new SupportMessage
            {
                Id = Guid.NewGuid(),
                TicketId = ticketId,
                SenderUserId = adminUserId,
                Body = $"This ticket has been marked as {wording}. If your issue isn't fully solved, reply here to reopen it.",
                IsSystem = true,
                CreatedAtUtc = DateTime.UtcNow
            };
            await _support.InsertMessageAsync(system);
            ticket.LastMessageAtUtc = system.CreatedAtUtc;
            await _support.UpdateTicketAsync(ticket);

            var systemDto = await _support.GetMessageDtoAsync(system.Id);
            if (systemDto is not null)
                await _realtime.SupportMessagePostedAsync(ticketId, await ParticipantsAsync(ticket), systemDto);

            await NotifyAsync(ticket.RequesterUserId, ticketId, NotificationType.SupportResolved,
                $"Your ticket \"{Trim(ticket.Subject)}\" has been {wording}.");
        }
        else if (request.Status is { } && request.Status != previousStatus)
        {
            await NotifyAsync(ticket.RequesterUserId, ticketId, NotificationType.SupportStatusChanged,
                $"Your ticket \"{Trim(ticket.Subject)}\" is now {ticket.Status}.");
        }

        var dto = await _support.GetTicketDtoAsync(ticketId) ?? throw NotFound();
        await RaiseChangedAsync(ticketId, ticket.RequesterUserId);
        return dto;
    }

    public async Task<SupportAttachmentDto> AttachFileAsync(Guid userId, bool isAdmin, Guid ticketId, Guid? messageId,
        string fileName, string contentType, byte[] content)
    {
        await LoadAuthorizedAsync(userId, isAdmin, ticketId);
        if (content.Length == 0)
            throw new InvalidOperationException("The uploaded file is empty.");

        var attachment = new SupportAttachment
        {
            Id = Guid.NewGuid(),
            TicketId = ticketId,
            MessageId = messageId,
            UploadedByUserId = userId,
            FileName = fileName,
            ContentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType,
            Content = content,
            SizeBytes = content.LongLength,
            CreatedAtUtc = DateTime.UtcNow
        };
        await _support.InsertAttachmentAsync(attachment);

        return new SupportAttachmentDto
        {
            Id = attachment.Id,
            TicketId = attachment.TicketId,
            MessageId = attachment.MessageId,
            FileName = attachment.FileName,
            ContentType = attachment.ContentType,
            SizeBytes = attachment.SizeBytes
        };
    }

    public async Task<SupportAttachment?> GetAttachmentAsync(Guid userId, bool isAdmin, Guid attachmentId)
    {
        var attachment = await _support.GetAttachmentAsync(attachmentId);
        if (attachment is null) return null;
        await LoadAuthorizedAsync(userId, isAdmin, attachment.TicketId); // authorize via the owning ticket
        return attachment;
    }

    // ---- helpers ----

    private async Task<SupportTicket> LoadAuthorizedAsync(Guid userId, bool isAdmin, Guid ticketId)
    {
        var ticket = await _support.GetTicketByIdAsync(ticketId) ?? throw NotFound();
        if (!CanAccess(ticket, userId, isAdmin))
            throw new InvalidOperationException("You don't have access to this ticket.");
        return ticket;
    }

    private static bool CanAccess(SupportTicket ticket, Guid userId, bool isAdmin) =>
        isAdmin || ticket.RequesterUserId == userId;

    /// <summary>Everyone who should receive live pushes for a ticket: the requester + every admin.</summary>
    private async Task<List<Guid>> ParticipantsAsync(SupportTicket ticket)
    {
        var ids = new List<Guid> { ticket.RequesterUserId };
        ids.AddRange(await _users.ListAdminIdsAsync());
        return ids;
    }

    private async Task NotifyAsync(Guid recipientUserId, Guid ticketId, NotificationType type, string message)
    {
        var notification = new Notification
        {
            Id = Guid.NewGuid(),
            UserId = recipientUserId,
            TicketId = ticketId,
            Type = type,
            Message = message,
            IsRead = false,
            CreatedAtUtc = DateTime.UtcNow
        };
        await _notifications.InsertAsync(notification);
        await _realtime.NotificationAsync(recipientUserId, new NotificationDto
        {
            Id = notification.Id,
            TicketId = notification.TicketId,
            Type = notification.Type,
            Message = notification.Message,
            IsRead = notification.IsRead,
            CreatedAtUtc = notification.CreatedAtUtc
        });
    }

    private async Task RaiseChangedAsync(Guid ticketId, Guid requesterUserId)
    {
        var ids = new List<Guid> { requesterUserId };
        ids.AddRange(await _users.ListAdminIdsAsync());
        await _realtime.TicketChangedAsync(ticketId, ids);
    }

    private static InvalidOperationException NotFound() => new("Ticket not found.");

    private static string Trim(string? s, int max = 60) =>
        string.IsNullOrEmpty(s) ? "" : (s.Length <= max ? s : s[..max] + "…");
}
