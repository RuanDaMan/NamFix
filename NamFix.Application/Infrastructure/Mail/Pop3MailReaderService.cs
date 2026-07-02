using MailKit;
using MailKit.Net.Pop3;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using MimeKit;
using NamFix.Shared.Domain;

namespace NamFix.Application.Infrastructure.Mail;

/// <summary>Reads new messages from the configured mailbox over POP3 (read-only — nothing is deleted
/// from the server). Callers supply a de-dupe check so only genuinely new messages are fully downloaded.</summary>
public interface IMailReaderService
{
    /// <param name="alreadyStored">Returns true if a message-id has already been persisted.</param>
    /// <param name="max">Cap on how many new messages to pull in one run.</param>
    Task<List<InboxMessage>> FetchNewAsync(Func<string, Task<bool>> alreadyStored, int max = 50);
}

public sealed class Pop3MailReaderService : IMailReaderService
{
    private readonly MailConfiguration _cfg;
    private readonly ILogger<Pop3MailReaderService> _log;

    public Pop3MailReaderService(MailConfiguration cfg, ILogger<Pop3MailReaderService> log)
    {
        _cfg = cfg;
        _log = log;
    }

    public async Task<List<InboxMessage>> FetchNewAsync(Func<string, Task<bool>> alreadyStored, int max = 50)
    {
        var result = new List<InboxMessage>();
        if (string.IsNullOrWhiteSpace(_cfg.PopHost))
        {
            _log.LogWarning("POP host not configured; inbox sync skipped.");
            return result;
        }

        using var client = new Pop3Client();
        var options = _cfg.PopTls ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTlsWhenAvailable;
        await client.ConnectAsync(_cfg.PopHost, _cfg.PopPort, options);
        await client.AuthenticateAsync(_cfg.PopUserName, _cfg.PopPassword);

        var count = client.Count;
        // Walk newest-first; stop once we've collected `max` new ones.
        for (var i = count - 1; i >= 0 && result.Count < max; i--)
        {
            // Cheap header fetch to read the Message-Id before downloading the whole body.
            var headers = await client.GetMessageHeadersAsync(i);
            var messageId = headers[HeaderId.MessageId];
            if (string.IsNullOrWhiteSpace(messageId))
                messageId = $"<no-id-{i}@{_cfg.PopHost}>"; // stable-ish fallback for this mailbox position
            if (await alreadyStored(messageId)) continue;

            var msg = await client.GetMessageAsync(i);
            var from = msg.From.Mailboxes.FirstOrDefault();
            result.Add(new InboxMessage
            {
                Id = Guid.NewGuid(),
                MessageId = messageId,
                FromName = from?.Name ?? from?.Address ?? "(unknown)",
                FromAddress = from?.Address ?? string.Empty,
                Subject = msg.Subject ?? "(no subject)",
                ReceivedAtUtc = msg.Date.UtcDateTime,
                TextBody = msg.TextBody,
                HtmlBody = msg.HtmlBody,
                FetchedAtUtc = DateTime.UtcNow
            });
        }

        await client.DisconnectAsync(true);
        _log.LogInformation("Inbox sync fetched {New} new message(s) of {Total} on the server.", result.Count, count);
        return result;
    }
}
