using LeaseLense.Application.Profile;

namespace LeaseLense.Web.Services;

public static class LeaseLenseEmailHtmlTemplates
{
    private const string TextColor = "#16364b";
    private const string MutedColor = "#4f6f84";
    private const string AccentColor = "#2a66d9";
    private const string AccentCyan = "#0ea5e9";
    private const string HeaderBg = "#f2f9ff";
    private const string BorderColor = "#e3eef6";
    private const string CardBg = "#fbfdff";

    public static string Wrap(string title, string innerHtml)
    {
        return $"""
            <!DOCTYPE html>
            <html lang="en">
            <head>
              <meta charset="utf-8" />
              <meta name="viewport" content="width=device-width, initial-scale=1" />
              <title>{System.Net.WebUtility.HtmlEncode(title)}</title>
            </head>
            <body style="margin:0;padding:0;background:#ffffff;font-family:'Segoe UI',Inter,Roboto,Helvetica,Arial,sans-serif;line-height:1.55;color:{TextColor};">
              <table role="presentation" cellpadding="0" cellspacing="0" width="100%" style="background:linear-gradient(135deg,#ffffff 0%,#ecf5fb 50%,#ffffff 100%);padding:24px 16px;">
                <tr>
                  <td align="center">
                    <table role="presentation" cellpadding="0" cellspacing="0" width="100%" style="max-width:560px;background:{CardBg};border-radius:18px;border:1px solid {BorderColor};box-shadow:0 16px 34px rgba(27,75,110,0.12);overflow:hidden;">
                      <tr>
                        <td style="background:{HeaderBg};padding:20px 24px;border-bottom:1px solid {BorderColor};">
                          <table role="presentation" width="100%" cellpadding="0" cellspacing="0">
                            <tr>
                              <td style="vertical-align:middle;">
                                <span style="display:inline-block;width:10px;height:10px;border-radius:999px;background:linear-gradient(130deg,{AccentColor},{AccentCyan});box-shadow:0 0 0 4px rgba(42,102,217,0.1);vertical-align:middle;margin-right:10px;"></span>
                                <span style="font-family:'Sora','Segoe UI',sans-serif;font-size:20px;font-weight:700;color:#205473;letter-spacing:0.2px;vertical-align:middle;">LeaseLense</span>
                              </td>
                            </tr>
                          </table>
                        </td>
                      </tr>
                      <tr>
                        <td style="padding:24px 24px 28px;">
                          {innerHtml}
                        </td>
                      </tr>
                      <tr>
                        <td style="padding:16px 24px 22px;border-top:1px solid {BorderColor};background:#f7fbfe;">
                          <p style="margin:0;font-size:12px;color:{MutedColor};">You are receiving this email because of activity on your LeaseLense account.</p>
                        </td>
                      </tr>
                    </table>
                  </td>
                </tr>
              </table>
            </body>
            </html>
            """;
    }

    public static string BuildEmailVerificationHtml(string verificationUrl)
    {
        var safeUrl = System.Net.WebUtility.HtmlEncode(verificationUrl);
        var inner = $"""
            <p style="margin:0 0 16px;font-size:16px;">Welcome to LeaseLense. Please confirm your email address to finish setting up your account.</p>
            <table role="presentation" cellpadding="0" cellspacing="0" style="margin:20px 0;">
              <tr>
                <td style="border-radius:12px;background:linear-gradient(130deg,{AccentColor},{AccentCyan});">
                  <a href="{safeUrl}" style="display:inline-block;padding:14px 28px;font-size:15px;font-weight:600;color:#ffffff;text-decoration:none;border-radius:12px;">Verify email</a>
                </td>
              </tr>
            </table>
            <p style="margin:16px 0 0;font-size:13px;color:{MutedColor};">If the button does not work, copy and paste this link into your browser:</p>
            <p style="margin:8px 0 0;font-size:12px;word-break:break-all;color:{AccentColor};">{safeUrl}</p>
            <p style="margin:20px 0 0;font-size:14px;color:{MutedColor};">If you did not create this account, you can ignore this message.</p>
            """;
        return Wrap("Verify your email", inner);
    }

