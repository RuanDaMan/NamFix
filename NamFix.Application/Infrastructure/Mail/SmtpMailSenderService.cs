using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using MimeKit;
using NamFix.Shared.Dtos;

namespace NamFix.Application.Infrastructure.Mail;

/// <summary>
/// MailKit/MimeKit-backed <see cref="IMailSenderService"/>. Builds a multipart message (plain-text +
/// HTML) and sends it over SMTP using the configured mailbox.
///
/// Error handling (per the project rule): every failure is <b>logged in full</b> via the logger
/// (Serilog is the provider). Inline <see cref="SendMail"/> rethrows so the caller — or Hangfire's
/// <c>[AutomaticRetry]</c> for the background path — sees the failure and can retry; a best-effort copy
/// of the failure is emailed to support when <c>sendErrorsToSupport</c> is set (guarded against
/// recursion). Callers on the request thread only ever <i>enqueue</i>, so a broken SMTP server never
/// crashes an HTTP request.
/// </summary>
public sealed class SmtpMailSenderService : IMailSenderService
{
    private readonly MailConfiguration _cfg;
    private readonly MailAppSettings _app;
    private readonly ILogger<SmtpMailSenderService> _log;

    public SmtpMailSenderService(MailConfiguration cfg, MailAppSettings app, ILogger<SmtpMailSenderService> log)
    {
        _cfg = cfg;
        _app = app;
        _log = log;
    }

    // Hangfire job entry point. Blocks on the async send so any exception propagates to the worker,
    // triggering [AutomaticRetry]. Runs on a background thread, never the request thread.
    public void SendMailInBackground(List<EmailRecipientDto> recipients, List<EmailRecipientDto> ccRecipients,
        string subject, string textBody, string htmlBody, List<AttachmentDto> attachments, bool important = false) =>
        SendMail(recipients, ccRecipients, new List<EmailRecipientDto>(), subject, textBody, htmlBody, attachments, important)
            .GetAwaiter().GetResult();

    public Task SendMail(List<EmailRecipientDto> recipients, List<EmailRecipientDto> ccRecipients,
        string subject, string textBody, string htmlBody, List<AttachmentDto> attachments, bool important = false) =>
        SendMail(recipients, ccRecipients, new List<EmailRecipientDto>(), subject, textBody, htmlBody, attachments, important);

    public async Task SendMail(List<EmailRecipientDto> recipients, List<EmailRecipientDto> ccRecipients,
        List<EmailRecipientDto> bccRecipients, string subject, string textBody, string htmlBody,
        List<AttachmentDto> attachments, bool important = false, bool sendErrorsToSupport = true)
    {
        if (recipients is null || recipients.Count == 0)
        {
            _log.LogWarning("SendMail called with no recipients (subject: {Subject}); skipping.", subject);
            return;
        }

        ccRecipients ??= new List<EmailRecipientDto>();
        bccRecipients ??= new List<EmailRecipientDto>();

#if DEBUG
        // Debug builds never mail real users: redirect everything to the configured DebugEmail. With
        // none set, skip silently (log a warning) rather than sending or throwing.
        if (string.IsNullOrWhiteSpace(_app.DebugEmail))
        {
            _log.LogWarning("Debug build with no DebugEmail configured — email \"{Subject}\" was not sent. " +
                            "Set Mail:DebugEmail to receive redirected mail.", subject);
            return;
        }

        var intended = string.Join(", ", recipients.Concat(ccRecipients).Concat(bccRecipients).Select(r => r.Email));
        _log.LogInformation("Debug build: redirecting email \"{Subject}\" (intended for {Intended}) to {DebugEmail}.",
            subject, intended, _app.DebugEmail);
        recipients = new List<EmailRecipientDto> { new(_app.DebugEmail) };
        ccRecipients = new List<EmailRecipientDto>();
        bccRecipients = new List<EmailRecipientDto>();
        subject = $"[DEBUG] {subject}";
#endif

        var message = BuildMessage(recipients, ccRecipients, bccRecipients, subject, textBody, htmlBody, attachments, important);

        try
        {
            using var client = new SmtpClient();
            var socketOptions = _cfg.SmtpTls ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTlsWhenAvailable;
            await client.ConnectAsync(_cfg.SmtpHost, _cfg.SmtpPort, socketOptions);
            if (!string.IsNullOrWhiteSpace(_cfg.SmtpUserName))
                await client.AuthenticateAsync(_cfg.SmtpUserName, _cfg.SmtpPassword);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);

            _log.LogInformation("Sent email \"{Subject}\" to {Recipients}.", subject,
                string.Join(", ", recipients.Select(r => r.Email)));
        }
        catch (Exception ex)
        {
            // Full detail is logged; the short story is enough for the operator scanning logs.
            _log.LogError(ex, "Failed to send email \"{Subject}\" to {Recipients}.", subject,
                string.Join(", ", recipients.Select(r => r.Email)));

            if (sendErrorsToSupport)
                await TryNotifySupportAsync(subject, recipients, ex);

            throw; // let the caller / Hangfire AutomaticRetry react
        }
    }

    private MimeMessage BuildMessage(List<EmailRecipientDto> to, List<EmailRecipientDto> cc, List<EmailRecipientDto> bcc,
        string subject, string textBody, string htmlBody, List<AttachmentDto> attachments, bool important)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_cfg.DisplayName, _cfg.FromEmail));
        AddAll(message.To, to);
        AddAll(message.Cc, cc);
        AddAll(message.Bcc, bcc);
        message.Subject = subject;

        if (important)
        {
            message.Priority = MessagePriority.Urgent;
            message.Importance = MessageImportance.High;
        }

        var builder = new BodyBuilder
        {
            TextBody = string.IsNullOrEmpty(textBody) ? null : textBody,
            HtmlBody = string.IsNullOrEmpty(htmlBody) ? null : htmlBody
        };
        foreach (var a in attachments ?? new List<AttachmentDto>())
            builder.Attachments.Add(a.FileName, a.Content, ContentType.Parse(a.ContentType));

        message.Body = builder.ToMessageBody();
        return message;
    }

    private static void AddAll(InternetAddressList list, IEnumerable<EmailRecipientDto>? recipients)
    {
        foreach (var r in recipients ?? Enumerable.Empty<EmailRecipientDto>())
            if (!string.IsNullOrWhiteSpace(r.Email))
                list.Add(new MailboxAddress(r.DisplayName ?? r.Email, r.Email));
    }

    private async Task TryNotifySupportAsync(string failedSubject, List<EmailRecipientDto> intended, Exception ex)
    {
        if (string.IsNullOrWhiteSpace(_app.SupportEmail)) return;
        try
        {
            var body = $"An outgoing email failed.\n\nSubject: {failedSubject}\n" +
                       $"Intended recipients: {string.Join(", ", intended.Select(r => r.Email))}\n\n{ex}";
            // sendErrorsToSupport:false prevents an error loop if the support send also fails.
            await SendMail(
                new List<EmailRecipientDto> { new(_app.SupportEmail) },
                new List<EmailRecipientDto>(), new List<EmailRecipientDto>(),
                subject: $"[NamFix] Email delivery failed: {failedSubject}",
                textBody: body, htmlBody: string.Empty,
                attachments: new List<AttachmentDto>(), important: true, sendErrorsToSupport: false);
        }
        catch (Exception inner)
        {
            _log.LogError(inner, "Also failed to notify support about the delivery failure.");
        }
    }
}
