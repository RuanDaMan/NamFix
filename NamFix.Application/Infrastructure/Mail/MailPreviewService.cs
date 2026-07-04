using System.Net;
using Microsoft.Extensions.Logging;
using NamFix.Application.Services;
using NamFix.Shared.Dtos;
using NamFix.Shared.Enums;

namespace NamFix.Application.Infrastructure.Mail;

/// <summary>
/// Backs the admin "send test emails" tool. It enumerates every email type the platform can send
/// (one per <see cref="NotificationType"/>, plus the transactional password-reset), renders each with
/// representative dummy content through the <b>same</b> <see cref="EmailTemplateRenderer"/> the real
/// paths use, and sends the selected ones to a chosen address so an admin can eyeball the result.
///
/// <para>Sends are inline (not enqueued) and per-type isolated: a failure on one type is logged in
/// full and reported back in <see cref="SendTestEmailsResultDto.Failed"/> rather than aborting the
/// rest. Support-copy-on-failure is suppressed so exercising this tool never spams the support inbox.</para>
/// </summary>
public interface IMailPreviewService
{
    /// <summary>All selectable test-email types, grouped for the UI.</summary>
    IReadOnlyList<TestEmailTypeDto> ListTypes();

    /// <summary>Sends a dummy sample of each selected type to <paramref name="toEmail"/>.</summary>
    Task<SendTestEmailsResultDto> SendAsync(string toEmail, IReadOnlyCollection<string> keys);
}

public sealed class MailPreviewService : IMailPreviewService
{
    private const string PasswordResetKey = "account.password-reset";
    private const string NotifyPrefix = "notify.";

    private readonly IMailSenderService _mail;
    private readonly EmailTemplateRenderer _templates;
    private readonly MailAppSettings _app;
    private readonly ILogger<MailPreviewService> _log;

    public MailPreviewService(IMailSenderService mail, EmailTemplateRenderer templates, MailAppSettings app,
        ILogger<MailPreviewService> log)
    {
        _mail = mail;
        _templates = templates;
        _app = app;
        _log = log;
    }

    public IReadOnlyList<TestEmailTypeDto> ListTypes()
    {
        var types = new List<TestEmailTypeDto>();

        foreach (var type in Enum.GetValues<NotificationType>())
        {
            var (category, title) = NotificationDispatcher.DescribeEmail(type);
            types.Add(new TestEmailTypeDto
            {
                Key = NotifyPrefix + type,
                Label = title,
                Group = GroupName(category)
            });
        }

        types.Add(new TestEmailTypeDto
        {
            Key = PasswordResetKey,
            Label = "Password reset",
            Group = GroupName(EmailNotificationCategory.AccountSecurity)
        });

        return types;
    }

    public async Task<SendTestEmailsResultDto> SendAsync(string toEmail, IReadOnlyCollection<string> keys)
    {
        var sent = 0;
        var failed = new List<string>();

        foreach (var key in keys.Distinct())
        {
            var sample = Build(key);
            if (sample is null)
            {
                _log.LogWarning("Ignoring unknown test-email key {Key}.", key);
                continue;
            }

            try
            {
                // bcc empty, not important, and sendErrorsToSupport:false so a failing test send never
                // triggers a support-inbox copy. SmtpMailSenderService logs the full failure and rethrows.
                await _mail.SendMail(
                    new List<EmailRecipientDto> { new(toEmail) },
                    new List<EmailRecipientDto>(),
                    new List<EmailRecipientDto>(),
                    sample.Subject, sample.Text, sample.Html,
                    new List<AttachmentDto>(), important: false, sendErrorsToSupport: false);
                sent++;
            }
            catch (Exception ex)
            {
                // Full detail is already logged by the sender; keep a breadcrumb and surface the label.
                _log.LogError(ex, "Test email \"{Label}\" to {ToEmail} failed.", sample.Label, toEmail);
                failed.Add(sample.Label);
            }
        }

        return new SendTestEmailsResultDto { Sent = sent, Failed = failed };
    }

    /// <summary>Builds the rendered subject/body for one key, or null if the key is unknown.</summary>
    private Sample? Build(string key)
    {
        if (key == PasswordResetKey)
            return BuildPasswordReset();

        if (key.StartsWith(NotifyPrefix, StringComparison.Ordinal) &&
            Enum.TryParse<NotificationType>(key[NotifyPrefix.Length..], out var type))
            return BuildNotification(type);

        return null;
    }

    /// <summary>Mirrors <c>NotificationDispatcher.TryEnqueueEmailAsync</c>: title + message paragraph +
    /// contextual CTA + unsubscribe footer, rendered through the shared template.</summary>
    private Sample BuildNotification(NotificationType type)
    {
        var (_, title) = NotificationDispatcher.DescribeEmail(type);
        var message = SampleMessage(type);

        var (ctaText, ctaUrl) = SampleCallToAction(type);
        var unsubscribeUrl =
            $"{_app.ApiBaseUrl.TrimEnd('/')}/api/email/unsubscribe?token=SAMPLE-TOKEN";

        var bodyHtml = $"<p>{WebUtility.HtmlEncode(message)}</p>";
        var html = _templates.Render(title, bodyHtml, ctaText, ctaUrl, unsubscribeUrl, preheader: message);
        var text = _templates.PlainText(title, message, ctaUrl, unsubscribeUrl);

        return new Sample($"[TEST] {title}", html, text, title);
    }