    public static string BuildResidencyInProcessHtml()
    {
        var inner = $"""
            <p style="margin:0 0 12px;font-size:16px;">Thanks for submitting your residency document.</p>
            <p style="margin:0;font-size:15px;color:{MutedColor};">We are processing your verification. You will receive another email when we have an update.</p>
            """;
        return Wrap("Verification in progress", inner);
    }

    public static string BuildResidencyDecisionHtml(ResidencyDecisionEmailContext context)
    {
        var statusLabel = ResidencyDecisionEmailFormatter.ToDisplayStatus(context.Status);
        var summary = ResidencyVerificationDisplayHelper.BuildCustomerSummary(
            context.Status,
            context.ExtractedName,
            context.ExtractedAddress);
        var providedAddress = ResidencyDecisionEmailFormatter.FormatMailingAddress(context);
        var extractedName = string.IsNullOrWhiteSpace(context.ExtractedName) ? "Not read from document" : context.ExtractedName;
        var extractedAddress = string.IsNullOrWhiteSpace(context.ExtractedAddress) ? "Not read from document" : context.ExtractedAddress;
        var displayName = string.IsNullOrWhiteSpace(context.DisplayName) ? "Not on file" : context.DisplayName;

        var inner = $"""
            <p style="margin:0 0 8px;font-size:13px;font-weight:600;text-transform:uppercase;letter-spacing:0.06em;color:{MutedColor};">Status</p>
            <p style="margin:0 0 18px;font-size:18px;font-weight:700;color:{AccentColor};">{System.Net.WebUtility.HtmlEncode(statusLabel)}</p>
            <p style="margin:0 0 20px;font-size:15px;">{System.Net.WebUtility.HtmlEncode(summary)}</p>
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="border-collapse:separate;border-spacing:0 12px;">
              <tr>
                <td style="padding:14px 16px;background:#ffffff;border:1px solid {BorderColor};border-radius:14px;">
                  <p style="margin:0 0 6px;font-size:12px;font-weight:600;text-transform:uppercase;letter-spacing:0.04em;color:{MutedColor};">Name on your account</p>
                  <p style="margin:0;font-size:15px;">{System.Net.WebUtility.HtmlEncode(displayName)}</p>
                </td>
              </tr>
              <tr>
                <td style="padding:14px 16px;background:#ffffff;border:1px solid {BorderColor};border-radius:14px;">
                  <p style="margin:0 0 6px;font-size:12px;font-weight:600;text-transform:uppercase;letter-spacing:0.04em;color:{MutedColor};">Address on your account</p>
                  <p style="margin:0;font-size:15px;">{System.Net.WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(providedAddress) ? "Not on file" : providedAddress)}</p>
                </td>
              </tr>
              <tr>
                <td style="padding:14px 16px;background:#ffffff;border:1px solid {BorderColor};border-radius:14px;">
                  <p style="margin:0 0 6px;font-size:12px;font-weight:600;text-transform:uppercase;letter-spacing:0.04em;color:{MutedColor};">From your document</p>
                  <p style="margin:0 0 6px;font-size:15px;"><strong style="color:{MutedColor};font-size:12px;">Name</strong><br />{System.Net.WebUtility.HtmlEncode(extractedName)}</p>
                  <p style="margin:0;font-size:15px;"><strong style="color:{MutedColor};font-size:12px;">Address</strong><br />{System.Net.WebUtility.HtmlEncode(extractedAddress)}</p>
                </td>
              </tr>
            </table>
            <p style="margin:22px 0 0;font-size:14px;color:{MutedColor};">You can return to your <strong style="color:{TextColor};">Profile</strong> on LeaseLense to review your verification history or submit another document if needed.</p>
            """;
        return Wrap($"Residency verification — {statusLabel}", inner);
    }
}
