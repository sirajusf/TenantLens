using LeaseLense.Application.Search;

namespace LeaseLense.Application.Abstractions;

/// <summary>
/// Single backend search hub.  Every feature routes through here so that a future NLP layer
/// only needs to parse natural language once — the result populates <see cref="SearchQueryDto"/>
/// and the rest of the pipeline is unchanged.
/// </summary>
public interface ICoreSearchService
{
    Task<IReadOnlyList<PropertyMatch>> SearchPropertiesAsync(
        string? queryText,
        string? city,
        int limit,
        string? propertyType = null,
        string? landlordName = null,
        decimal? minRating = null,
        bool hasVerifiedReviews = false,
        string? sortBy = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ReviewMatch>> SearchReviewsAsync(
        string? queryText,
        string? city,
        decimal? minRent,
        decimal? maxRent,
        decimal? minRating,
        bool verifiedOnly = false,
        IReadOnlyList<string>? issueTypes = null,
        decimal? minCommunicationRating = null,
        decimal? minMaintenanceRating = null,
        string? sortBy = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ScamReportMatch>> SearchScamReportsAsync(
        string? queryText,
        string? city,
        decimal? minSeverity,
        int limit,
        string? scamType = null,
        decimal? maxSeverity = null,
        DateOnly? dateReportedAfter = null,
        string? sortBy = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Unified dispatch — routes to the correct typed method based on
    /// <see cref="SearchQueryDto.EntityType"/>.  Entry point for the future NLP layer.
    /// </summary>
    Task<object> SearchAsync(SearchQueryDto query, CancellationToken cancellationToken = default);
}
