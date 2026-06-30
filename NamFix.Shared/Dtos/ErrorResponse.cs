namespace NamFix.Shared.Dtos;

/// <summary>
/// Standard error envelope returned by the API for any failed request. The <see cref="Error"/> is a
/// short, user-safe message to display; <see cref="TraceId"/> correlates it with the full server log.
/// </summary>
public record ErrorResponse
{
    public string Error { get; init; } = "Something went wrong.";

    /// <summary>Correlation id (matches the server log entry) so a user can quote it in a report.</summary>
    public string? TraceId { get; init; }
}
