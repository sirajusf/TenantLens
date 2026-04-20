namespace LeaseLense.Application.ScamReports;

public sealed class CreateScamReportDto
{
    public Guid? PropertyId { get; init; }
    public string? NewPropertyTitle { get; init; }
    public string? NewPropertyStreetAddress { get; init; }
    public string? NewPropertyCity { get; init; }
    public string? NewPropertyCountry { get; init; }
    public string ScamType { get; init; } = string.Empty;
    public string? Description { get; init; }
    public decimal? SeverityScore { get; init; }
    public string ReporterEmail { get; init; } = string.Empty;
}
