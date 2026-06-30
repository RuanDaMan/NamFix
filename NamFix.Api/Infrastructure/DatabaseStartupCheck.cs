using NamFix.Application.Data;

namespace NamFix.Api.Infrastructure;

/// <summary>
/// Verifies the database is reachable before the host starts listening. Project rule: the API must
/// NOT start if it has no database connection — fail fast with a clear log instead of serving
/// requests that all 500 on the first query.
/// </summary>
public static class DatabaseStartupCheck
{
    public static async Task EnsureDatabaseReachableAsync(this WebApplication app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbConnectionFactory>();
        var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");

        try
        {
            using var connection = await factory.CreateOpenConnectionAsync();
            logger.LogInformation("Database connection verified; starting API.");
        }
        catch (Exception ex)
        {
            // Throw so Program.cs can log fatal via Serilog and exit non-zero — the app will not start.
            throw new InvalidOperationException(
                "Cannot start NamFix.Api: the database is not reachable. " +
                "Check ConnectionStrings:DefaultConnection in appsettings.json and that the server/DB exists.",
                ex);
        }
    }
}
