using System.Net;
using System.Text;

namespace NamFix.Application.Infrastructure.Mail;

/// <summary>
/// Renders NamFix-branded HTML emails with a consistent look that mirrors the app's styling
/// (<c>namfix.css</c> light palette — the black + gold "screw-head" identity: ink <c>#27272a</c>,
/// gold accent <c>#f59e0b</c>, card <c>#ffffff</c>, text <c>#18181b</c>, muted <c>#71717a</c>,
/// radius 12px). Email clients strip &lt;style&gt; and don't honour CSS variables, so every rule
/// here is inline. One <see cref="Render"/> builds the whole message from a heading, a body, an
/// optional call-to-action button, and an optional unsubscribe link in the footer.
/// </summary>
public sealed class EmailTemplateRenderer
{
    // Light-theme tokens copied from namfix.css :root (black + gold brand).
    private const string Ink = "#27272a";        // --nf-primary (charcoal button/brand)
    private const string InkDark = "#18181b";    // --nf-primary-dark / text
    private const string OnInk = "#ffffff";      // --nf-on-primary
    private const string Accent = "#f59e0b";     // --nf-accent (gold)
    private const string Bg = "#f4f4f5";         // --nf-bg
    private const string Card = "#ffffff";       // --nf-card
    private const string Border = "#e4e4e7";     // --nf-border
    private const string Text = "#18181b";       // --nf-text
    private const string Muted = "#71717a";      // --nf-muted

    private readonly MailAppSettings _app;

    public EmailTemplateRenderer(MailAppSettings app) => _app = app;

    /// <summary>Muted grey used for de-emphasised fine print inside a body (matches the app's --nf-muted).</summary>
    public static string MutedColor => Muted;

    /// <summary>Full HTML email. <paramref name="bodyHtml"/> is inserted verbatim (already-safe markup).</summary>
    public string Render(string heading, string bodyHtml, string? ctaText = null, string? ctaUrl = null,
        string? unsubscribeUrl = null, string? preheader = null)
    {
        var sb = new StringBuilder();
        sb.Append("<!DOCTYPE html><html><head><meta charset=\"utf-8\">")
          .Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">")
          .Append("<meta name=\"color-scheme\" content=\"light\"></head>")
          .Append($"<body style=\"margin:0;padding:0;background:{Bg};font-family:system-ui,-apple-system,'Segoe UI',Roboto,Helvetica,Arial,sans-serif;color:{Text};-webkit-font-smoothing:antialiased;\">");

        // Preheader: the grey snippet shown after the subject in most inboxes. Kept hidden in-body.
        if (!string.IsNullOrWhiteSpace(preheader))
            sb.Append($"<div style=\"display:none;max-height:0;overflow:hidden;opacity:0;mso-hide:all;\">{WebUtility.HtmlEncode(preheader)}</div>");

        sb.Append($"<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" style=\"background:{Bg};padding:28px 0;\"><tr><td align=\"center\">")
          .Append("<table role=\"presentation\" width=\"600\" cellpadding=\"0\" cellspacing=\"0\" style=\"width:600px;max-width:92%;\">");

        AppendHeader(sb);
        AppendCard(sb, heading, bodyHtml, ctaText, ctaUrl);
        AppendFooter(sb, unsubscribeUrl);

