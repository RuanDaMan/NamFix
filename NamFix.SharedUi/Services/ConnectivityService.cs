using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;

namespace NamFix.SharedUi.Services;

/// <summary>
/// Tracks whether the backend is reachable by holding a SignalR connection to <c>/hubs/status</c>.
/// A live connection means "online". If the initial connect fails (server down at load) or the
/// connection drops, it keeps retrying and flips back to online the moment the server returns — the
/// UI subscribes to <see cref="StateChanged"/> to show a status indicator / offline overlay.
/// </summary>
public sealed class ConnectivityService : IAsyncDisposable
{
    private readonly Uri _hubUri;
    private readonly ILogger<ConnectivityService> _logger;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(5);

    private HubConnection? _connection;
    private bool _connecting;
    private bool _stateKnown;

    public ConnectivityService(Uri apiBaseAddress, ILogger<ConnectivityService> logger)
    {
        _hubUri = new Uri(apiBaseAddress, "hubs/status");
        _logger = logger;
    }

    /// <summary>True when the backend is currently reachable.</summary>
    public bool IsOnline { get; private set; }

    /// <summary>Raised whenever <see cref="IsOnline"/> changes (marshal to the UI thread in the handler).</summary>
    public event Func<Task>? StateChanged;

    /// <summary>Builds the connection and starts the connect/retry loop. Safe to call once.</summary>
    public async Task StartAsync()
    {
        if (_connection is not null) return;

        _connection = new HubConnectionBuilder()
            .WithUrl(_hubUri)
            .Build();

        _connection.Closed += OnClosedAsync;
        await ConnectLoopAsync();
    }

    private async Task OnClosedAsync(Exception? error)
    {
        _logger.LogWarning(error, "Status hub connection closed; backend offline. Will keep retrying.");
        await SetOnlineAsync(false);
        await ConnectLoopAsync();
    }

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
                    await SetOnlineAsync(true);
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Backend unreachable; retrying in {Seconds}s.", RetryDelay.TotalSeconds);
                    await SetOnlineAsync(false);
                    await Task.Delay(RetryDelay);
                }
            }
        }
        finally
        {
            _connecting = false;
        }
    }

    private async Task SetOnlineAsync(bool online)
    {
        if (_stateKnown && IsOnline == online) return;
        IsOnline = online;
        _stateKnown = true;

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
