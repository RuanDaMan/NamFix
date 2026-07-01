using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;

namespace NamFix.SharedUi.Components;

/// <summary>
/// An <see cref="ErrorBoundary"/> that logs the full exception before showing its error content.
/// Without this, an unhandled render/lifecycle exception falls through to Blazor's opaque
/// "An unhandled error has occurred" bar with nothing useful in the console — this makes the real
/// cause visible in the logger (browser console in WASM) while keeping the UI recoverable.
/// </summary>
public sealed class LoggingErrorBoundary : ErrorBoundary
{
    [Inject] private ILogger<LoggingErrorBoundary> Logger { get; set; } = default!;

    protected override Task OnErrorAsync(Exception exception)
    {
        Logger.LogError(exception, "Unhandled UI exception caught by the error boundary.");
        return base.OnErrorAsync(exception);
    }
}
