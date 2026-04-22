using System.Net;
using System.Net.Mail;
using System.Text;
using Microsoft.Extensions.Options;

namespace LeaseLense.Web.Services;

public sealed class GmailEmailVerificationSender : IEmailVerificationSender
{
    private readonly GmailSmtpOptions _options;
    private readonly ILogger<GmailEmailVerificationSender> _logger;

    public GmailEmailVerificationSender(
        IOptions<GmailSmtpOptions> options,
        ILogger<GmailEmailVerificationSender> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendVerificationEmailAsync(string toEmail, string verificationUrl, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            _logger.LogWarning("Gmail SMTP is disabled. Skipping email delivery.");
            return;
        }

        if (string.IsNullOrWhiteSpace(_options.SenderEmail) || string.IsNullOrWhiteSpace(_options.AppPassword))
        {
            throw new InvalidOperationException("Gmail SMTP sender configuration is incomplete.");
        }

        using var message = new MailMessage();
        message.From = new MailAddress(_options.SenderEmail, _options.SenderDisplayName);
        message.To.Add(new MailAddress(toEmail));
        message.Subject = "Verify your LeaseLense email";
        message.SubjectEncoding = Encoding.UTF8;
        message.BodyEncoding = Encoding.UTF8;

        var plain = ResidencyDecisionEmailFormatter.BuildEmailVerificationPlainText(verificationUrl);
        var html = LeaseLenseEmailHtmlTemplates.BuildEmailVerificationHtml(verificationUrl);
        message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(plain, null, "text/plain"));
        message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(html, null, "text/html"));

        using var client = new SmtpClient(_options.Host, _options.Port)
        {
            EnableSsl = true,
            Credentials = new NetworkCredential(_options.SenderEmail, _options.AppPassword)
        };

        using var registration = cancellationToken.Register(() => client.SendAsyncCancel());
        await client.SendMailAsync(message, cancellationToken);
    }

    public async Task SendResidencyDecisionEmailAsync(
        string toEmail,
        ResidencyDecisionEmailContext context,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            _logger.LogWarning("Gmail SMTP is disabled. Skipping email delivery.");
            return;
        }

        if (string.IsNullOrWhiteSpace(_options.SenderEmail) || string.IsNullOrWhiteSpace(_options.AppPassword))
        {
            throw new InvalidOperationException("Gmail SMTP sender configuration is incomplete.");
        }

        using var message = new MailMessage();
        message.From = new MailAddress(_options.SenderEmail, _options.SenderDisplayName);
        message.To.Add(new MailAddress(toEmail));
        var displayStatus = ResidencyDecisionEmailFormatter.ToDisplayStatus(context.Status);
        message.Subject = $"LeaseLense residency verification — {displayStatus}";
        message.SubjectEncoding = Encoding.UTF8;
        message.BodyEncoding = Encoding.UTF8;

        var plain = ResidencyDecisionEmailFormatter.BuildResidencyDecisionPlainText(context);
        var html = LeaseLenseEmailHtmlTemplates.BuildResidencyDecisionHtml(context);
        message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(plain, null, "text/plain"));
        message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(html, null, "text/html"));

        using var client = new SmtpClient(_options.Host, _options.Port)
        {
            EnableSsl = true,
            Credentials = new NetworkCredential(_options.SenderEmail, _options.AppPassword)
        };

        using var registration = cancellationToken.Register(() => client.SendAsyncCancel());
        await client.SendMailAsync(message, cancellationToken);
    }

    public async Task SendResidencyVerificationInProcessEmailAsync(string toEmail, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            _logger.LogWarning("Gmail SMTP is disabled. Skipping email delivery.");
            return;
        }

        if (string.IsNullOrWhiteSpace(_options.SenderEmail) || string.IsNullOrWhiteSpace(_options.AppPassword))
        {
            throw new InvalidOperationException("Gmail SMTP sender configuration is incomplete.");
        }

        using var message = new MailMessage();
        message.From = new MailAddress(_options.SenderEmail, _options.SenderDisplayName);
        message.To.Add(new MailAddress(toEmail));
        message.Subject = "LeaseLense — verification in progress";
        message.SubjectEncoding = Encoding.UTF8;
        message.BodyEncoding = Encoding.UTF8;

        var plain = ResidencyDecisionEmailFormatter.BuildInProcessPlainText();
        var html = LeaseLenseEmailHtmlTemplates.BuildResidencyInProcessHtml();
        message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(plain, null, "text/plain"));
        message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(html, null, "text/html"));

        using var client = new SmtpClient(_options.Host, _options.Port)
        {
            EnableSsl = true,
            Credentials = new NetworkCredential(_options.SenderEmail, _options.AppPassword)
        };

        using var registration = cancellationToken.Register(() => client.SendAsyncCancel());
        await client.SendMailAsync(message, cancellationToken);
    }
}
