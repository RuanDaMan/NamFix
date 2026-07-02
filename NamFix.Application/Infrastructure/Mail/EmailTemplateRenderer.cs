using System.Net;
using System.Text;

namespace NamFix.Application.Infrastructure.Mail;

/// <summary>
/// Renders NamFix-branded HTML emails with a consistent look that mirrors the app's styling
/// (<c>namfix.css</c> light palette: primary <c>#1f6feb</c>, card <c>#ffffff</c>, text <c>#1f2933</c>,
/// muted <c>#6b7280</c>, radius 12px). Email clients strip &lt;style&gt; and don't honour CSS
/// variables, so every rule here is inline. One <see cref="Render"/> builds the whole message from a
/// heading, a body, an optional call-to-action button, and an optional unsubscribe link in the footer.
/// </summary>
public sealed class EmailTemplateRenderer
{
    // Light-theme tokens copied from namfix.css :root.
    private const string Primary = "#1f6feb";
    private const string OnPrimary = "#ffffff";
    private const string Bg = "#f6f8fa";
    private const string Card = "#ffffff";
    private const string Border = "#e2e8f0";
    private const string Text = "#1f2933";
    private const string Muted = "#6b7280";

    /// <summary>Full HTML email. <paramref name="bodyHtml"/> is inserted verbatim (already-safe markup).</summary>
    public string Render(string heading, string bodyHtml, string? ctaText = null, string? ctaUrl = null,
        string? unsubscribeUrl = null, string? preheader = null)
    {
        var sb = new StringBuilder();
        sb.Append("<!DOCTYPE html><html><head><meta charset=\"utf-8\">")
          .Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\"></head>")
          .Append($"<body style=\"margin:0;padding:0;background:{Bg};font-family:system-ui,-apple-system,'Segoe UI',Roboto,sans-serif;color:{Text};\">");

        if (!string.IsNullOrWhiteSpace(preheader))
            sb.Append($"<div style=\"display:none;max-height:0;overflow:hidden;opacity:0;\">{WebUtility.HtmlEncode(preheader)}</div>");

        sb.Append($"<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" style=\"background:{Bg};padding:24px 0;\"><tr><td align=\"center\">")
          .Append("<table role=\"presentation\" width=\"600\" cellpadding=\"0\" cellspacing=\"0\" style=\"width:600px;max-width:92%;\">");

        // Header / wordmark
        sb.Append("<tr><td style=\"padding:8px 4px 20px 4px;\">")
          .Append($"<span style=\"font-size:22px;font-weight:800;letter-spacing:-.3px;color:{Primary};\">Nam</span>")
          .Append($"<span style=\"font-size:22px;font-weight:800;letter-spacing:-.3px;color:{Text};\">Fix</span>")
          .Append("</td></tr>");

        // Card
        sb.Append($"<tr><td style=\"background:{Card};border:1px solid {Border};border-radius:12px;padding:28px 28px 24px 28px;box-shadow:0 1px 3px rgba(0,0,0,.06);\">")
          .Append($"<h1 style=\"margin:0 0 14px 0;font-size:19px;line-height:1.35;color:{Text};\">{WebUtility.HtmlEncode(heading)}</h1>")
          .Append($"<div style=\"font-size:15px;line-height:1.6;color:{Text};\">{bodyHtml}</div>");

        if (!string.IsNullOrWhiteSpace(ctaText) && !string.IsNullOrWhiteSpace(ctaUrl))
            sb.Append("<div style=\"margin:26px 0 6px 0;\">")
              .Append($"<a href=\"{WebUtility.HtmlEncode(ctaUrl)}\" style=\"display:inline-block;background:{Primary};color:{OnPrimary};text-decoration:none;font-weight:600;font-size:15px;padding:11px 22px;border-radius:8px;\">{WebUtility.HtmlEncode(ctaText)}</a>")
              .Append("</div>");

        sb.Append("</td></tr>");

        // Footer
        sb.Append($"<tr><td style=\"padding:20px 8px;font-size:12px;line-height:1.6;color:{Muted};\">")
          .Append("NamFix — find & pay skilled tradespeople in Namibia.<br>");
        if (!string.IsNullOrWhiteSpace(unsubscribeUrl))
            sb.Append($"You're receiving this because of your notification settings. <a href=\"{WebUtility.HtmlEncode(unsubscribeUrl)}\" style=\"color:{Muted};\">Unsubscribe from these emails</a>.");
        sb.Append("</td></tr>");

        sb.Append("</table></td></tr></table></body></html>");
        return sb.ToString();
    }

    /// <summary>A plain-text fallback mirroring the HTML content, for non-HTML mail clients.</summary>
    public string PlainText(string heading, string bodyText, string? ctaUrl = null, string? unsubscribeUrl = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine(heading).AppendLine();
        sb.AppendLine(bodyText);
        if (!string.IsNullOrWhiteSpace(ctaUrl))
            sb.AppendLine().AppendLine(ctaUrl);
        sb.AppendLine().AppendLine("—").AppendLine("NamFix — find & pay skilled tradespeople in Namibia.");
        if (!string.IsNullOrWhiteSpace(unsubscribeUrl))
            sb.AppendLine($"Unsubscribe: {unsubscribeUrl}");
        return sb.ToString();
    }
}
