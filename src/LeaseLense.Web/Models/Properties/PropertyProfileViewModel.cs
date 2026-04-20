namespace LeaseLense.Web.Models.Properties;

public sealed class PropertyProfileViewModel
{
    public Guid PropertyId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string StreetAddress { get; init; } = string.Empty;
    public string City { get; init; } = string.Empty;
    public string Country { get; init; } = string.Empty;
    public string? LandlordName { get; init; }
    public string? ManagementCompanyName { get; init; }
    public decimal? AverageRating { get; init; }
    public int ReviewCount { get; init; }
    public decimal? AverageScamSeverity { get; init; }
    public IReadOnlyList<PropertyProfileReviewViewModel> Reviews { get; init; } = [];
    public IReadOnlyList<PropertyProfileScamReportViewModel> ScamReports { get; init; } = [];
}
