namespace LeaseLense.Web.Models.Home;

public sealed class HomeIndexViewModel
{
    public IReadOnlyList<HomePropertyListItemViewModel> Properties { get; init; } = [];
    public IReadOnlyList<HomeReviewListItemViewModel> Reviews { get; init; } = [];
    public IReadOnlyList<HomeScamReportListItemViewModel> ScamReports { get; init; } = [];
}
