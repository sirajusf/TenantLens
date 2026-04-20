namespace LeaseLense.Web.Models.ScamReports;

public sealed class ScamReportListItemViewModel
{
    public Guid PropertyId { get; init; }
    public string PropertyTitle { get; init; } = string.Empty;
    public string City { get; init; } = string.Empty;
    public string ScamType { get; init; } = string.Empty;
    public decimal? SeverityScore { get; init; }
    public DateTime DateReported { get; init; }
    public string Description { get; init; } = string.Empty;
}
