using LeaseLense.Application.Properties;

namespace LeaseLense.Application.Home;

public sealed class HomePageDataDto
{
    public IReadOnlyList<PropertyListItemDto> Properties { get; init; } = [];
    public IReadOnlyList<HomeReviewListItemDto> Reviews { get; init; } = [];
    public IReadOnlyList<HomeScamReportListItemDto> ScamReports { get; init; } = [];
}
