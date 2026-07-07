namespace NamFix.SharedUi.Services;

/// <summary>
/// Tracks which open-job ids a provider has already seen on the Job board, so the nav badge counts
/// only NEW (unseen) jobs and clears once the board has been viewed. In-memory for the app session
/// (scoped == one instance for the WASM/MAUI app lifetime).
/// </summary>
public sealed class JobBoardState
{
    private readonly HashSet<Guid> _seen = new();

    /// <summary>Raised when the seen-set changes so the nav can recompute its badge.</summary>
    public event Action? Changed;

    public bool IsSeen(Guid id) => _seen.Contains(id);

    /// <summary>Marks the given open-job ids as seen (called when the board is viewed).</summary>
    public void MarkSeen(IEnumerable<Guid> ids)
    {
        var changed = false;
        foreach (var id in ids) changed |= _seen.Add(id);
        if (changed) Changed?.Invoke();
    }
}
