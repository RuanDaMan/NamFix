using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NamFix.Application.Data;
using NamFix.Application.Data.Repositories;
using NamFix.Application.Infrastructure;
using NamFix.Application.Infrastructure.Mail;
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
        services.AddScoped<IJobRepository, JobRequestRepository>();
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<ISupportRepository, SupportRepository>();
        services.AddScoped<IAvailabilityRepository, AvailabilityRepository>();
        services.AddScoped<IRateCardRepository, RateCardRepository>();
        services.AddScoped<IPlatformSettingsRepository, PlatformSettingsRepository>();
        services.AddScoped<IEmailPreferenceRepository, EmailPreferenceRepository>();
        services.AddScoped<IInboxRepository, InboxRepository>();

        // Mail: SMTP send + POP read, config-bound options, and the HTML template renderer. The
        // background sender (SendMailInBackground) is invoked by Hangfire, which the API host registers.
        var mailConfig = new MailConfiguration();
        configuration.GetSection("MailConfiguration").Bind(mailConfig);
        services.AddSingleton(mailConfig);

        var mailApp = new MailAppSettings();
        configuration.GetSection("Mail").Bind(mailApp);
        services.AddSingleton(mailApp);

        services.AddSingleton<EmailTemplateRenderer>();
        services.AddScoped<IMailSenderService, SmtpMailSenderService>();
        services.AddScoped<IMailReaderService, Pop3MailReaderService>();

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
        services.AddScoped<IJobService, JobRequestService>();
        services.AddScoped<IAvailabilityService, AvailabilityService>();
        services.AddScoped<IRateCardService, RateCardService>();
        services.AddScoped<IPlatformSettingsService, PlatformSettingsService>();
        services.AddScoped<ISupportService, SupportService>();
        services.AddScoped<IUserAdminService, UserAdminService>();
        services.AddScoped<INotificationDispatcher, NotificationDispatcher>();
        services.AddScoped<IEmailPreferenceService, EmailPreferenceService>();
        services.AddScoped<IInboxService, InboxService>();

        return services;
    }
}
