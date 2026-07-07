using Microsoft.Extensions.Logging;

namespace NamFix.SharedUi.Services;

/// <summary>
/// Central sink for API call failures. Project rule: every HTTP request shows its error to the
/// client — the short message is surfaced to the UI (a toast in <c>MainLayout</c> subscribes to
/// <see cref="ErrorReported"/>), and the full detail is always written to the log.
/// </summary>
public sealed class ApiErrorNotifier
{
    private readonly ILogger<ApiErrorNotifier> _logger;

    public ApiErrorNotifier(ILogger<ApiErrorNotifier> logger) => _logger = logger;

    /// <summary>Raised with the short, user-safe message that should be displayed.</summary>
    public event Action<string>? ErrorReported;

    /// <param name="userMessage">Short message to display to the user.</param>
    /// <param name="fullDetail">Full technical detail (status, operation, exception) for the log.</param>
    public void Report(string userMessage, string fullDetail)
    {
        _logger.LogError("API error shown to user: {UserMessage} | detail: {Detail}", userMessage, fullDetail);
        ErrorReported?.Invoke(userMessage);
    }

    /// <summary>
    /// Logs an expected/handled API condition without surfacing a toast. Use for outcomes that are not
    /// user-facing failures — e.g. a 401 that races an intentional logout, where the user already meant
    /// to sign out and a "session expired" message would be misleading. The detail is still logged.
    /// </summary>
    public void LogHandled(string fullDetail) =>
        _logger.LogInformation("API call handled without user message | detail: {Detail}", fullDetail);
}
