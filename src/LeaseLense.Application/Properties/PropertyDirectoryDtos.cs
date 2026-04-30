namespace LeaseLense.Application.Properties;

public sealed class PropertyDirectoryQueryDto
{
    public string? QueryText { get; init; }
    public int Limit { get; init; } = 100;
}

public sealed class PropertyDirectoryResultDto
{
    public string QueryText { get; init; } = string.Empty;
    public IReadOnlyList<PropertyDirectoryItemDto> Items { get; init; } = [];
}

public sealed class PropertyDirectoryItemDto
{
    public Guid PropertyId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string CommunityName { get; init; } = string.Empty;
    public string StreetAddress { get; init; } = string.Empty;
    public string City { get; init; } = string.Empty;
    public string Country { get; init; } = string.Empty;
    public decimal? AverageRating { get; init; }
    public decimal? AverageScamSeverity { get; init; }
}

public sealed class PropertyProfileDto
{
    public Guid PropertyId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string CommunityName { get; init; } = string.Empty;
    public string StreetAddress { get; init; } = string.Empty;
    public string City { get; init; } = string.Empty;
    public string Country { get; init; } = string.Empty;
    public string? LandlordName { get; init; }
    public string? ManagementCompanyName { get; init; }
    public decimal? AverageRating { get; init; }
    public int ReviewCount { get; init; }
    public decimal? AverageScamSeverity { get; init; }
    public IReadOnlyList<PropertyProfileReviewDto> Reviews { get; init; } = [];
    public IReadOnlyList<PropertyProfileScamReportDto> ScamReports { get; init; } = [];
}

public sealed class PropertyProfileReviewDto
{
    public Guid ReviewId { get; init; }
    public string ReviewerAlias { get; init; } = string.Empty;
    public decimal? MonthlyRent { get; init; }
    public decimal AverageRating { get; init; }
    public string? VerificationBadge { get; init; }
    public string ReviewText { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
}

public sealed class PropertyProfileScamReportDto
{
    public Guid ScamReportId { get; init; }
    public string ScamType { get; init; } = string.Empty;
    public decimal? SeverityScore { get; init; }
    public string Description { get; init; } = string.Empty;
    public string? VerificationBadge { get; init; }
    public DateTime DateReported { get; init; }
}
