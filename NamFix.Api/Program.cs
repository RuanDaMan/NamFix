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

    // Allow the JWT signing key + connection string to come from environment variables in any
    // environment. e.g. NAMFIX_Jwt__SigningKey, NAMFIX_ConnectionStrings__NamFix.
    builder.Configuration.AddEnvironmentVariables(prefix: "NAMFIX_");

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
    builder.Services.AddNamFixApplication(builder.Configuration);

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

    // Serve the Blazor WebAssembly client hosted from this API.
    app.UseBlazorFrameworkFiles();
    app.UseStaticFiles();

    app.UseSerilogRequestLogging();

    app.UseHttpsRedirection();
    app.UseCors();
    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();
    app.MapHub<StatusHub>("/hubs/status");

    // SPA fallback so client-side routes resolve to the WASM host page.
    app.MapFallbackToFile("index.html");

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