    /// <summary>Mirrors <c>AuthService.SendPasswordResetEmail</c> so the reset mail can be previewed too.</summary>
    private Sample BuildPasswordReset()
    {
        const string heading = "Reset your NamFix password";
        var resetUrl = $"{_app.ClientBaseUrl.TrimEnd('/')}/reset-password?token=SAMPLE-TOKEN";
        var hours = Math.Max(1, _app.PasswordResetTokenHours);

        var bodyHtml =
            "<p>Hi there,</p>" +
            "<p>We received a request to reset your NamFix password. Click the button below to choose a new one. " +
            $"This link expires in {hours} hour(s).</p>" +
            "<p style=\"color:#6b7280;font-size:13px;\">If you didn't request this, you can safely ignore this email — your password won't change.</p>";
        var html = _templates.Render(heading, bodyHtml, "Reset password", resetUrl);
        var text = _templates.PlainText(heading,
            "We received a request to reset your NamFix password. Open the link below to choose a new one.", resetUrl);

        return new Sample($"[TEST] {heading}", html, text, "Password reset");
    }

    /// <summary>A representative CTA for the type, matching what the real notification would produce
    /// (job link, ticket link, or a generic "Open NamFix").</summary>
    private (string ctaText, string ctaUrl) SampleCallToAction(NotificationType type)
    {
        var baseUrl = _app.ClientBaseUrl.TrimEnd('/');
        var (category, _) = NotificationDispatcher.DescribeEmail(type);
        return category switch
        {
            EmailNotificationCategory.Support => ("View ticket", $"{baseUrl}/support/00000000-0000-0000-0000-000000000000"),
            EmailNotificationCategory.JobUpdates or EmailNotificationCategory.Quotes or EmailNotificationCategory.Messages
                => ("View job", $"{baseUrl}/bookings/00000000-0000-0000-0000-000000000000"),
            _ => ("Open NamFix", baseUrl)
        };
    }

    /// <summary>Dummy message body per notification type, so each preview reads like the real thing.</summary>
    private static string SampleMessage(NotificationType type) => type switch
    {
        NotificationType.BookingRequested => "Sipho requested a booking for “Fix leaking kitchen tap” on Sat 12 Jul, 09:00.",
        NotificationType.TimeProposed => "Aqua Plumbing proposed a new time for your job: Mon 14 Jul, 14:00.",
        NotificationType.BookingScheduled => "Your booking with Aqua Plumbing is confirmed for Sat 12 Jul, 09:00.",
        NotificationType.BookingCompleted => "Aqua Plumbing marked the job complete and issued an invoice of N$650.00.",
        NotificationType.BookingPaid => "Payment of N$650.00 for “Fix leaking kitchen tap” was received. Thank you!",
        NotificationType.BookingCancelled => "Your booking with Aqua Plumbing for Sat 12 Jul was cancelled.",
        NotificationType.BookingDeclined => "Aqua Plumbing declined your booking request for “Fix leaking kitchen tap”.",
        NotificationType.NewMessage => "Sipho: “Hi, are you able to come a bit earlier on Saturday?”",
        NotificationType.LocationShared => "Sipho shared the job location: 12 Nelson Mandela Ave, Windhoek.",

        NotificationType.SupportTicketCreated => "A new support ticket was opened: “Can't update my profile photo”.",
        NotificationType.SupportReply => "NamFix Support replied to your ticket “Can't update my profile photo”.",
        NotificationType.SupportStatusChanged => "Your ticket “Can't update my profile photo” is now Awaiting your reply.",
        NotificationType.SupportResolved => "Your ticket “Can't update my profile photo” was marked resolved.",

        NotificationType.JobPosted => "Your job “Fix leaking kitchen tap” in Windhoek was posted to matching providers.",
        NotificationType.QuoteReceived => "Aqua Plumbing sent a quote of N$650.00 for “Fix leaking kitchen tap”.",
        NotificationType.QuoteAccepted => "Sipho accepted your quote of N$650.00 for “Fix leaking kitchen tap”.",
        NotificationType.QuoteDeclined => "Your quote for “Fix leaking kitchen tap” was not selected.",
        NotificationType.JobStarted => "Aqua Plumbing has started work on “Fix leaking kitchen tap”.",
        NotificationType.NoShowFlagged => "A no-show was flagged on the job “Fix leaking kitchen tap”.",
        NotificationType.ReviewRequested => "How did it go? Leave a review for Aqua Plumbing.",
        NotificationType.UrgentJobBroadcast => "Urgent job nearby: “Burst pipe” in Windhoek needs a plumber now.",

        _ => "This is a sample NamFix notification email so you can preview how it looks."
    };

    private static string GroupName(EmailNotificationCategory category) => category switch
    {
        EmailNotificationCategory.JobUpdates => "Job updates",
        EmailNotificationCategory.Messages => "Messages",
        EmailNotificationCategory.Quotes => "Quotes",
        EmailNotificationCategory.Support => "Support",
        EmailNotificationCategory.AccountSecurity => "Account & security",
        _ => "Other"
    };

    private sealed record Sample(string Subject, string Html, string Text, string Label);
}
