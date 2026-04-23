namespace LeaseLense.Web.Models.Home;

public sealed class HomeScamReportListItemViewModel
{
    public string PropertyTitle { get; init; } = string.Empty;
    public string CommunityName { get; init; } = string.Empty;
    public string City { get; init; } = string.Empty;
    public string ScamType { get; init; } = string.Empty;
    public decimal? SeverityScore { get; init; }
    public string Description { get; init; } = string.Empty;
    public string? VerificationBadge { get; init; }
}
