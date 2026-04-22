namespace LeaseLense.Application.Profile;

public static class ResidencyVerificationDisplayHelper
{
    public static string BuildCustomerSummary(string status, string? extractedName, string? extractedAddress)
    {
        var hasExtraction = !string.IsNullOrWhiteSpace(extractedName) || !string.IsNullOrWhiteSpace(extractedAddress);
        return status.ToLowerInvariant() switch
        {
            "verified_stay" =>
                "Your document matched the name and address we have on file for this stay. Your verified stay is active for this property.",
            "pending_manual_review" when !hasExtraction =>
                "We could not read enough clear information from your document automatically. Our team will review it and email you with an update.",
            "pending_manual_review" =>
                "We need a closer look at your submission. Our team will review it and email you with an update.",
            "rejected" =>
                "We could not confirm your residency from this document. You can update the address on your profile or submit a clearer document.",
            _ => "Your verification request is being processed."
        };
    }

    public static string FormatPropertyAddress(
        string? street,
        string? city,
        string? stateOrRegion,
        string? postalCode,
        string? country)
    {
        var parts = new[]
        {
            street,
            city,
            stateOrRegion,
            postalCode,
            country
        }
        .Where(static x => !string.IsNullOrWhiteSpace(x))
        .Select(static x => x!.Trim());

        return string.Join(", ", parts);
    }
}
