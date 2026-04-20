namespace LeaseLense.Web.Models.ScamReports;

public sealed class ScamReportIndexViewModel
{
    public IReadOnlyList<ScamReportListItemViewModel> Reports { get; init; } = [];
}
