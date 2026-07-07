using Microsoft.AspNetCore.Http.Connections.Client;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using NamFix.Shared.Contracts;
using NamFix.Shared.Dtos;

namespace NamFix.SharedUi.Services;

/// <summary>
/// Live in-app notifications + booking updates over SignalR (<c>/hubs/notifications</c>). Holds the
/// authenticated user's recent notifications and unread count for the nav bell, and re-broadcasts
/// booking/chat events so open booking views update live. The hub connection authenticates with the
/// stored JWT (passed in the query string, since WebSockets can't send headers).
///
/// Host-agnostic: depends only on the API base <see cref="Uri"/>, <see cref="ITokenStore"/>,
/// <see cref="NamFixApiClient"/> and a logger — all available in web and a MAUI webview.
/// </summary>
public sealed class NotificationService : IAsyncDisposable
{
    private readonly Uri _hubUri;
    private readonly ITokenStore _tokens;
    private readonly NamFixApiClient _api;
    private readonly ILogger<NotificationService> _logger;
    private readonly Action<HttpConnectionOptions>? _configureConnection;

    private HubConnection? _connection;
    private bool _starting;
    private bool _connecting;

    /// <param name="configureConnection">
    /// Optional host-specific tweak to the SignalR HTTP connection (e.g. the MAUI app trusting the
    /// local dev HTTPS certificate). Left null on Blazor WebAssembly, where the browser owns TLS.
    /// </param>
    public NotificationService(
        Uri apiBaseAddress,
        ITokenStore tokens,
        NamFixApiClient api,
        ILogger<NotificationService> logger,
        Action<HttpConnectionOptions>? configureConnection = null)
    {
        _hubUri = new Uri(apiBaseAddress, "hubs/notifications");
        _tokens = tokens;
        _api = api;
        _logger = logger;
        _configureConnection = configureConnection;
    }

    /// <summary>Most recent notifications, newest first.</summary>
    public IReadOnlyList<NotificationDto> Notifications { get; private set; } = Array.Empty<NotificationDto>();

    public int UnreadCount => Notifications.Count(n => !n.IsRead);

    /// <summary>Raised when the notification list or unread count changes (marshal to UI thread).</summary>
    public event Action? NotificationsChanged;

    /// <summary>Raised when a job the user participates in changed; carries the job id.</summary>
    public event Action<Guid>? BookingChanged;

    /// <summary>Raised when a chat message is posted to a job the user participates in.</summary>
    public event Action<Guid, JobMessageDto>? MessagePosted;

    /// <summary>Raised when a support ticket the user participates in changed; carries the ticket id.</summary>
    public event Action<Guid>? TicketChanged;

    /// <summary>Raised when a message is posted to a support ticket the user participates in.</summary>
    public event Action<Guid, SupportMessageDto>? SupportMessagePosted;

    /// <summary>
    /// Connects the hub (if not already) and loads the initial notification list. Idempotent — safe
    /// to call from the bell on every authenticated render.
    /// </summary>
    public async Task EnsureStartedAsync()
    {
        if (_connection is not null || _starting) return;
        // Nothing to subscribe to while logged out. Guard here so an anonymous cold start (where a
        // window focus/visibility event triggers ReconnectIfNeededAsync) doesn't call the auth-only
        // notifications endpoint and surface a misleading "your session has expired" toast.
        if (string.IsNullOrWhiteSpace(await _tokens.GetAccessTokenAsync())) return;
        _starting = true;
        try
        {
            _connection = new HubConnectionBuilder()
                .WithUrl(_hubUri, options =>
                {
                    options.AccessTokenProvider = async () => await _tokens.GetAccessTokenAsync();
                    _configureConnection?.Invoke(options);
                })
                // Retry forever. The default policy gives up after ~1 min, which breaks live updates
                // when a mobile app is backgrounded longer than that and then resumed.
                .WithAutomaticReconnect(new InfiniteRetryPolicy())
                .Build();

            // Reload after an auto-reconnect so anything missed while offline is pulled in over HTTP.
            _connection.Reconnected += async _ =>
            {
                _logger.LogInformation("Notifications hub reconnected.");
                await RefreshAsync();
            };

            _connection.On<NotificationDto>("Notification", n =>
            {
                Notifications = new[] { n }.Concat(Notifications).Take(30).ToList();
                NotificationsChanged?.Invoke();
            });
            _connection.On<Guid>("JobChanged", id => BookingChanged?.Invoke(id));
            _connection.On<Guid, JobMessageDto>("MessagePosted", (id, m) => MessagePosted?.Invoke(id, m));
            _connection.On<Guid>("TicketChanged", id => TicketChanged?.Invoke(id));
            _connection.On<Guid, SupportMessageDto>("SupportMessagePosted", (id, m) => SupportMessagePosted?.Invoke(id, m));

            // Load the current list now (HTTP) so the bell + unread count show even before the socket
            // is up. Then connect in the background with retry — WithAutomaticReconnect only recovers a
            // connection that connected at least once, so a failed *initial* connect (cold backend on
            // first launch) would otherwise never retry until the app is reopened.
            await RefreshAsync();
            _ = ConnectWithRetryAsync();
        }
        finally
        {
            _starting = false;
        }
    }

