namespace LeaseLense.Web.Models.ScamReports;

public sealed class ScamReportIndexViewModel
{
    public string? QueryText { get; init; }
    public string? City { get; init; }
    public decimal? MinSeverity { get; init; }
    public IReadOnlyList<ScamReportListItemViewModel> Reports { get; init; } = [];
}
