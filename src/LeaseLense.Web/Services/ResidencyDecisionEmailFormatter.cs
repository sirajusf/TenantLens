using LeaseLense.Application.Profile;

namespace LeaseLense.Web.Services;

public static class ResidencyDecisionEmailFormatter
{
    public static string BuildInProcessPlainText()
    {
        return """
            Thanks for submitting your residency document.

            We are processing your verification. You will receive another email when we have an update.
            """;
    }

    public static string BuildResidencyDecisionPlainText(ResidencyDecisionEmailContext context)
    {
        var statusLabel = ToDisplayStatus(context.Status);
        var summary = ResidencyVerificationDisplayHelper.BuildCustomerSummary(
            context.Status,
            context.ExtractedName,
            context.ExtractedAddress);
        var providedAddress = FormatMailingAddress(context);
        var extractedName = string.IsNullOrWhiteSpace(context.ExtractedName) ? "Not read from document" : context.ExtractedName.Trim();
        var extractedAddress = string.IsNullOrWhiteSpace(context.ExtractedAddress) ? "Not read from document" : context.ExtractedAddress.Trim();
        var displayName = string.IsNullOrWhiteSpace(context.DisplayName) ? "Not on file" : context.DisplayName.Trim();

        return $"""
            LeaseLense — residency verification update

            Status: {statusLabel}

            {summary}

            Name on your account: {displayName}
            Address on your account: {(string.IsNullOrWhiteSpace(providedAddress) ? "Not on file" : providedAddress)}

            From your document:
            - Name: {extractedName}
            - Address: {extractedAddress}

            Visit your Profile on LeaseLense to review verification history or submit another document if needed.
            """;
    }

    public static string BuildEmailVerificationPlainText(string verificationUrl)
    {
        return $"""
            Welcome to LeaseLense. Please verify your email by opening this link:

            {verificationUrl}

            If you did not create this account, you can ignore this message.
            """;
    }

    public static string FormatMailingAddress(ResidencyDecisionEmailContext context)
    {
        var parts = new[]
        {
            context.StreetAddress,
            context.City,
            context.StateOrRegion,
            context.PostalCode,
            context.Country
        }
        .Where(static x => !string.IsNullOrWhiteSpace(x))
        .Select(static x => x!.Trim());

        return string.Join(", ", parts);
    }

    public static string ToDisplayStatus(string status)
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
