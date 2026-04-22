namespace LeaseLense.Web.Models.Profile;

public sealed class ProfilePageViewModel
{
    public ProfileAccountFormViewModel Account { get; init; } = new();
    public ProfileResidencyVerificationFormViewModel VerificationForm { get; init; } = new();
    public IReadOnlyList<ProfileVerificationStatusViewModel> VerificationStatuses { get; init; } = [];
}

public sealed class ProfileAccountFormViewModel
{
    public string DisplayName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public bool EmailVerified { get; init; }
    public bool NameLocked { get; init; }
    public string? StreetAddress { get; init; }
    public string? City { get; init; }
    public string? StateOrRegion { get; init; }
    public string? PostalCode { get; init; }
    public string? Country { get; init; }
    public bool HasVerifiedStay { get; init; }
}

public sealed class ProfileResidencyVerificationFormViewModel
{
    public string CommunityName { get; init; } = string.Empty;
    public string PropertyTitle { get; init; } = string.Empty;
    public string PropertyAddressSummary { get; init; } = string.Empty;
    public string DocumentType { get; init; } = "lease";
}

public sealed class ProfileVerificationStatusViewModel
{
    public string PropertyTitle { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public decimal ConfidenceScore { get; init; }
    public string? ReviewReason { get; init; }
    public DateTime UpdatedAt { get; init; }
}