        sb.Append("</table></td></tr></table></body></html>");
        return sb.ToString();
    }

    /// <summary>Brand lockup: a gold "screw-head" badge next to the Nam/Fix wordmark.</summary>
    private static void AppendHeader(StringBuilder sb)
    {
        sb.Append("<tr><td style=\"padding:4px 4px 22px 4px;\">")
          .Append("<table role=\"presentation\" cellpadding=\"0\" cellspacing=\"0\"><tr>")
          // Screw-head mark: charcoal disc with a gold cross-slot. Border-radius degrades to a square
          // only on ancient Outlook, which is acceptable.
          .Append($"<td width=\"40\" style=\"width:40px;height:40px;background:{Ink};border-radius:50%;text-align:center;vertical-align:middle;\">")
          .Append($"<span style=\"font-size:24px;font-weight:800;line-height:40px;color:{Accent};\">+</span>")
          .Append("</td>")
          .Append("<td style=\"padding-left:11px;vertical-align:middle;\">")
          .Append($"<span style=\"font-size:23px;font-weight:800;letter-spacing:-.4px;color:{InkDark};\">Nam</span>")
          .Append($"<span style=\"font-size:23px;font-weight:800;letter-spacing:-.4px;color:{Accent};\">Fix</span>")
          .Append("</td></tr></table>")
          .Append("</td></tr>");
    }

    private void AppendCard(StringBuilder sb, string heading, string bodyHtml, string? ctaText, string? ctaUrl)
    {
        // Card with a thin gold accent bar across the top for brand flair.
        sb.Append($"<tr><td style=\"background:{Card};border:1px solid {Border};border-radius:12px;overflow:hidden;box-shadow:0 1px 3px rgba(0,0,0,.08),0 1px 2px rgba(0,0,0,.06);\">")
          .Append($"<div style=\"height:4px;background:{Accent};font-size:0;line-height:0;\">&nbsp;</div>")
          .Append("<div style=\"padding:30px 30px 26px 30px;\">")
          .Append($"<h1 style=\"margin:0 0 16px 0;font-size:20px;line-height:1.35;font-weight:700;color:{Text};\">{WebUtility.HtmlEncode(heading)}</h1>")
          .Append($"<div style=\"font-size:15px;line-height:1.65;color:{Text};\">{bodyHtml}</div>");

        if (!string.IsNullOrWhiteSpace(ctaText) && !string.IsNullOrWhiteSpace(ctaUrl))
            sb.Append("<div style=\"margin:28px 0 6px 0;\">")
              .Append($"<a href=\"{WebUtility.HtmlEncode(ctaUrl)}\" style=\"display:inline-block;background:{Ink};color:{OnInk};text-decoration:none;font-weight:600;font-size:15px;padding:12px 26px;border-radius:8px;\">{WebUtility.HtmlEncode(ctaText)} &rarr;</a>")
              .Append("</div>")
              // Fallback link so the CTA is still reachable if the button is stripped or the styling fails.
              .Append($"<div style=\"margin:14px 0 0 0;font-size:12px;line-height:1.6;color:{Muted};\">Button not working? Copy and paste this link into your browser:<br>")
              .Append($"<a href=\"{WebUtility.HtmlEncode(ctaUrl)}\" style=\"color:{Muted};word-break:break-all;\">{WebUtility.HtmlEncode(ctaUrl)}</a></div>");

        sb.Append("</div></td></tr>");
    }

    private void AppendFooter(StringBuilder sb, string? unsubscribeUrl)
    {
        var appUrl = _app.ClientBaseUrl.TrimEnd('/');
        var support = _app.SupportEmail;
        var year = DateTime.UtcNow.Year;

        sb.Append($"<tr><td style=\"padding:24px 10px 8px 10px;font-size:12px;line-height:1.7;color:{Muted};\">")
          // Tagline
          .Append($"<div style=\"font-size:13px;font-weight:600;color:{Text};margin-bottom:4px;\">NamFix</div>")
          .Append("Find &amp; pay skilled tradespeople across Namibia — plumbers, electricians, builders and more, all in one place.<br>");

        // Help line with support contact, when a support inbox is configured.
        if (!string.IsNullOrWhiteSpace(support))
            sb.Append($"Need a hand? Email us at <a href=\"mailto:{WebUtility.HtmlEncode(support)}\" style=\"color:{Ink};text-decoration:underline;\">{WebUtility.HtmlEncode(support)}</a> or ")
              .Append($"<a href=\"{WebUtility.HtmlEncode(appUrl + "/support")}\" style=\"color:{Ink};text-decoration:underline;\">open a support ticket</a>.<br>");
        else
            sb.Append($"Need a hand? <a href=\"{WebUtility.HtmlEncode(appUrl + "/support")}\" style=\"color:{Ink};text-decoration:underline;\">Open a support ticket</a> and we'll help you out.<br>");

        // Divider
        sb.Append($"<div style=\"border-top:1px solid {Border};margin:14px 0 12px 0;font-size:0;line-height:0;\">&nbsp;</div>");

        // Why-you-got-this + unsubscribe
        if (!string.IsNullOrWhiteSpace(unsubscribeUrl))
            sb.Append("You're receiving this email because of your NamFix notification settings. ")
              .Append($"<a href=\"{WebUtility.HtmlEncode(unsubscribeUrl)}\" style=\"color:{Muted};text-decoration:underline;\">Unsubscribe from these emails</a> or ")
              .Append($"<a href=\"{WebUtility.HtmlEncode(appUrl + "/settings/email")}\" style=\"color:{Muted};text-decoration:underline;\">manage your preferences</a>.<br>");
        else
            sb.Append("This is an automated message about your NamFix account.<br>");

        sb.Append($"<span style=\"color:{Muted};\">&copy; {year} NamFix &middot; Windhoek, Namibia</span>")
          .Append("</td></tr>");
    }

    /// <summary>A plain-text fallback mirroring the HTML content, for non-HTML mail clients.</summary>
    public string PlainText(string heading, string bodyText, string? ctaUrl = null, string? unsubscribeUrl = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine(heading).AppendLine();
        sb.AppendLine(bodyText);
        if (!string.IsNullOrWhiteSpace(ctaUrl))
            sb.AppendLine().AppendLine(ctaUrl);

        sb.AppendLine().AppendLine("—")
          .AppendLine("NamFix — find & pay skilled tradespeople across Namibia.");

        if (!string.IsNullOrWhiteSpace(_app.SupportEmail))
            sb.AppendLine($"Need a hand? Email {_app.SupportEmail}");

        if (!string.IsNullOrWhiteSpace(unsubscribeUrl))
            sb.AppendLine($"Unsubscribe: {unsubscribeUrl}");

        sb.AppendLine($"© {DateTime.UtcNow.Year} NamFix · Windhoek, Namibia");
        return sb.ToString();
    }
}
