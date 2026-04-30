using LeaseLense.Domain.Entities;

namespace LeaseLense.Application.Search;

public sealed class PropertyMatch
{
    public required Property Property { get; init; }
    public string CommunityName { get; init; } = string.Empty;
}

public sealed class ReviewMatch
{
    public required Review Review { get; init; }
    public Property? Property { get; init; }
    public string CommunityName { get; init; } = string.Empty;
}

public sealed class ScamReportMatch
{
    public required ScamReport ScamReport { get; init; }
    public Property? Property { get; init; }
    public string CommunityName { get; init; } = string.Empty;
}
