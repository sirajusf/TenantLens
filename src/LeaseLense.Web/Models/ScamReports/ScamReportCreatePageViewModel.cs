namespace LeaseLense.Web.Models.ScamReports;

public sealed class ScamReportCreatePageViewModel
{
    public CreateScamReportViewModel Form { get; init; } = new();
    public bool CanSubmit { get; init; } = true;
    public string? RestrictionMessage { get; init; }
    public IReadOnlyList<ScamReportOptionViewModel> Properties { get; init; } = [];
}
