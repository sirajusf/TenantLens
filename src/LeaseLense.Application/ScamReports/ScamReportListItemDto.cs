namespace LeaseLense.Application.ScamReports;

public sealed class ScamReportListItemDto
{
    public Guid ScamReportId { get; init; }
    public Guid PropertyId { get; init; }
    public string PropertyTitle { get; init; } = string.Empty;
    public string City { get; init; } = string.Empty;
    public string ScamType { get; init; } = string.Empty;
    public decimal? SeverityScore { get; init; }
    public DateTime DateReported { get; init; }
    public string Description { get; init; } = string.Empty;
}
