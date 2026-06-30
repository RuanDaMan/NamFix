using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using NamFix.Shared.Dtos;

namespace NamFix.Api.Infrastructure;

/// <summary>
/// Catches any exception that escapes a request, logs the FULL detail (Serilog), and returns a short,
/// user-safe <see cref="ErrorResponse"/> with a trace id. Enforces the project rule that every HTTP
/// request handles its error — short message to the client, full detail to the log.
/// </summary>
public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) => _logger = logger;

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var traceId = httpContext.TraceIdentifier;

        // Full detail is logged for the developer; the user only ever sees the short message below.
        _logger.LogError(exception,
            "Unhandled exception for {Method} {Path} (TraceId {TraceId})",
            httpContext.Request.Method, httpContext.Request.Path, traceId);

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await httpContext.Response.WriteAsJsonAsync(new ErrorResponse
        {
            Error = "An unexpected error occurred. Please try again.",
            TraceId = traceId
        }, cancellationToken);

        return true; // handled — stops the default developer/exception page
    }
}
