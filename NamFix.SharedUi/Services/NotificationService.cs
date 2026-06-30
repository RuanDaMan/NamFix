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

    private HubConnection? _connection;
    private bool _starting;

    public NotificationService(Uri apiBaseAddress, ITokenStore tokens, NamFixApiClient api, ILogger<NotificationService> logger)
    {
        _hubUri = new Uri(apiBaseAddress, "hubs/notifications");
        _tokens = tokens;
        _api = api;
        _logger = logger;
    }

    /// <summary>Most recent notifications, newest first.</summary>
    public IReadOnlyList<NotificationDto> Notifications { get; private set; } = Array.Empty<NotificationDto>();

    public int UnreadCount => Notifications.Count(n => !n.IsRead);

    /// <summary>Raised when the notification list or unread count changes (marshal to UI thread).</summary>
    public event Action? NotificationsChanged;

    /// <summary>Raised when a booking the user participates in changed; carries the booking id.</summary>
    public event Action<Guid>? BookingChanged;

    /// <summary>Raised when a chat message is posted to a booking the user participates in.</summary>
    public event Action<Guid, BookingMessageDto>? MessagePosted;

    /// <summary>
    /// Connects the hub (if not already) and loads the initial notification list. Idempotent — safe
    /// to call from the bell on every authenticated render.
    /// </summary>
    public async Task EnsureStartedAsync()
    {
        if (_connection is not null || _starting) return;
        _starting = true;
        try
        {
            _connection = new HubConnectionBuilder()
                .WithUrl(_hubUri, options =>
                    options.AccessTokenProvider = async () => await _tokens.GetAccessTokenAsync())
                .WithAutomaticReconnect()
                .Build();

            _connection.On<NotificationDto>("Notification", n =>
            {
                Notifications = new[] { n }.Concat(Notifications).Take(30).ToList();
                NotificationsChanged?.Invoke();
            });
            _connection.On<Guid>("BookingChanged", id => BookingChanged?.Invoke(id));
            _connection.On<Guid, BookingMessageDto>("MessagePosted", (id, m) => MessagePosted?.Invoke(id, m));

            try
            {
                await _connection.StartAsync();
                _logger.LogInformation("Notifications hub connected.");
            }
            catch (Exception ex)
            {
                // Don't block the UI if realtime is unavailable — the list still loads over HTTP and
                // WithAutomaticReconnect keeps trying.
                _logger.LogWarning(ex, "Notifications hub failed to connect; will keep retrying.");
            }

            await RefreshAsync();
        }
        finally
        {
            _starting = false;
        }
    }

    /// <summary>Reloads the notification list from the API (used on startup and after marking read).</summary>
    public async Task RefreshAsync()
    {
        Notifications = await _api.GetNotificationsAsync();
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
