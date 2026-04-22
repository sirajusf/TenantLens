namespace LeaseLense.Web.Models.Properties;

public sealed class PropertyProfileScamReportViewModel
{
    public string ScamType { get; init; } = string.Empty;
    public decimal? SeverityScore { get; init; }
    public string Description { get; init; } = string.Empty;
    public string? VerificationBadge { get; init; }
    public DateTime DateReported { get; init; }
}
