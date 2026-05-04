namespace LeaseLense.Web.Models.ScamReports;

public sealed class ScamReportIndexViewModel
{
    public string? QueryText { get; init; }
    public string? City { get; init; }
    public decimal? MinSeverity { get; init; }
    public string? ScamType { get; init; }
    public decimal? MaxSeverity { get; init; }
    public DateOnly? DateReportedAfter { get; init; }
    public string? SortBy { get; init; }
    public IReadOnlyList<string> InterpretedFilters { get; init; } = [];
    public bool LlmUnavailable { get; init; }
    public bool NlFallback { get; init; }
    public IReadOnlyList<ScamReportListItemViewModel> Reports { get; init; } = [];
}
