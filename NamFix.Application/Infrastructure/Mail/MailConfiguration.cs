namespace NamFix.Application.Infrastructure.Mail;

/// <summary>
/// SMTP + POP mailbox settings, bound from the "MailConfiguration" section of appsettings.json.
/// Ports are read as int (the config binder converts the string values in appsettings). These are
/// prefilled with dummy values in the repo — set real credentials via config/secret in production.
/// </summary>
public sealed class MailConfiguration
{
    public string PopHost { get; set; } = string.Empty;
    public int PopPort { get; set; } = 110;
    public string PopUserName { get; set; } = string.Empty;
    public string PopPassword { get; set; } = string.Empty;
    public bool PopTls { get; set; }

    public string SmtpHost { get; set; } = string.Empty;
    public int SmtpPort { get; set; } = 587;
    public bool SmtpTls { get; set; }
    public string SmtpUserName { get; set; } = string.Empty;
    public string SmtpPassword { get; set; } = string.Empty;

    public string DisplayName { get; set; } = "NamFix";
    public string FromEmail { get; set; } = string.Empty;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(SmtpHost) && !string.IsNullOrWhiteSpace(FromEmail);
}

/// <summary>
/// App-level mail settings bound from the "Mail" section: where the client is hosted (used to build
/// links inside emails), the support address failures are copied to, and the inbox poll cadence.
/// </summary>
public sealed class MailAppSettings
{
    /// <summary>Base URL of the WASM client, used to build password-reset links and email CTA buttons.</summary>
    public string ClientBaseUrl { get; set; } = "https://localhost:7213";

    /// <summary>Base URL of this API, used to build one-click unsubscribe links in email footers.</summary>
    public string ApiBaseUrl { get; set; } = "https://localhost:7111";
    public string SupportEmail { get; set; } = string.Empty;
    public int InboxPollMinutes { get; set; } = 5;
    public int PasswordResetTokenHours { get; set; } = 2;

    /// <summary>
    /// In <b>Debug</b> builds only, every outgoing email is redirected to this address so real users
    /// are never mailed during development/testing. If it is left blank, the send is skipped silently
    /// (a warning is logged). Ignored entirely in Release builds.
    /// </summary>
    public string DebugEmail { get; set; } = string.Empty;
}
