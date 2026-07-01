using System.Collections.Concurrent;
using NamFix.Shared.Contracts;

namespace NamFix.Api.Infrastructure;

/// <summary>
/// In-memory <see cref="IPresenceTracker"/> — a per-process count of live authenticated connections
/// per user. A user is "online" while they hold at least one connection. Updated by
/// <see cref="NotificationHub"/> on connect/disconnect. Registered as a singleton so all requests share
/// one view. (Single-instance only; a scaled-out deployment would back this with Redis/backplane.)
/// </summary>
public sealed class InMemoryPresenceTracker : IPresenceTracker
{
    private readonly ConcurrentDictionary<Guid, int> _connectionCounts = new();

    public Task UserConnectedAsync(Guid userId)
    {
        _connectionCounts.AddOrUpdate(userId, 1, (_, count) => count + 1);
        return Task.CompletedTask;
    }

    public Task UserDisconnectedAsync(Guid userId)
    {
        _connectionCounts.AddOrUpdate(userId, 0, (_, count) => count - 1);
        // Drop the entry once no connections remain so OnlineUserIds stays clean.
        if (_connectionCounts.TryGetValue(userId, out var remaining) && remaining <= 0)
            _connectionCounts.TryRemove(new KeyValuePair<Guid, int>(userId, remaining));
        return Task.CompletedTask;
    }

    public bool IsOnline(Guid userId) =>
        _connectionCounts.TryGetValue(userId, out var count) && count > 0;

    public IReadOnlyCollection<Guid> OnlineUserIds =>
        _connectionCounts.Where(kv => kv.Value > 0).Select(kv => kv.Key).ToList();
}
