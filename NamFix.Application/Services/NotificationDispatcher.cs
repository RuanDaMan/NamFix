using System.Net;
using System.Text;
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

        var bodyHtml = BuildBodyHtml(GreetingName(user.FullName), n.Message, FollowUp(n.Type));
        var bodyText = BuildBodyText(GreetingName(user.FullName), n.Message, FollowUp(n.Type));
        var html = _templates.Render(title, bodyHtml, ctaText, ctaUrl, unsubscribeUrl, preheader: n.Message);
        var text = _templates.PlainText(title, bodyText, ctaUrl, unsubscribeUrl);

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

    /// <summary>Maps a notification type to its unsubscribe category and email title. Exposed so the
    /// admin test-email tool renders the exact same titles/categories real notifications use.</summary>
    public static (EmailNotificationCategory Category, string Title) DescribeEmail(NotificationType type) => Map(type);

    /// <summary>Builds the card body: a friendly greeting, the notification message, and a contextual
    /// follow-up line so every email reads as a full, professional message rather than a bare sentence.
    /// Shared with the admin preview tool so tests look exactly like real sends.</summary>
    public static string BuildBodyHtml(string greetingName, string message, string? followUp)
    {
        var sb = new StringBuilder();
        sb.Append($"<p style=\"margin:0 0 14px 0;\">Hi {WebUtility.HtmlEncode(greetingName)},</p>")
          .Append($"<p style=\"margin:0 0 14px 0;\">{WebUtility.HtmlEncode(message)}</p>");
        if (!string.IsNullOrWhiteSpace(followUp))
            sb.Append($"<p style=\"margin:0;color:{EmailTemplateRenderer.MutedColor};font-size:14px;\">{WebUtility.HtmlEncode(followUp)}</p>");
        return sb.ToString();
    }

    /// <summary>Plain-text counterpart of <see cref="BuildBodyHtml"/>.</summary>
    public static string BuildBodyText(string greetingName, string message, string? followUp)
    {
        var sb = new StringBuilder();
        sb.Append($"Hi {greetingName},").Append("\n\n").Append(message);
        if (!string.IsNullOrWhiteSpace(followUp))
            sb.Append("\n\n").Append(followUp);
        return sb.ToString();
    }

    /// <summary>First name for the greeting, or a friendly fallback when the name is blank.</summary>
    public static string GreetingName(string? fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName)) return "there";
        var first = fullName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return first.Length > 0 ? first[0] : "there";
    }

    /// <summary>A contextual "what this means / what to do next" line per notification type, exposed so
    /// the admin preview renders the same supporting copy real emails use. Empty for types that need none.</summary>
    public static string? FollowUp(NotificationType type) => type switch
    {
        NotificationType.BookingRequested => "Review the details and accept, propose a new time, or decline. Responding quickly helps you win the work.",
        NotificationType.TimeProposed => "Open the job to accept the proposed time or suggest another that suits you better.",
        NotificationType.BookingScheduled => "You can view or manage this booking anytime from your NamFix dashboard.",
        NotificationType.BookingCompleted => "Review the invoice and pay securely through NamFix to close out the job.",
        NotificationType.BookingPaid => "A receipt is available on the job page. Thanks for using NamFix!",
        NotificationType.BookingCancelled => "If this wasn't expected, you can post the job again or message the other party from NamFix.",
        NotificationType.BookingDeclined => "No problem — you can send this request to another provider from the search page.",
        NotificationType.NewMessage => "Reply straight from the job page to keep the whole conversation in one place.",
        NotificationType.LocationShared => "Open the job to view the location and get directions.",

        NotificationType.SupportTicketCreated => "Our team has been notified and will get back to you as soon as possible.",
        NotificationType.SupportReply => "Open the ticket to read the full reply and continue the conversation.",
        NotificationType.SupportStatusChanged => "Open the ticket for the latest details on where things stand.",
        NotificationType.SupportResolved => "If your issue isn't fully sorted, just reply on the ticket to reopen it.",

        NotificationType.JobPosted => "Matching providers can now send you quotes — we'll let you know as they come in.",
        NotificationType.QuoteReceived => "Compare your quotes and accept the one that works best for you.",
        NotificationType.QuoteAccepted => "Great news! Coordinate the details with the client and get the job scheduled.",
        NotificationType.QuoteDeclined => "Keep an eye on the job board — new opportunities are posted all the time.",
        NotificationType.JobStarted => "You'll be notified once the work is complete and an invoice is ready.",
        NotificationType.NoShowFlagged => "Our support team may reach out if any follow-up is needed.",
        NotificationType.ReviewRequested => "Your feedback helps other customers and supports great tradespeople.",
        NotificationType.UrgentJobBroadcast => "Be quick — urgent jobs fill fast. Open the job to send your quote.",

        _ => null
    };

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
