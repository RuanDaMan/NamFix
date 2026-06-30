using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NamFix.Application.Data;
using NamFix.Application.Data.Repositories;
using NamFix.Application.Infrastructure;
using NamFix.Application.Security;
using NamFix.Application.Services;
using NamFix.Shared.Contracts;

namespace NamFix.Application;

/// <summary>
/// Single entry point for wiring the application + data layer into the API host's DI container.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddNamFixApplication(this IServiceCollection services, IConfiguration configuration, string connectionString)
    {
        // Bind JWT options from configuration (Jwt section); SigningKey should come from a secret/env var.
        var jwtOptions = new JwtOptions();
        configuration.GetSection("Jwt").Bind(jwtOptions);
        services.AddSingleton(jwtOptions);

        // Data access — connection string is resolved by the host from appsettings.json only.
        services.AddSingleton<IDbConnectionFactory>(_ => new SqlConnectionFactory(connectionString));
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IProviderRepository, ProviderRepository>();
        services.AddScoped<ITaxonomyRepository, TaxonomyRepository>();
        services.AddScoped<IReviewRepository, ReviewRepository>();
        services.AddScoped<ITransactionRepository, TransactionRepository>();
        services.AddScoped<ICommissionRepository, CommissionRepository>();
        services.AddScoped<IBookingRepository, BookingRepository>();
        services.AddScoped<INotificationRepository, NotificationRepository>();

        // Security
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<IJwtTokenService, JwtTokenService>();

        // Payment gateway abstraction — swap StubPaymentService for a real provider later.
        services.AddSingleton<IPaymentService, StubPaymentService>();

        // Application services
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IProviderService, ProviderService>();
        services.AddScoped<ITransactionService, TransactionService>();
        services.AddScoped<ITaxonomyService, TaxonomyService>();
        services.AddScoped<IReviewService, ReviewService>();
        services.AddScoped<IAdminService, AdminService>();
        services.AddScoped<IBookingService, BookingService>();

        return services;
    }
}
