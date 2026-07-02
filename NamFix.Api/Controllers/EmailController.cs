using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NamFix.Application.Infrastructure.Mail;
using NamFix.Application.Services;
using NamFix.Shared.Dtos;

namespace NamFix.Api.Controllers;

/// <summary>Email preference management for the signed-in user, plus the anonymous one-click
/// unsubscribe endpoint linked from email footers.</summary>
[Authorize]
public sealed class EmailController : ApiControllerBase
{
    private readonly IEmailPreferenceService _prefs;
    private readonly MailAppSettings _mailApp;

    public EmailController(IEmailPreferenceService prefs, MailAppSettings mailApp)
    {
        _prefs = prefs;
        _mailApp = mailApp;
    }

    [HttpGet("preferences")]
    public async Task<ActionResult<List<EmailPreferenceDto>>> GetPreferences() =>
        Ok(await _prefs.GetForUserAsync(CurrentUserId));

    [HttpPut("preferences")]
    public async Task<ActionResult> UpdatePreferences(UpdateEmailPreferencesRequest request)
    {
        await _prefs.UpdateAsync(CurrentUserId, request);
        return NoContent();
    }

    /// <summary>One-click unsubscribe from an email footer link. Anonymous — the signed token carries the
    /// user + category. Returns a small themed HTML confirmation page.</summary>
    [AllowAnonymous]
    [HttpGet("unsubscribe")]
    public async Task<ContentResult> Unsubscribe([FromQuery] string token)
    {
        var category = string.IsNullOrWhiteSpace(token) ? null : await _prefs.TryUnsubscribeAsync(token);
        var manageUrl = $"{_mailApp.ClientBaseUrl.TrimEnd('/')}/settings/email";

        var (heading, message) = category is { } c
            ? ("You're unsubscribed", $"You will no longer receive <strong>{WebUtility.HtmlEncode(FriendlyName(c))}</strong> emails from NamFix.")
            : ("Link not valid", "This unsubscribe link is invalid or has expired.");

        return new ContentResult
        {
            ContentType = "text/html",
            Content = Page(heading, message, manageUrl)
        };
    }

    private static string FriendlyName(Shared.Enums.EmailNotificationCategory c) => c switch
    {
        Shared.Enums.EmailNotificationCategory.JobUpdates => "job update",
        Shared.Enums.EmailNotificationCategory.Messages => "message",
        Shared.Enums.EmailNotificationCategory.Quotes => "quote",
        Shared.Enums.EmailNotificationCategory.Support => "support",
        _ => "notification"
    };

    private static string Page(string heading, string messageHtml, string manageUrl) =>
        "<!DOCTYPE html><html><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">" +
        "<title>NamFix</title></head>" +
        "<body style=\"margin:0;background:#f6f8fa;font-family:system-ui,-apple-system,'Segoe UI',Roboto,sans-serif;color:#1f2933;\">" +
        "<div style=\"max-width:520px;margin:64px auto;background:#fff;border:1px solid #e2e8f0;border-radius:12px;padding:32px;box-shadow:0 1px 3px rgba(0,0,0,.06);\">" +
        "<div style=\"font-size:22px;font-weight:800;margin-bottom:16px;\"><span style=\"color:#1f6feb;\">Nam</span>Fix</div>" +
        $"<h1 style=\"font-size:20px;margin:0 0 10px;\">{WebUtility.HtmlEncode(heading)}</h1>" +
        $"<p style=\"font-size:15px;line-height:1.6;color:#1f2933;\">{messageHtml}</p>" +
        $"<p style=\"margin-top:22px;\"><a href=\"{WebUtility.HtmlEncode(manageUrl)}\" style=\"display:inline-block;background:#1f6feb;color:#fff;text-decoration:none;font-weight:600;padding:10px 20px;border-radius:8px;\">Manage email settings</a></p>" +
        "</div></body></html>";
}
