namespace LeaseLense.Application.Reviews;

public sealed class ReviewQueryDto
{
    public string? PropertyQuery { get; init; }
    public string? City { get; init; }
    public decimal? MinRent { get; init; }
    public decimal? MaxRent { get; init; }
    public decimal? MinRating { get; init; }
    public decimal? MinCommunicationRating { get; init; }
    public decimal? MinMaintenanceRating { get; init; }

    /// <summary>When true, only reviews from renters with a verified stay are returned.</summary>
    public bool VerifiedOnly { get; init; }

    /// <summary>Return reviews whose issue tags include at least one of these values.</summary>
    public IReadOnlyList<string>? IssueTypes { get; init; }

    /// <summary>"newest" | "rating" | "rent"</summary>
    public string? SortBy { get; init; }
}
