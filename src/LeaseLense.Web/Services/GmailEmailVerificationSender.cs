using System.Net;
using System.Net.Mail;
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
        message.Body = $"""
                        Hi,

                        Welcome to LeaseLense. Please verify your email by clicking the link below:
                        {verificationUrl}

                        If you did not create this account, you can ignore this message.
                        """;
        message.IsBodyHtml = false;

        using var client = new SmtpClient(_options.Host, _options.Port)
        {
            EnableSsl = true,
            Credentials = new NetworkCredential(_options.SenderEmail, _options.AppPassword)
        };

        using var registration = cancellationToken.Register(() => client.SendAsyncCancel());
        await client.SendMailAsync(message, cancellationToken);
    }

    public async Task SendResidencyDecisionEmailAsync(string toEmail, string status, string reason, CancellationToken cancellationToken = default)
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
        var displayStatus = ToDisplayStatus(status);
        message.Subject = $"LeaseLense residency verification update: {displayStatus}";
        message.Body = $"""
                        Hi,

                        Your residency verification status is now: {displayStatus}.
                        Reason: {reason}

                        You can return to your Profile page to submit another document if needed.
                        """;
        message.IsBodyHtml = false;

        using var client = new SmtpClient(_options.Host, _options.Port)
        {
            EnableSsl = true,
            Credentials = new NetworkCredential(_options.SenderEmail, _options.AppPassword)
        };

        using var registration = cancellationToken.Register(() => client.SendAsyncCancel());
        await client.SendMailAsync(message, cancellationToken);
    }

    private static string ToDisplayStatus(string status)
    {
        return status.ToLowerInvariant() switch
        {
            "verified_stay" => "Verified stay",
            "pending_manual_review" => "Pending review",
            "rejected" => "Rejected",
            _ => status.Replace("_", " ")
        };
    }
}
