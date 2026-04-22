namespace LeaseLense.Application.ScamReports;

public sealed class ScamReportFormMetadataDto
{
    public bool CanSubmit { get; init; }
    public string? RestrictionMessage { get; init; }
    public IReadOnlyList<ScamReportOptionDto> Properties { get; init; } = [];
}
