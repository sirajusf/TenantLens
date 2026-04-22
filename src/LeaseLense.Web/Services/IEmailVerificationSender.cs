namespace LeaseLense.Web.Services;

public interface IEmailVerificationSender
{
    Task SendVerificationEmailAsync(string toEmail, string verificationUrl, CancellationToken cancellationToken = default);
    Task SendResidencyDecisionEmailAsync(string toEmail, string status, string reason, CancellationToken cancellationToken = default);
}
