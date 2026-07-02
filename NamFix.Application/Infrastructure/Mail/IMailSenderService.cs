using Hangfire;
using NamFix.Shared.Dtos;

namespace NamFix.Application.Infrastructure.Mail;

/// <summary>
/// The single mail-sending abstraction for the platform. All outgoing mail (notification emails,
/// password resets, error copies) goes through one implementation (<see cref="SmtpMailSenderService"/>).
///
/// <para><see cref="SendMailInBackground"/> is the entry point enqueued on Hangfire: it is decorated
/// with <c>[AutomaticRetry(Attempts = 2)]</c> so a transient SMTP failure is retried twice by the
/// background worker rather than on the request thread. The <see cref="SendMail"/> overloads send
/// inline (awaitable) for callers that need the result.</para>
/// </summary>
public interface IMailSenderService
{
    [AutomaticRetry(Attempts = 2)]
    void SendMailInBackground(List<EmailRecipientDto> recipients, List<EmailRecipientDto> ccRecipients, string subject, string textBody, string htmlBody, List<AttachmentDto> attachments, bool important = false);

    Task SendMail(List<EmailRecipientDto> recipients, List<EmailRecipientDto> ccRecipients, string subject, string textBody, string htmlBody, List<AttachmentDto> attachments, bool important = false);

    Task SendMail(List<EmailRecipientDto> recipients, List<EmailRecipientDto> ccRecipients, List<EmailRecipientDto> bccRecipients, string subject, string textBody, string htmlBody,
        List<AttachmentDto> attachments, bool important = false, bool sendErrorsToSupport = true);
}