    private async Task ConnectWithRetryAsync()
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
                    _logger.LogInformation("Notifications hub connected.");
                    await RefreshAsync(); // pull anything that arrived before the socket was up
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Notifications hub connect failed; retrying in 5s.");
                    await Task.Delay(TimeSpan.FromSeconds(5));
                }
            }
        }
        finally
        {
            _connecting = false;
        }
    }

    /// <summary>
    /// Kick an immediate reconnect if the hub is down (e.g. the app just resumed from the background,
    /// where SignalR's own keepalive may not have noticed the dead socket yet). Safe to call anytime.
    /// </summary>
    public async Task ReconnectIfNeededAsync()
    {
        if (_connection is null)
        {
            await EnsureStartedAsync();
            return;
        }
        // Always re-sync the list over HTTP (cheap, and catches anything missed while suspended)…
        await RefreshAsync();
        // …and if the socket is down, retry connecting in the background.
        if (_connection.State == HubConnectionState.Disconnected)
            _ = ConnectWithRetryAsync();
    }

    /// <summary>Reloads the notification list from the API. A failed fetch is ignored (keeps the current
    /// list) rather than wiping the bell — important on resume when the network may still be flapping.</summary>
    public async Task RefreshAsync()
    {
        // Skip the auth-only fetch when logged out (e.g. a resume/focus event on the login screen) so
        // it can't 401 into a "session expired" toast for a user who was never signed in.
        if (string.IsNullOrWhiteSpace(await _tokens.GetAccessTokenAsync())) return;
        var latest = await _api.TryGetNotificationsAsync();
        if (latest is null) return;
        Notifications = latest;
        NotificationsChanged?.Invoke();
    }

    public async Task MarkReadAsync(Guid id)
    {
        if (await _api.MarkNotificationReadAsync(id))
        {
            Notifications = Notifications
                .Select(n => n.Id == id ? n with { IsRead = true } : n)
                .ToList();
            NotificationsChanged?.Invoke();
        }
    }

    public async Task MarkAllReadAsync()
    {
        if (await _api.MarkAllNotificationsReadAsync())
        {
            Notifications = Notifications.Select(n => n with { IsRead = true }).ToList();
            NotificationsChanged?.Invoke();
        }
    }

    /// <summary>Disconnects and clears state — call on logout so the next user starts clean.</summary>
    public async Task StopAsync()
    {
        if (_connection is not null)
        {
            await _connection.DisposeAsync();
            _connection = null;
        }
        Notifications = Array.Empty<NotificationDto>();
        NotificationsChanged?.Invoke();
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
            await _connection.DisposeAsync();
    }
}

/// <summary>Reconnect policy that never gives up: exponential backoff capped at 30s.</summary>
internal sealed class InfiniteRetryPolicy : IRetryPolicy
{
    public TimeSpan? NextRetryDelay(RetryContext retryContext)
    {
        var seconds = Math.Min(30, Math.Pow(2, Math.Min(retryContext.PreviousRetryCount, 5)));
        return TimeSpan.FromSeconds(seconds);
    }
}
