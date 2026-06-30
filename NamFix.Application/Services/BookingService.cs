using NamFix.Application.Data.Repositories;
using NamFix.Shared.Contracts;
using NamFix.Shared.Domain;
using NamFix.Shared.Dtos;
using NamFix.Shared.Enums;

namespace NamFix.Application.Services;

public interface IBookingService
{
    Task<BookingDto> CreateAsync(Guid clientUserId, CreateBookingRequest request);
    Task<List<BookingDto>> ListForUserAsync(Guid userId);
    Task<BookingDto?> GetAsync(Guid userId, Guid bookingId);

    Task<BookingDto> ProposeTimeAsync(Guid userId, Guid bookingId, ProposeTimeRequest request);
    Task<BookingDto> AcceptAsync(Guid userId, Guid bookingId);
    Task<BookingDto> DeclineAsync(Guid userId, Guid bookingId);
    Task<BookingDto> CancelAsync(Guid userId, Guid bookingId);
    Task<BookingDto> SetLocationAsync(Guid userId, Guid bookingId, SetBookingLocationRequest request);
    Task<BookingDto> CompleteAsync(Guid userId, Guid bookingId, CompleteBookingRequest request);
    Task<BookingDto> PayAsync(Guid clientUserId, Guid bookingId);

    Task SaveInvoiceFileAsync(Guid userId, Guid bookingId, string fileName, string contentType, byte[] content);
    Task<BookingAttachment?> GetInvoiceFileAsync(Guid userId, Guid bookingId);

    Task<List<BookingMessageDto>> GetMessagesAsync(Guid userId, Guid bookingId);
    Task<BookingMessageDto> PostMessageAsync(Guid userId, Guid bookingId, SendBookingMessageRequest request);
}

/// <summary>
/// Orchestrates the booking lifecycle: the back-and-forth time negotiation, location sharing,
/// invoice/completion, and booking-locked payment. Enforces that every action is performed by a
/// participant with the right role, that transitions are legal, and raises an in-app notification
/// (plus a realtime push) to the affected party on every change.
///
/// Payment can only be made against a <see cref="BookingStatus.Completed"/> booking and always uses
/// the invoice amount the provider set — never a caller-supplied figure.
/// </summary>
public sealed class BookingService : IBookingService
{
    private readonly IBookingRepository _bookings;
    private readonly INotificationRepository _notifications;
    private readonly IProviderRepository _providers;
    private readonly ITransactionService _transactions;
    private readonly IBookingRealtimeNotifier _realtime;

    public BookingService(
        IBookingRepository bookings,
        INotificationRepository notifications,
        IProviderRepository providers,
        ITransactionService transactions,
        IBookingRealtimeNotifier realtime)
    {
        _bookings = bookings;
        _notifications = notifications;
        _providers = providers;
        _transactions = transactions;
        _realtime = realtime;
    }

