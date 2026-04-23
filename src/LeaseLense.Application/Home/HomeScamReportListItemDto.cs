namespace LeaseLense.Application.Home;

public sealed class HomeScamReportListItemDto
{
    public Guid ScamReportId { get; init; }
    public string PropertyTitle { get; init; } = string.Empty;
    public string CommunityName { get; init; } = string.Empty;
    public string City { get; init; } = string.Empty;
    public string ScamType { get; init; } = string.Empty;
    public decimal? SeverityScore { get; init; }
    public string Description { get; init; } = string.Empty;
    public string? VerificationBadge { get; init; }
}
