namespace LeaseLense.Web.Services;

public interface IEmailVerificationSender
{
    Task SendVerificationEmailAsync(string toEmail, string verificationUrl, CancellationToken cancellationToken = default);
    Task SendResidencyVerificationInProcessEmailAsync(string toEmail, CancellationToken cancellationToken = default);
    Task SendResidencyDecisionEmailAsync(
        string toEmail,
        ResidencyDecisionEmailContext context,
        CancellationToken cancellationToken = default);
}

public sealed class ResidencyDecisionEmailContext
{
    public string Status { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string? StreetAddress { get; init; }
    public string? City { get; init; }
    public string? StateOrRegion { get; init; }
    public string? PostalCode { get; init; }
    public string? Country { get; init; }
    public string DocumentType { get; init; } = string.Empty;
    public string CommunityName { get; init; } = string.Empty;
    public string PropertyTitle { get; init; } = string.Empty;
    public string FileName { get; init; } = string.Empty;
    public string ExtractedName { get; init; } = string.Empty;
    public string ExtractedAddress { get; init; } = string.Empty;
    public DateOnly? ExtractedFromDate { get; init; }
    public DateOnly? ExtractedToDate { get; init; }
    public decimal ParserConfidence { get; init; }
}