    public async Task<BookingDto> CreateAsync(Guid clientUserId, CreateBookingRequest request)
    {
        var provider = await _providers.GetByIdAsync(request.ProviderId)
            ?? throw new InvalidOperationException("Provider not found.");

        var now = DateTime.UtcNow;
        var booking = new Booking
        {
            Id = Guid.NewGuid(),
            ProviderId = provider.Id,
            ProviderUserId = provider.UserId,
            ClientUserId = clientUserId,
            CategoryId = provider.PrimaryCategoryId,
            ServiceDescription = request.ServiceDescription.Trim(),
            Status = BookingStatus.PendingProvider,
            ProposedStartUtc = request.RequestedStartUtc,
            ProposedByUserId = clientUserId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        await _bookings.InsertAsync(booking);
        var dto = await _bookings.GetDtoByIdAsync(booking.Id) ?? throw NotFound();

        await NotifyAsync(provider.UserId, booking.Id, NotificationType.BookingRequested,
            $"{dto.ClientName} requested a booking: {Trim(dto.ServiceDescription)}");
        await RaiseChangedAsync(booking);
        return dto;
    }

    public Task<List<BookingDto>> ListForUserAsync(Guid userId) => _bookings.ListDtosForUserAsync(userId);

    public async Task<BookingDto?> GetAsync(Guid userId, Guid bookingId)
    {
        var booking = await _bookings.GetByIdAsync(bookingId);
        if (booking is null || !IsParticipant(booking, userId)) return null;
        return await _bookings.GetDtoByIdAsync(bookingId);
    }

    public async Task<BookingDto> ProposeTimeAsync(Guid userId, Guid bookingId, ProposeTimeRequest request)
    {
        var booking = await LoadParticipantAsync(userId, bookingId);
        RequireActive(booking, "A time can only be proposed on an open booking.");

        var byProvider = userId == booking.ProviderUserId;
        booking.ProposedStartUtc = request.ProposedStartUtc;
        booking.ProposedByUserId = userId;
        booking.ConfirmedStartUtc = null;
        // Approval bounces to the OTHER party.
        booking.Status = byProvider ? BookingStatus.PendingClient : BookingStatus.PendingProvider;

        var dto = await PersistAsync(booking);
        var recipient = Other(booking, userId);
        var who = byProvider ? dto.ProviderBusinessName : dto.ClientName;
        await NotifyAsync(recipient, booking.Id, NotificationType.TimeProposed,
            $"{who} proposed a new time: {Local(request.ProposedStartUtc)}");
        await RaiseChangedAsync(booking);
        return dto;
    }

    public async Task<BookingDto> AcceptAsync(Guid userId, Guid bookingId)
    {
        var booking = await LoadParticipantAsync(userId, bookingId);

        // Only the party whose turn it is may accept (the status encodes whose turn it is).
        var canAccept = booking.Status switch
        {
            BookingStatus.PendingProvider => userId == booking.ProviderUserId,
            BookingStatus.PendingClient => userId == booking.ClientUserId,
            _ => false
        };
        if (!canAccept)
            throw new InvalidOperationException("There is no proposed time awaiting your approval.");

        booking.Status = BookingStatus.Scheduled;
        booking.ConfirmedStartUtc = booking.ProposedStartUtc;

        var dto = await PersistAsync(booking);
        await NotifyAsync(Other(booking, userId), booking.Id, NotificationType.BookingScheduled,
            $"Booking confirmed for {Local(booking.ConfirmedStartUtc)}.");
        await RaiseChangedAsync(booking);
        return dto;
    }

    public async Task<BookingDto> DeclineAsync(Guid userId, Guid bookingId)
    {
        var booking = await LoadParticipantAsync(userId, bookingId);
        if (userId != booking.ProviderUserId)
            throw new InvalidOperationException("Only the provider can decline a booking request.");
        if (booking.Status is not (BookingStatus.PendingProvider or BookingStatus.PendingClient))
            throw new InvalidOperationException("This booking can no longer be declined.");

        booking.Status = BookingStatus.Declined;
        var dto = await PersistAsync(booking);
        await NotifyAsync(booking.ClientUserId, booking.Id, NotificationType.BookingDeclined,
            $"{dto.ProviderBusinessName} declined your booking request.");
        await RaiseChangedAsync(booking);
        return dto;
    }

    public async Task<BookingDto> CancelAsync(Guid userId, Guid bookingId)
    {
        var booking = await LoadParticipantAsync(userId, bookingId);
        RequireActive(booking, "This booking can no longer be cancelled.");

        booking.Status = BookingStatus.Cancelled;
        var dto = await PersistAsync(booking);
        var canceller = userId == booking.ProviderUserId ? dto.ProviderBusinessName : dto.ClientName;
        await NotifyAsync(Other(booking, userId), booking.Id, NotificationType.BookingCancelled,
            $"{canceller} cancelled the booking.");
        await RaiseChangedAsync(booking);
        return dto;
    }

    public async Task<BookingDto> SetLocationAsync(Guid userId, Guid bookingId, SetBookingLocationRequest request)
    {
        var booking = await LoadParticipantAsync(userId, bookingId);
        if (userId != booking.ClientUserId)
            throw new InvalidOperationException("Only the client can share the job location.");
        RequireActive(booking, "Location can't be set on a closed booking.");

        booking.LocationText = request.LocationText.Trim();
        booking.LocationLat = request.Lat;
        booking.LocationLng = request.Lng;

        var dto = await PersistAsync(booking);
        await NotifyAsync(booking.ProviderUserId, booking.Id, NotificationType.LocationShared,
            $"{dto.ClientName} shared the job location: {Trim(booking.LocationText)}");
        await RaiseChangedAsync(booking);
        return dto;
    }

    public async Task<BookingDto> CompleteAsync(Guid userId, Guid bookingId, CompleteBookingRequest request)
    {
        var booking = await LoadParticipantAsync(userId, bookingId);
        if (userId != booking.ProviderUserId)
            throw new InvalidOperationException("Only the provider can mark a booking complete.");
        if (booking.Status != BookingStatus.Scheduled)
            throw new InvalidOperationException("Only a scheduled booking can be marked complete.");
        if (request.InvoiceAmount <= 0)
            throw new InvalidOperationException("Enter the amount to charge.");

        booking.Status = BookingStatus.Completed;
        booking.InvoiceAmount = request.InvoiceAmount;
        booking.InvoiceNotes = request.InvoiceNotes;

        var dto = await PersistAsync(booking);
        await NotifyAsync(booking.ClientUserId, booking.Id, NotificationType.BookingCompleted,
            $"{dto.ProviderBusinessName} completed the job. Amount due: {booking.Currency} {request.InvoiceAmount:0.00}.");
        await RaiseChangedAsync(booking);
        return dto;
    }

    public async Task<BookingDto> PayAsync(Guid clientUserId, Guid bookingId)
    {
        var booking = await LoadParticipantAsync(clientUserId, bookingId);
        if (clientUserId != booking.ClientUserId)
            throw new InvalidOperationException("Only the client can pay a booking.");
        if (booking.Status != BookingStatus.Completed)
            throw new InvalidOperationException("Only a completed booking with an invoice can be paid.");
        if (booking.InvoiceAmount is not { } amount || amount <= 0)
            throw new InvalidOperationException("This booking has no invoice amount to pay.");

        // Payment is locked to the booking's invoice amount — never a caller-supplied figure.
        var transaction = await _transactions.CreateAsync(clientUserId, new CreateTransactionRequest
        {
            ProviderId = booking.ProviderId,
            GrossAmount = amount,
            CategoryId = booking.CategoryId,
            Currency = booking.Currency
        });

        booking.Status = BookingStatus.Paid;
        booking.TransactionId = transaction.Id;

        var dto = await PersistAsync(booking);
        await NotifyAsync(booking.ProviderUserId, booking.Id, NotificationType.BookingPaid,
            $"{dto.ClientName} paid {booking.Currency} {amount:0.00} for the booking.");
        await RaiseChangedAsync(booking);
        return dto;
    }

    public async Task SaveInvoiceFileAsync(Guid userId, Guid bookingId, string fileName, string contentType, byte[] content)
    {
        var booking = await LoadParticipantAsync(userId, bookingId);
        if (userId != booking.ProviderUserId)
            throw new InvalidOperationException("Only the provider can upload an invoice.");
        if (booking.Status is not (BookingStatus.Scheduled or BookingStatus.Completed))
            throw new InvalidOperationException("An invoice can only be attached to a scheduled or completed booking.");
        if (content.Length == 0)
            throw new InvalidOperationException("The uploaded file is empty.");

        await _bookings.ReplaceInvoiceAsync(new BookingAttachment
        {
            Id = Guid.NewGuid(),
            BookingId = bookingId,
            UploadedByUserId = userId,
            FileName = fileName,
            ContentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType,
            Content = content,
            CreatedAtUtc = DateTime.UtcNow
        });

        await RaiseChangedAsync(booking);
    }

    public async Task<BookingAttachment?> GetInvoiceFileAsync(Guid userId, Guid bookingId)
    {
        await LoadParticipantAsync(userId, bookingId); // authorize
        return await _bookings.GetInvoiceAsync(bookingId);
    }

    public async Task<List<BookingMessageDto>> GetMessagesAsync(Guid userId, Guid bookingId)
    {
        await LoadParticipantAsync(userId, bookingId); // authorize
        return await _bookings.ListMessageDtosAsync(bookingId);
    }

    public async Task<BookingMessageDto> PostMessageAsync(Guid userId, Guid bookingId, SendBookingMessageRequest request)
    {
        var booking = await LoadParticipantAsync(userId, bookingId);

        var message = new BookingMessage
        {
            Id = Guid.NewGuid(),
            BookingId = bookingId,
            SenderUserId = userId,
            Body = request.Body.Trim(),
            CreatedAtUtc = DateTime.UtcNow
        };
        await _bookings.InsertMessageAsync(message);

        var dto = await _bookings.GetMessageDtoAsync(message.Id) ?? throw NotFound();
        var participants = new[] { booking.ClientUserId, booking.ProviderUserId };
        await _realtime.MessagePostedAsync(bookingId, participants, dto);

        await NotifyAsync(Other(booking, userId), bookingId, NotificationType.NewMessage,
            $"{dto.SenderName}: {Trim(dto.Body)}");
        return dto;
    }

    // ---- helpers ----

    private async Task<Booking> LoadParticipantAsync(Guid userId, Guid bookingId)
    {
        var booking = await _bookings.GetByIdAsync(bookingId) ?? throw NotFound();
        if (!IsParticipant(booking, userId))
            throw new InvalidOperationException("You don't have access to this booking.");
        return booking;
    }

    private async Task<BookingDto> PersistAsync(Booking booking)
    {
        await _bookings.UpdateAsync(booking);
        return await _bookings.GetDtoByIdAsync(booking.Id) ?? throw NotFound();
    }

    private async Task NotifyAsync(Guid recipientUserId, Guid bookingId, NotificationType type, string message)
    {
        var notification = new Notification
        {
            Id = Guid.NewGuid(),
            UserId = recipientUserId,
            BookingId = bookingId,
            Type = type,
            Message = message,
            IsRead = false,
            CreatedAtUtc = DateTime.UtcNow
        };
        await _notifications.InsertAsync(notification);
        await _realtime.NotificationAsync(recipientUserId, new NotificationDto
        {
            Id = notification.Id,
            BookingId = notification.BookingId,
            Type = notification.Type,
            Message = notification.Message,
            IsRead = notification.IsRead,
            CreatedAtUtc = notification.CreatedAtUtc
        });
    }

    private Task RaiseChangedAsync(Booking booking) =>
        _realtime.BookingChangedAsync(booking.Id, new[] { booking.ClientUserId, booking.ProviderUserId });

    private static bool IsParticipant(Booking b, Guid userId) =>
        userId == b.ClientUserId || userId == b.ProviderUserId;

    private static Guid Other(Booking b, Guid userId) =>
        userId == b.ClientUserId ? b.ProviderUserId : b.ClientUserId;

    private static void RequireActive(Booking b, string message)
    {
        if (b.Status is BookingStatus.Paid or BookingStatus.Cancelled or BookingStatus.Declined)
            throw new InvalidOperationException(message);
    }

    private static InvalidOperationException NotFound() => new("Booking not found.");

    private static string Local(DateTime? utc) =>
        utc is { } d ? d.ToString("ddd d MMM, HH:mm") + " UTC" : "—";

    private static string Trim(string? s, int max = 60) =>
        string.IsNullOrEmpty(s) ? "" : (s.Length <= max ? s : s[..max] + "…");
}
