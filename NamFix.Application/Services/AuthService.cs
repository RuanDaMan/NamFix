using System.Security.Cryptography;
using Hangfire;
using Microsoft.Extensions.Logging;
using NamFix.Application.Data.Repositories;
using NamFix.Application.Infrastructure.Mail;
using NamFix.Application.Security;
using NamFix.Shared.Domain;
using NamFix.Shared.Dtos;

namespace NamFix.Application.Services;

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request);
    Task<AuthResponse> LoginAsync(LoginRequest request);
    Task<AuthResponse> RefreshAsync(string refreshToken);

    /// <summary>Start password recovery: email a reset link if the account exists. Always succeeds
    /// (never reveals whether the email is registered).</summary>
    Task ForgotPasswordAsync(ForgotPasswordRequest request);

    /// <summary>Complete password recovery with a valid, unexpired, unused token.</summary>
    Task ResetPasswordAsync(ResetPasswordWithTokenRequest request);
}

/// <summary>Thrown for auth failures so the API can translate to 400/401 without leaking detail.</summary>
public sealed class AuthException(string message) : Exception(message);

public sealed class AuthService : IAuthService
{
    private readonly IUserRepository _users;
    private readonly IPasswordHasher _hasher;
    private readonly IJwtTokenService _jwt;
    private readonly JwtOptions _jwtOptions;
    private readonly IBackgroundJobClient _jobs;
    private readonly EmailTemplateRenderer _templates;
    private readonly MailAppSettings _mailApp;
    private readonly ILogger<AuthService> _log;

    public AuthService(IUserRepository users, IPasswordHasher hasher, IJwtTokenService jwt, JwtOptions jwtOptions,
        IBackgroundJobClient jobs, EmailTemplateRenderer templates, MailAppSettings mailApp, ILogger<AuthService> log)
    {
        _users = users;
        _hasher = hasher;
        _jwt = jwt;
        _jwtOptions = jwtOptions;
        _jobs = jobs;
        _templates = templates;
        _mailApp = mailApp;
        _log = log;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
    {
        var existing = await _users.GetByEmailAsync(request.Email);
        if (existing is not null)
            throw new AuthException("An account with this email already exists.");

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = request.Email.Trim().ToLowerInvariant(),
            PhoneNumber = request.PhoneNumber,
            FullName = request.FullName.Trim(),
            Role = request.Role,
            PasswordHash = _hasher.Hash(request.Password),
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };
        await _users.InsertAsync(user);
        return await IssueTokensAsync(user);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request)
    {
        var user = await _users.GetByEmailAsync(request.Email.Trim().ToLowerInvariant());
        if (user is null || !user.IsActive || !_hasher.Verify(request.Password, user.PasswordHash))
            throw new AuthException("Invalid email or password.");

        return await IssueTokensAsync(user);
    }

    public async Task<AuthResponse> RefreshAsync(string refreshToken)
    {
        var stored = await _users.GetRefreshTokenAsync(refreshToken);
        if (stored is null || !stored.IsActive)
            throw new AuthException("Invalid or expired refresh token.");

        var user = await _users.GetByIdAsync(stored.UserId)
            ?? throw new AuthException("User no longer exists.");

        // Rotate: revoke the used token and issue a fresh pair.
        await _users.RevokeRefreshTokenAsync(stored.Id);
        return await IssueTokensAsync(user);
    }

    public async Task ForgotPasswordAsync(ForgotPasswordRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _users.GetByEmailAsync(email);

        // Never reveal whether the account exists — just stop quietly if it doesn't (or is disabled).
        if (user is null || !user.IsActive)
        {
            _log.LogInformation("Password reset requested for unknown/inactive email {Email}; no mail sent.", email);
            return;
        }

        var token = UrlSafeToken();
        await _users.AddPasswordResetTokenAsync(new PasswordResetToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Token = token,
            CreatedAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = DateTime.UtcNow.AddHours(Math.Max(1, _mailApp.PasswordResetTokenHours))
        });

        var resetUrl = $"{_mailApp.ClientBaseUrl.TrimEnd('/')}/reset-password?token={Uri.EscapeDataString(token)}";
        var heading = "Reset your NamFix password";
        var bodyHtml =
            $"<p>Hi {System.Net.WebUtility.HtmlEncode(user.FullName)},</p>" +
            "<p>We received a request to reset your NamFix password. Click the button below to choose a new one. " +
            $"This link expires in {Math.Max(1, _mailApp.PasswordResetTokenHours)} hour(s).</p>" +
            "<p style=\"color:#6b7280;font-size:13px;\">If you didn't request this, you can safely ignore this email — your password won't change.</p>";
        var html = _templates.Render(heading, bodyHtml, "Reset password", resetUrl);
        var text = _templates.PlainText(heading,
            "We received a request to reset your NamFix password. Open the link below to choose a new one.", resetUrl);

        // AccountSecurity mail is always sent (no unsubscribe). Enqueue so a slow SMTP never blocks the request.
        _jobs.Enqueue<IMailSenderService>(x => x.SendMailInBackground(
            new List<EmailRecipientDto> { new(user.Email, user.FullName) },
            new List<EmailRecipientDto>(), heading, text, html, new List<AttachmentDto>(), true));
    }

    public async Task ResetPasswordAsync(ResetPasswordWithTokenRequest request)
    {
        var stored = await _users.GetPasswordResetTokenAsync(request.Token);
        if (stored is null || !stored.IsRedeemable)
            throw new AuthException("This password reset link is invalid or has expired. Request a new one.");

        var user = await _users.GetByIdAsync(stored.UserId)
            ?? throw new AuthException("This password reset link is invalid or has expired. Request a new one.");

        await _users.UpdatePasswordHashAsync(user.Id, _hasher.Hash(request.NewPassword));
        await _users.MarkPasswordResetTokenUsedAsync(stored.Id);
        // Force re-login everywhere: any stolen/old sessions are cut off.
        await _users.RevokeAllRefreshTokensForUserAsync(user.Id);

        _log.LogInformation("Password reset completed for user {UserId}.", user.Id);
    }

    private static string UrlSafeToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(48))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private async Task<AuthResponse> IssueTokensAsync(User user)
    {
        var (accessToken, expiresAt) = _jwt.CreateAccessToken(user);
        var refresh = _jwt.CreateRefreshToken();

        await _users.AddRefreshTokenAsync(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Token = refresh,
            CreatedAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = DateTime.UtcNow.AddDays(_jwtOptions.RefreshTokenDays)
        });

        return new AuthResponse
        {
            AccessToken = accessToken,
            RefreshToken = refresh,
            ExpiresAtUtc = expiresAt,
            User = new UserDto
            {
                Id = user.Id,
                Email = user.Email,
                FullName = user.FullName,
                PhoneNumber = user.PhoneNumber,
                Role = user.Role
            }
        };
    }
}
