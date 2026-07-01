using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using NamFix.Api.Infrastructure;
using NamFix.Application;
using NamFix.Application.Security;
using Serilog;

// Bootstrap logger so anything that fails before the host is built is still logged to the console.
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Allow non-database settings (e.g. the JWT signing key, NAMFIX_Jwt__SigningKey) to come from
    // environment variables. The database connection string is deliberately NOT taken from the
    // environment — see below; it is read exclusively from appsettings.json.
    builder.Configuration.AddEnvironmentVariables(prefix: "NAMFIX_");

    // The connection string always comes from appsettings.json, never from environment variables.
    // Read it from a dedicated appsettings-only configuration so no env var can override it.
    var appSettingsOnly = new ConfigurationBuilder()
        .SetBasePath(builder.Environment.ContentRootPath)
        .AddJsonFile("appsettings.json", optional: false)
        .Build();
    var connectionString = appSettingsOnly.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException(
            "Connection string 'DefaultConnection' is not configured. Set ConnectionStrings:DefaultConnection in NamFix.Api/appsettings.json.");

    // Serilog: console + rolling daily file under NamFix.Api/logs/. Reads optional overrides from config.
    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console()
        .WriteTo.File("logs/namfix-.log", rollingInterval: RollingInterval.Day, retainedFileCountLimit: 14));

    builder.Services.AddControllers();
    builder.Services.AddOpenApi();
    builder.Services.AddSignalR();

    // Global exception handling — logs full detail, returns a short ErrorResponse to the client.
    builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
    builder.Services.AddProblemDetails();

    // Application + data layer (Dapper repositories, services, JWT, payment abstraction).
    builder.Services.AddNamFixApplication(builder.Configuration, connectionString);

    // Realtime job + support pushes go out over SignalR (NotificationHub). The Application layer
    // raises these through IJobRealtimeNotifier / ISupportRealtimeNotifier; the host supplies the
    // SignalR-backed implementations. IPresenceTracker (who is online) is kept current by the hub.
    builder.Services.AddSingleton<NamFix.Shared.Contracts.IJobRealtimeNotifier, SignalRJobNotifier>();
    builder.Services.AddSingleton<NamFix.Shared.Contracts.ISupportRealtimeNotifier, SignalRSupportNotifier>();
    builder.Services.AddSingleton<NamFix.Shared.Contracts.IPresenceTracker, InMemoryPresenceTracker>();

    // JWT bearer authentication using the same options the token service signs with.
    var jwt = new JwtOptions();
    builder.Configuration.GetSection("Jwt").Bind(jwt);
    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwt.Issuer,
                ValidAudience = jwt.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey))
            };

            // SignalR (WebSockets) can't send an Authorization header, so the JS client passes the
            // access token in the query string. Pull it from there for hub requests.
            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    var accessToken = context.Request.Query["access_token"];
                    if (!string.IsNullOrEmpty(accessToken) &&
                        context.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                    {
                        context.Token = accessToken;
                    }
                    return Task.CompletedTask;
                }
            };
        });
    builder.Services.AddAuthorization();

    // CORS for running the WASM client on a different origin during development. SignalR needs
    // AllowCredentials, which is incompatible with a wildcard origin, so we list explicit origins.
    builder.Services.AddCors(options =>
        options.AddDefaultPolicy(p => p
            .WithOrigins(builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? Array.Empty<string>())
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials()));

    var app = builder.Build();

    // Project rule: do not start without a database connection. Fail fast with a clear fatal log.
    await app.EnsureDatabaseReachableAsync();

    // Global exception handler must be first so it wraps the whole pipeline.
    app.UseExceptionHandler();

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
    }

    app.UseSerilogRequestLogging();

    app.UseHttpsRedirection();
    app.UseCors();
    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();
    app.MapHub<StatusHub>("/hubs/status");
    app.MapHub<NotificationHub>("/hubs/notifications");

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "NamFix.Api terminated unexpectedly during startup.");
    Environment.ExitCode = 1;
}
finally
{
    Log.CloseAndFlush();
}
