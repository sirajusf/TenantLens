using LeaseLense.Application.Search;

namespace LeaseLense.Application.Abstractions;

/// <summary>
/// Shared text + filter matching for properties, reviews, and scam reports (no ranking/scoring).
/// </summary>
public interface ICoreSearchService
{
    Task<IReadOnlyList<PropertyMatch>> SearchPropertiesAsync(
        string? queryText,
        string? city,
        int limit,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ReviewMatch>> SearchReviewsAsync(
        string? queryText,
        string? city,
        decimal? minRent,
        decimal? maxRent,
        decimal? minRating,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ScamReportMatch>> SearchScamReportsAsync(
        string? queryText,
        string? city,
        decimal? minSeverity,
        int limit,
        CancellationToken cancellationToken = default);

    Task<object> SearchAsync(SearchQueryDto query, CancellationToken cancellationToken = default);
}
