using System.Security.Cryptography;
using System.Text;
using NamFix.Application.Data.Repositories;
using NamFix.Application.Security;
using NamFix.Shared.Dtos;
using NamFix.Shared.Enums;

namespace NamFix.Application.Services;

public interface IEmailPreferenceService
{
    /// <summary>The full set of categories with the user's current subscription state and metadata.</summary>
    Task<List<EmailPreferenceDto>> GetForUserAsync(Guid userId);

    Task UpdateAsync(Guid userId, UpdateEmailPreferencesRequest request);

    /// <summary>Whether a category's emails should be sent to the user (locked categories always send).</summary>
    Task<bool> IsSubscribedAsync(Guid userId, EmailNotificationCategory category);

    /// <summary>Opaque, signed one-click unsubscribe token for an email footer link.</summary>
    string CreateUnsubscribeToken(Guid userId, EmailNotificationCategory category);

    /// <summary>Validate an unsubscribe token and, if valid and unlockable, opt the user out. Returns the
    /// affected category on success.</summary>
    Task<EmailNotificationCategory?> TryUnsubscribeAsync(string token);
}

/// <summary>
/// Manages per-user email-category subscriptions and the signed unsubscribe tokens used in email
/// footers. Tokens are stateless: they carry the user id + category and an HMAC over both keyed by the
/// JWT signing key, so a one-click unsubscribe needs no login and nothing pre-stored.
/// <see cref="EmailNotificationCategory.AccountSecurity"/> is transactional and cannot be turned off.
/// </summary>
public sealed class EmailPreferenceService : IEmailPreferenceService
{
    private readonly IEmailPreferenceRepository _repo;
    private readonly byte[] _key;

    public EmailPreferenceService(IEmailPreferenceRepository repo, JwtOptions jwt)
    {
        _repo = repo;
        _key = Encoding.UTF8.GetBytes(jwt.SigningKey);
    }

    private static readonly (EmailNotificationCategory Category, string Label, string Description, bool Locked)[] Catalog =
    {
        (EmailNotificationCategory.JobUpdates, "Job updates", "Status changes on your jobs and bookings.", false),
        (EmailNotificationCategory.Messages, "Messages", "New chat messages on your jobs.", false),
        (EmailNotificationCategory.Quotes, "Quotes", "Quotes received, accepted or declined.", false),
        (EmailNotificationCategory.Support, "Support", "Replies and updates on your support tickets.", false),
        (EmailNotificationCategory.AccountSecurity, "Account & security", "Password resets and account security. Always on.", true),
    };

    public async Task<List<EmailPreferenceDto>> GetForUserAsync(Guid userId)
    {
        var stored = (await _repo.ListForUserAsync(userId)).ToDictionary(p => p.Category, p => p.IsSubscribed);
        return Catalog.Select(c => new EmailPreferenceDto
        {
            Category = c.Category,
            Label = c.Label,
            Description = c.Description,
            Locked = c.Locked,
            IsSubscribed = c.Locked || (!stored.TryGetValue(c.Category, out var sub) || sub) // default subscribed
        }).ToList();
    }

    public async Task UpdateAsync(Guid userId, UpdateEmailPreferencesRequest request)
    {
        foreach (var pref in request.Preferences)
        {
            if (IsLocked(pref.Category)) continue; // can't change transactional categories
            await _repo.UpsertAsync(userId, pref.Category, pref.IsSubscribed);
        }
    }

    public async Task<bool> IsSubscribedAsync(Guid userId, EmailNotificationCategory category)
    {
        if (IsLocked(category)) return true;
        return await _repo.IsSubscribedAsync(userId, category);
    }

    public string CreateUnsubscribeToken(Guid userId, EmailNotificationCategory category)
    {
        var payload = $"{userId:N}:{(int)category}";
        return $"{Base64Url(Encoding.UTF8.GetBytes(payload))}.{Base64Url(Sign(payload))}";
    }

    public async Task<EmailNotificationCategory?> TryUnsubscribeAsync(string token)
    {
        var parts = token.Split('.', 2);
        if (parts.Length != 2) return null;

        string payload;
        try { payload = Encoding.UTF8.GetString(FromBase64Url(parts[0])); }
        catch { return null; }

        // Constant-time compare of the signature.
        if (!CryptographicOperations.FixedTimeEquals(Sign(payload), FromBase64UrlSafe(parts[1])))
            return null;

        var fields = payload.Split(':');
        if (fields.Length != 2 || !Guid.TryParseExact(fields[0], "N", out var userId)
            || !int.TryParse(fields[1], out var catInt) || !Enum.IsDefined(typeof(EmailNotificationCategory), catInt))
            return null;

        var category = (EmailNotificationCategory)catInt;
        if (IsLocked(category)) return null; // never unsubscribe transactional mail

        await _repo.UpsertAsync(userId, category, isSubscribed: false);
        return category;
    }

    private static bool IsLocked(EmailNotificationCategory category) =>
        Catalog.First(c => c.Category == category).Locked;

    private byte[] Sign(string payload)
    {
        using var hmac = new HMACSHA256(_key);
        return hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
    }

    private static byte[] FromBase64UrlSafe(string s)
    {
        try { return FromBase64Url(s); }
        catch { return Array.Empty<byte>(); }
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] FromBase64Url(string s)
    {
        var padded = s.Replace('-', '+').Replace('_', '/');
        padded = (padded.Length % 4) switch { 2 => padded + "==", 3 => padded + "=", _ => padded };
        return Convert.FromBase64String(padded);
    }
}
