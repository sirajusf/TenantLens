namespace LeaseLense.Application.Profile;

public sealed class UserProfileDto
{
    public string Email { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public bool EmailVerified { get; init; }
    public string? StreetAddress { get; init; }
    public string? City { get; init; }
    public string? StateOrRegion { get; init; }
    public string? PostalCode { get; init; }
    public string? Country { get; init; }
    public bool NameLocked { get; init; }
    public bool HasVerifiedStay { get; init; }
    public IReadOnlyList<UserVerificationStatusDto> Verifications { get; init; } = [];
}

public sealed class UserVerificationStatusDto
{
    public Guid PropertyId { get; init; }
    public string PropertyTitle { get; init; } = string.Empty;
    public string Status { get; init; } = "pending_manual_review";
    public decimal ConfidenceScore { get; init; }
    public string? ReviewReason { get; init; }
    public DateTime UpdatedAt { get; init; }
}

public sealed class ResidencyVerificationDecisionDto
{
    public string Status { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
}

public sealed class ResidencyDocumentExtractionDto
{
    public string ExtractedName { get; init; } = string.Empty;
    public string ExtractedAddress { get; init; } = string.Empty;
    public DateOnly? ExtractedFromDate { get; init; }
    public DateOnly? ExtractedToDate { get; init; }
    public decimal ParserConfidence { get; init; }
    public string RawText { get; init; } = string.Empty;
}

public sealed class UpdateUserProfileDto
{
    public string Email { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string? StreetAddress { get; init; }
    public string? City { get; init; }
    public string? StateOrRegion { get; init; }
    public string? PostalCode { get; init; }
    public string? Country { get; init; }
}

public sealed class SubmitResidencyVerificationDto
{
    public string Email { get; init; } = string.Empty;
    public string CommunityName { get; init; } = string.Empty;
    public string PropertyTitle { get; init; } = string.Empty;
    public string DocumentType { get; init; } = string.Empty;
    public string FileName { get; init; } = string.Empty;
    public string ContentType { get; init; } = string.Empty;
    public long FileSizeBytes { get; init; }
    public string FileHashSha256 { get; init; } = string.Empty;
    public string ExtractedName { get; init; } = string.Empty;
    public string ExtractedAddress { get; init; } = string.Empty;
    public DateOnly? ExtractedFromDate { get; init; }
    public DateOnly? ExtractedToDate { get; init; }
    public decimal ParserConfidence { get; init; }
}
