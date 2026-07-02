using Microsoft.Extensions.Logging;
using NamFix.Application.Data.Repositories;
using NamFix.Application.Infrastructure.Mail;
using NamFix.Shared.Dtos;

namespace NamFix.Application.Services;

public interface IInboxService
{
    /// <summary>Pull new messages from the mailbox and persist them. Invoked by the Hangfire recurring job.</summary>
    Task SyncInboxAsync();

    Task<List<InboxMessageDto>> ListAsync(int take = 100);
    Task<InboxMessageDetailDto?> GetAsync(Guid id);
}

/// <summary>
/// Bridges the POP3 reader and the inbox store: <see cref="SyncInboxAsync"/> fetches new mail and
/// persists it (de-duped by message-id), and the read methods back the admin inbox page. Failures are
/// logged, never thrown, so a broken mailbox doesn't crash the background worker.
/// </summary>
public sealed class InboxService : IInboxService
{
    private readonly IMailReaderService _reader;
    private readonly IInboxRepository _repo;
    private readonly ILogger<InboxService> _log;

    public InboxService(IMailReaderService reader, IInboxRepository repo, ILogger<InboxService> log)
    {
        _reader = reader;
        _repo = repo;
        _log = log;
    }

    public async Task SyncInboxAsync()
    {
        try
        {
            var messages = await _reader.FetchNewAsync(_repo.ExistsByMessageIdAsync);
            foreach (var m in messages)
            {
                // Double-check inside the loop in case of a race across runs.
                if (await _repo.ExistsByMessageIdAsync(m.MessageId)) continue;
                await _repo.InsertAsync(m);
            }
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Inbox sync failed.");
        }
    }

    public Task<List<InboxMessageDto>> ListAsync(int take = 100) => _repo.ListAsync(take);

    public Task<InboxMessageDetailDto?> GetAsync(Guid id) => _repo.GetAsync(id);
}
