namespace LeaseLense.Web.Models.ScamReports;

public sealed class ScamReportCreatePageViewModel
{
    public CreateScamReportViewModel Form { get; init; } = new();
    public IReadOnlyList<ScamReportOptionViewModel> Properties { get; init; } = [];
}
