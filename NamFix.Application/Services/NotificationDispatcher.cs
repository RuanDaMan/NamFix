using System.Net;
using Hangfire;
using Microsoft.Extensions.Logging;
using NamFix.Application.Data.Repositories;
using NamFix.Application.Infrastructure.Mail;
using NamFix.Shared.Domain;
using NamFix.Shared.Dtos;
using NamFix.Shared.Enums;

namespace NamFix.Application.Services;

/// <summary>
/// Central sink for every in-app notification. Persists the notification and — this is the whole point —
/// also enqueues a matching, themed <b>email</b> for the recipient, unless they've unsubscribed from
/// that notification's category. Both <c>JobRequestService</c> and <c>SupportService</c> funnel their
/// notifications through here, so the rule "every SignalR notification also sends a mail" holds in one
/// place. It returns the <see cref="NotificationDto"/> the caller then pushes over SignalR (realtime
/// transport stays in the host-specific notifier).
/// </summary>
public interface INotificationDispatcher
{
    Task<NotificationDto> DispatchAsync(Notification notification);
}

public sealed class NotificationDispatcher : INotificationDispatcher
{
    private readonly INotificationRepository _notifications;
    private readonly IUserRepository _users;
    private readonly IEmailPreferenceService _prefs;
    private readonly IBackgroundJobClient _jobs;
    private readonly EmailTemplateRenderer _templates;
    private readonly MailAppSettings _mailApp;
    private readonly ILogger<NotificationDispatcher> _log;

    public NotificationDispatcher(
        INotificationRepository notifications,
        IUserRepository users,
        IEmailPreferenceService prefs,
        IBackgroundJobClient jobs,
        EmailTemplateRenderer templates,
        MailAppSettings mailApp,
        ILogger<NotificationDispatcher> log)
    {
        _notifications = notifications;
        _users = users;
        _prefs = prefs;
        _jobs = jobs;
        _templates = templates;
        _mailApp = mailApp;
        _log = log;
    }

    public async Task<NotificationDto> DispatchAsync(Notification notification)
    {
        await _notifications.InsertAsync(notification);

        var dto = new NotificationDto
        {
            Id = notification.Id,
            JobRequestId = notification.JobRequestId,
            TicketId = notification.TicketId,
            Type = notification.Type,
            Message = notification.Message,
            IsRead = notification.IsRead,
            CreatedAtUtc = notification.CreatedAtUtc
        };

        // Email dispatch must never break the notification path — it's best-effort and logged.
        try
        {
            await TryEnqueueEmailAsync(notification);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Failed to enqueue notification email for user {UserId} ({Type}).",
                notification.UserId, notification.Type);
        }

        return dto;
    }

    private async Task TryEnqueueEmailAsync(Notification n)
    {
        var (category, title) = Map(n.Type);

        if (!await _prefs.IsSubscribedAsync(n.UserId, category))
        {
            _log.LogDebug("User {UserId} unsubscribed from {Category}; skipping email for {Type}.",
                n.UserId, category, n.Type);
            return;
        }

        var user = await _users.GetByIdAsync(n.UserId);
        if (user is null || string.IsNullOrWhiteSpace(user.Email))
            return;

        var (ctaText, ctaUrl) = CallToAction(n);
        var unsubscribeUrl =
            $"{_mailApp.ApiBaseUrl.TrimEnd('/')}/api/email/unsubscribe?token={Uri.EscapeDataString(_prefs.CreateUnsubscribeToken(n.UserId, category))}";

        var bodyHtml = $"<p>{WebUtility.HtmlEncode(n.Message)}</p>";
        var html = _templates.Render(title, bodyHtml, ctaText, ctaUrl, unsubscribeUrl, preheader: n.Message);
        var text = _templates.PlainText(title, n.Message, ctaUrl, unsubscribeUrl);

        _jobs.Enqueue<IMailSenderService>(x => x.SendMailInBackground(
            new List<EmailRecipientDto> { new(user.Email, user.FullName) },
            new List<EmailRecipientDto>(),
            title, text, html, new List<AttachmentDto>(), false));
    }

    private (string ctaText, string ctaUrl) CallToAction(Notification n)
    {
        var baseUrl = _mailApp.ClientBaseUrl.TrimEnd('/');
        if (n.JobRequestId is { } jobId) return ("View job", $"{baseUrl}/bookings/{jobId}");
        if (n.TicketId is { } ticketId) return ("View ticket", $"{baseUrl}/support/{ticketId}");
        return ("Open NamFix", baseUrl);
    }

    /// <summary>Maps a notification type to its unsubscribe category and email title.</summary>
    private static (EmailNotificationCategory Category, string Title) Map(NotificationType type) => type switch
    {
        NotificationType.NewMessage => (EmailNotificationCategory.Messages, "New message"),

        NotificationType.QuoteReceived => (EmailNotificationCategory.Quotes, "New quote received"),
        NotificationType.QuoteAccepted => (EmailNotificationCategory.Quotes, "Your quote was accepted"),
        NotificationType.QuoteDeclined => (EmailNotificationCategory.Quotes, "Quote update"),

        NotificationType.SupportTicketCreated => (EmailNotificationCategory.Support, "New support ticket"),
        NotificationType.SupportReply => (EmailNotificationCategory.Support, "New support reply"),
        NotificationType.SupportStatusChanged => (EmailNotificationCategory.Support, "Support ticket updated"),
        NotificationType.SupportResolved => (EmailNotificationCategory.Support, "Support ticket resolved"),

        NotificationType.BookingRequested => (EmailNotificationCategory.JobUpdates, "New booking request"),
        NotificationType.TimeProposed => (EmailNotificationCategory.JobUpdates, "A new time was proposed"),
        NotificationType.BookingScheduled => (EmailNotificationCategory.JobUpdates, "Your booking is scheduled"),
        NotificationType.BookingCompleted => (EmailNotificationCategory.JobUpdates, "Job completed"),
        NotificationType.BookingPaid => (EmailNotificationCategory.JobUpdates, "Payment received"),
        NotificationType.BookingCancelled => (EmailNotificationCategory.JobUpdates, "Booking cancelled"),
        NotificationType.BookingDeclined => (EmailNotificationCategory.JobUpdates, "Booking declined"),
        NotificationType.LocationShared => (EmailNotificationCategory.JobUpdates, "Location shared"),
        NotificationType.JobPosted => (EmailNotificationCategory.JobUpdates, "Job posted"),
        NotificationType.JobStarted => (EmailNotificationCategory.JobUpdates, "Work has started"),
        NotificationType.NoShowFlagged => (EmailNotificationCategory.JobUpdates, "No-show flagged"),
        NotificationType.ReviewRequested => (EmailNotificationCategory.JobUpdates, "Leave a review"),
        NotificationType.UrgentJobBroadcast => (EmailNotificationCategory.JobUpdates, "Urgent job nearby"),

        _ => (EmailNotificationCategory.JobUpdates, "NamFix update")
    };
}
