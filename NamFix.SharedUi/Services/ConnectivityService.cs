using Microsoft.AspNetCore.Http.Connections.Client;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;

namespace NamFix.SharedUi.Services;

/// <summary>
/// Where the backend connection currently stands. On a cold start we begin in <see cref="Connecting"/>
/// (show a "getting things ready" loader, not an alarming offline message) and only fall to
/// <see cref="Offline"/> if the first connect doesn't succeed within the initial grace window.
/// </summary>
public enum ConnectivityPhase
{
    Connecting,
    Online,
    Offline
}

/// <summary>
/// Tracks whether the backend is reachable by holding a SignalR connection to <c>/hubs/status</c>.
/// A live connection means "online". If the initial connect fails (server down at load) or the
/// connection drops, it keeps retrying and flips back to online the moment the server returns — the
/// UI subscribes to <see cref="StateChanged"/> to show a status indicator / offline overlay.
///
/// On first load it stays in <see cref="ConnectivityPhase.Connecting"/> during a short grace window so
/// the user sees a loading screen rather than "you're offline" flashing before the first connect lands.
/// </summary>
public sealed class ConnectivityService : IAsyncDisposable
{
    private readonly Uri _hubUri;
    private readonly ILogger<ConnectivityService> _logger;
    private readonly Action<HttpConnectionOptions>? _configureConnection;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(5);
    // How long to keep showing "connecting" before conceding "offline" on a cold start.
    private static readonly TimeSpan InitialGrace = TimeSpan.FromSeconds(8);

    private HubConnection? _connection;
    private bool _connecting;
    private bool _everConnected;

    /// <param name="configureConnection">
    /// Optional host-specific tweak to the SignalR HTTP connection (e.g. the MAUI app trusting the
    /// local dev HTTPS certificate). Left null on Blazor WebAssembly, where the browser owns TLS.
    /// </param>
    public ConnectivityService(
        Uri apiBaseAddress,
        ILogger<ConnectivityService> logger,
        Action<HttpConnectionOptions>? configureConnection = null)
    {
        _hubUri = new Uri(apiBaseAddress, "hubs/status");
        _logger = logger;
        _configureConnection = configureConnection;
    }

    /// <summary>The current connection phase (connecting / online / offline).</summary>
    public ConnectivityPhase Phase { get; private set; } = ConnectivityPhase.Connecting;

    /// <summary>True when the backend is currently reachable.</summary>
    public bool IsOnline => Phase == ConnectivityPhase.Online;

    /// <summary>Raised whenever <see cref="Phase"/> changes (marshal to the UI thread in the handler).</summary>
    public event Func<Task>? StateChanged;

    /// <summary>Builds the connection and starts the connect/retry loop. Safe to call once.</summary>
    public async Task StartAsync()
    {
        if (_connection is not null) return;

        _connection = new HubConnectionBuilder()
            .WithUrl(_hubUri, options => _configureConnection?.Invoke(options))
            .Build();

        _connection.Closed += OnClosedAsync;
        // Concede "offline" if the cold-start connect hasn't landed by the end of the grace window.
        _ = GraceTimeoutAsync();
        await ConnectLoopAsync();
    }

    private async Task GraceTimeoutAsync()
    {
        await Task.Delay(InitialGrace);
        if (Phase == ConnectivityPhase.Connecting)
            await SetPhaseAsync(ConnectivityPhase.Offline);
    }

    private async Task OnClosedAsync(Exception? error)
    {
        _logger.LogWarning(error, "Status hub connection closed; backend offline. Will keep retrying.");
        await SetPhaseAsync(ConnectivityPhase.Offline);
        await ConnectLoopAsync();
    }

    /// <summary>
    /// Nudge an immediate reconnect attempt — e.g. the app just resumed from the background, where the
    /// 5s retry timer may have been frozen. No-op if already connected or a connect loop is running.
    /// </summary>
    public Task PokeAsync() => _connection is null ? Task.CompletedTask : ConnectLoopAsync();

    private async Task ConnectLoopAsync()
    {
        if (_connecting) return;
        _connecting = true;
        try
        {
            while (_connection is { State: HubConnectionState.Disconnected })
            {
                try
                {
                    await _connection.StartAsync();
                    _logger.LogInformation("Status hub connected; backend online.");
                    await SetPhaseAsync(ConnectivityPhase.Online);
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Backend unreachable; retrying in {Seconds}s.", RetryDelay.TotalSeconds);
                    // On a cold start stay in "connecting" until the grace window lapses (handled by
                    // GraceTimeoutAsync); once we've ever been online, a failure means we're offline now.
                    if (_everConnected)
                        await SetPhaseAsync(ConnectivityPhase.Offline);
                    await Task.Delay(RetryDelay);
                }
            }
        }
        finally
        {
            _connecting = false;
        }
    }

    private async Task SetPhaseAsync(ConnectivityPhase phase)
    {
        if (Phase == phase) return;
        Phase = phase;
        if (phase == ConnectivityPhase.Online) _everConnected = true;

        if (StateChanged is not null)
            await StateChanged.Invoke();
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
        {
            _connection.Closed -= OnClosedAsync;
            await _connection.DisposeAsync();
            _connection = null;
        }
    }
}
