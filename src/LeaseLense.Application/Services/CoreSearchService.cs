using LeaseLense.Application.Abstractions;
using LeaseLense.Application.Search;
using LeaseLense.Domain.Entities;

namespace LeaseLense.Application.Services;

public sealed class CoreSearchService : ICoreSearchService
{
    private readonly ILeaseLensRepository _repository;

    public CoreSearchService(ILeaseLensRepository repository)
    {
        _repository = repository;
    }

    // ── Properties ────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<PropertyMatch>> SearchPropertiesAsync(
        string? queryText,
        string? city,
        int limit,
        string? propertyType = null,
        string? landlordName = null,
        decimal? minRating = null,
        bool hasVerifiedReviews = false,
        string? sortBy = null,
        CancellationToken cancellationToken = default)
    {
        var properties = await _repository.GetPropertiesAsync(cancellationToken);
        var communities = await _repository.GetCommunitiesAsync(cancellationToken);
        var communityById = communities.ToDictionary(x => x.CommunityId);
        var capped = Math.Clamp(limit, 1, 200);

        // Conditionally load data only when the filter needs it.
        Dictionary<Guid, decimal?>? avgRatingByProperty = null;
        if (minRating.HasValue || string.Equals(sortBy, "rating", StringComparison.OrdinalIgnoreCase))
        {
            avgRatingByProperty = await BuildAvgRatingByPropertyAsync(cancellationToken);
        }

        HashSet<Guid>? propertiesWithVerifiedReviews = null;
        if (hasVerifiedReviews)
        {
            propertiesWithVerifiedReviews = await BuildPropertiesWithVerifiedReviewsAsync(cancellationToken);
        }

        IEnumerable<Property> filtered = properties;

        if (!string.IsNullOrWhiteSpace(city))
        {
            var trimmedCity = city.Trim();
            filtered = filtered.Where(p =>
                string.Equals(p.City, trimmedCity, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(propertyType))
        {
            var trimmedType = propertyType.Trim();
            filtered = filtered.Where(p =>
                string.Equals(p.PropertyType, trimmedType, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(landlordName))
        {
            var trimmedLandlord = landlordName.Trim();
            filtered = filtered.Where(p =>
                ContainsIgnoreCase(p.LandlordName, trimmedLandlord)
                || ContainsIgnoreCase(p.ManagementCompanyName, trimmedLandlord));
        }

        if (minRating.HasValue && avgRatingByProperty is not null)
        {
            filtered = filtered.Where(p =>
            {
                var avg = avgRatingByProperty.GetValueOrDefault(p.PropertyId);
                return avg.HasValue && avg.Value >= minRating.Value;
            });
        }

        if (hasVerifiedReviews && propertiesWithVerifiedReviews is not null)
        {
            filtered = filtered.Where(p => propertiesWithVerifiedReviews.Contains(p.PropertyId));
        }

        filtered = filtered.Where(p =>
        {
            var communityName = ResolveCommunityName(p, communityById);
            return MatchesPropertyQuery(queryText, p, communityName);
        });

        // Materialise before sorting (LINQ deferred execution with captured lambda closure is fine
        // but materialising once avoids repeated community lookups in sort comparers).
        var materialised = filtered
            .Select(p => new PropertyMatch
            {
                Property = p,
                CommunityName = ResolveCommunityName(p, communityById)
            })
            .ToList();

        IEnumerable<PropertyMatch> ordered = sortBy?.ToLowerInvariant() switch
        {
            "rating" => materialised
                .OrderByDescending(m => avgRatingByProperty?.GetValueOrDefault(m.Property.PropertyId) ?? 0m)
                .ThenBy(m => m.Property.Title),
            _ => materialised
                .OrderBy(m => m.Property.Title)
                .ThenBy(m => m.Property.StreetAddress)
                .ThenBy(m => m.Property.PropertyId)
        };

        return ordered.Take(capped).ToList();
    }

    // ── Reviews ───────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<ReviewMatch>> SearchReviewsAsync(
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
        CancellationToken cancellationToken = default)
    {
        var properties = await _repository.GetPropertiesAsync(cancellationToken);
        var communities = await _repository.GetCommunitiesAsync(cancellationToken);
        var communityById = communities.ToDictionary(x => x.CommunityId);
        var reviews = await _repository.GetReviewsAsync(cancellationToken);
        var ratings = await _repository.GetReviewRatingsAsync(cancellationToken);
        var propertyLookup = properties.ToDictionary(x => x.PropertyId);

        // Build rating lookups.
        var ratingsByReview = ratings.GroupBy(x => x.ReviewId)
            .ToDictionary(x => x.Key, x => x.ToList());

        var avgRatingByReview = ratingsByReview
            .ToDictionary(kvp => kvp.Key,
                kvp => Math.Round((decimal)kvp.Value.Average(r => r.RatingScore), 1));

        // Conditionally load verifications.
        HashSet<(Guid RenterId, Guid PropertyId)>? verifiedPairs = null;
        if (verifiedOnly)
        {
            verifiedPairs = await BuildVerifiedPairsAsync(cancellationToken);
        }

        // Conditionally load issue tags.
        Dictionary<Guid, List<string>>? issuesByReview = null;
        if (issueTypes is { Count: > 0 })
        {
            var tags = await _repository.GetReviewIssueTagsAsync(cancellationToken);
            issuesByReview = tags
                .GroupBy(x => x.ReviewId)
                .ToDictionary(x => x.Key, x => x.Select(t => t.IssueType).ToList());
        }

        var list = new List<ReviewMatch>();

        foreach (var review in reviews)
        {
            propertyLookup.TryGetValue(review.PropertyId, out var property);
            var communityName = ResolveCommunityName(property, communityById);

            if (!string.IsNullOrWhiteSpace(city))
            {
                if (property is null || !string.Equals(property.City, city.Trim(), StringComparison.OrdinalIgnoreCase))
                    continue;
            }

            if (minRent.HasValue && (!review.MonthlyRent.HasValue || review.MonthlyRent.Value < minRent.Value))
                continue;

            if (maxRent.HasValue && (!review.MonthlyRent.HasValue || review.MonthlyRent.Value > maxRent.Value))
                continue;

            if (minRating.HasValue)
            {
                var avg = avgRatingByReview.GetValueOrDefault(review.ReviewId, 0m);
                if (avg < minRating.Value) continue;
            }

            if (minCommunicationRating.HasValue)
            {
                var score = GetCategoryScore(ratingsByReview, review.ReviewId, "communication");
                if (score < minCommunicationRating.Value) continue;
            }

            if (minMaintenanceRating.HasValue)
            {
                var score = GetCategoryScore(ratingsByReview, review.ReviewId, "maintenance");
                if (score < minMaintenanceRating.Value) continue;
            }

            if (verifiedOnly && verifiedPairs is not null)
            {
                if (!verifiedPairs.Contains((review.RenterId, review.PropertyId))) continue;
            }

            if (issueTypes is { Count: > 0 } && issuesByReview is not null)
            {
                if (!issuesByReview.TryGetValue(review.ReviewId, out var reviewTags)
                    || !issueTypes.Any(t => reviewTags.Contains(t, StringComparer.OrdinalIgnoreCase)))
                {
                    continue;
                }
            }

            if (!MatchesReviewPropertyQuery(queryText, property, communityName))
                continue;

            list.Add(new ReviewMatch
            {
                Review = review,
                Property = property,
                CommunityName = communityName
            });
        }

        return list;
    }

    // ── Scam Reports ──────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<ScamReportMatch>> SearchScamReportsAsync(
        string? queryText,
        string? city,
        decimal? minSeverity,
        int limit,
        string? scamType = null,
        decimal? maxSeverity = null,
        DateOnly? dateReportedAfter = null,
        string? sortBy = null,
        CancellationToken cancellationToken = default)
    {
        var properties = await _repository.GetPropertiesAsync(cancellationToken);
        var communities = await _repository.GetCommunitiesAsync(cancellationToken);
        var communityById = communities.ToDictionary(x => x.CommunityId);
        var scams = await _repository.GetScamReportsAsync(cancellationToken);
        var propertyLookup = properties.ToDictionary(x => x.PropertyId);
        var capped = Math.Clamp(limit, 1, 500);

        IEnumerable<ScamReport> filtered = scams;

        if (!string.IsNullOrWhiteSpace(city))
        {
            var trimmedCity = city.Trim();
            filtered = filtered.Where(s =>
            {
                propertyLookup.TryGetValue(s.PropertyId, out var p);
                return p is not null && string.Equals(p.City, trimmedCity, StringComparison.OrdinalIgnoreCase);
            });
        }

        if (minSeverity.HasValue)
            filtered = filtered.Where(s => s.SeverityScore.HasValue && s.SeverityScore.Value >= minSeverity.Value);

        if (maxSeverity.HasValue)
            filtered = filtered.Where(s => s.SeverityScore.HasValue && s.SeverityScore.Value <= maxSeverity.Value);

        if (!string.IsNullOrWhiteSpace(scamType))
        {
            var trimmedType = scamType.Trim();
            filtered = filtered.Where(s =>
                string.Equals(s.ScamType, trimmedType, StringComparison.OrdinalIgnoreCase));
        }

        if (dateReportedAfter.HasValue)
        {
            var cutoff = dateReportedAfter.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            filtered = filtered.Where(s => s.DateReported >= cutoff);
        }

        filtered = filtered.Where(s =>
        {
            propertyLookup.TryGetValue(s.PropertyId, out var p);
            return MatchesScamReportQuery(queryText, s, p, ResolveCommunityName(p, communityById));
        });

        var materialised = filtered
            .Select(s =>
            {
                propertyLookup.TryGetValue(s.PropertyId, out var p);
                return new ScamReportMatch
                {
                    ScamReport = s,
                    Property = p,
                    CommunityName = ResolveCommunityName(p, communityById)
                };
            })
            .ToList();

        IEnumerable<ScamReportMatch> ordered = sortBy?.ToLowerInvariant() switch
        {
            "severity" => materialised.OrderByDescending(m => m.ScamReport.SeverityScore ?? 0m),
            _ => materialised.OrderByDescending(m => m.ScamReport.DateReported)
        };

        return ordered.Take(capped).ToList();
    }

    // ── Unified dispatch ──────────────────────────────────────────────────────

    public async Task<object> SearchAsync(SearchQueryDto query, CancellationToken cancellationToken = default)
    {
        return query.EntityType switch
        {
            SearchEntityType.Property => await SearchPropertiesAsync(
                query.QueryText, query.City, query.Limit,
                query.PropertyType, query.LandlordName,
                query.MinRating, query.HasVerifiedReviews,
                query.SortBy, cancellationToken),

            SearchEntityType.Review => await SearchReviewsAsync(
                query.QueryText, query.City,
                query.MinRent, query.MaxRent, query.MinRating,
                query.VerifiedOnly, query.IssueTypes,
                query.MinCommunicationRating, query.MinMaintenanceRating,
                query.SortBy, cancellationToken),

            SearchEntityType.ScamReport => await SearchScamReportsAsync(
                query.QueryText, query.City, query.MinSeverity, query.Limit,
                query.ScamType, query.MaxSeverity, query.DateReportedAfter,
                query.SortBy, cancellationToken),

            _ => throw new ArgumentOutOfRangeException(nameof(query.EntityType), query.EntityType, null)
        };
    }

    // ── Helper builders ───────────────────────────────────────────────────────

    private async Task<Dictionary<Guid, decimal?>> BuildAvgRatingByPropertyAsync(CancellationToken ct)
    {
        var reviews = await _repository.GetReviewsAsync(ct);
        var ratings = await _repository.GetReviewRatingsAsync(ct);
        var avgByReview = ratings
            .GroupBy(x => x.ReviewId)
            .ToDictionary(x => x.Key, x => (decimal)x.Average(r => r.RatingScore));

        var result = new Dictionary<Guid, decimal?>();
        foreach (var grp in reviews.GroupBy(x => x.PropertyId))
        {
            var scores = grp
                .Select(r => avgByReview.GetValueOrDefault(r.ReviewId))
                .Where(v => v != default)
                .ToList();
            result[grp.Key] = scores.Count == 0 ? null : (decimal?)Math.Round(scores.Average(), 1);
        }
        return result;
    }

    private async Task<HashSet<Guid>> BuildPropertiesWithVerifiedReviewsAsync(CancellationToken ct)
    {
        var verifications = await _repository.GetRenterPropertyVerificationsAsync(ct);
        var reviews = await _repository.GetReviewsAsync(ct);

        var verifiedPairs = verifications
            .Where(v => string.Equals(v.Status, "verified_stay", StringComparison.OrdinalIgnoreCase))
            .Select(v => (v.RenterId, v.PropertyId))
            .ToHashSet();

        return reviews
            .Where(r => verifiedPairs.Contains((r.RenterId, r.PropertyId)))
            .Select(r => r.PropertyId)
            .ToHashSet();
    }

    private async Task<HashSet<(Guid, Guid)>> BuildVerifiedPairsAsync(CancellationToken ct)
    {
        var verifications = await _repository.GetRenterPropertyVerificationsAsync(ct);
        return verifications
            .Where(v => string.Equals(v.Status, "verified_stay", StringComparison.OrdinalIgnoreCase))
            .Select(v => (v.RenterId, v.PropertyId))
            .ToHashSet();
    }

    // ── Text matching ─────────────────────────────────────────────────────────

    private static bool MatchesPropertyQuery(string? queryText, Property property, string communityName)
    {
        if (string.IsNullOrWhiteSpace(queryText)) return true;
        var q = queryText.Trim();
        return ContainsIgnoreCase(property.Title, q)
            || ContainsIgnoreCase(property.StreetAddress, q)
            || ContainsIgnoreCase(property.City, q)
            || ContainsIgnoreCase(communityName, q)
            || ContainsIgnoreCase(property.LandlordName, q)
            || ContainsIgnoreCase(property.ManagementCompanyName, q);
    }

    private static bool MatchesReviewPropertyQuery(string? queryText, Property? property, string communityName)
    {
        if (string.IsNullOrWhiteSpace(queryText)) return true;
        var q = queryText.Trim();
        return ContainsIgnoreCase(property?.Title, q)
            || ContainsIgnoreCase(property?.StreetAddress, q)
            || ContainsIgnoreCase(property?.City, q)
            || ContainsIgnoreCase(communityName, q);
    }

    private static bool MatchesScamReportQuery(
        string? queryText, ScamReport scam, Property? property, string communityName)
    {
        if (string.IsNullOrWhiteSpace(queryText)) return true;
        var q = queryText.Trim();
        return ContainsIgnoreCase(property?.Title, q)
            || ContainsIgnoreCase(property?.StreetAddress, q)
            || ContainsIgnoreCase(property?.City, q)
            || ContainsIgnoreCase(communityName, q)
            || ContainsIgnoreCase(scam.ScamType, q)
            || ContainsIgnoreCase(scam.Description, q);
    }

    // ── Utilities ─────────────────────────────────────────────────────────────

    private static decimal GetCategoryScore(
        Dictionary<Guid, List<ReviewRating>> ratingsByReview,
        Guid reviewId,
        string category)
    {
        if (!ratingsByReview.TryGetValue(reviewId, out var reviewRatings)) return 0m;
        var scores = reviewRatings
            .Where(r => r.RatingCategory.Contains(category, StringComparison.OrdinalIgnoreCase))
            .Select(r => r.RatingScore)
            .ToList();
        return scores.Count == 0 ? 0m : Math.Round((decimal)scores.Average(), 1);
    }

    private static bool ContainsIgnoreCase(string? field, string query)
        => !string.IsNullOrEmpty(field) && field.Contains(query, StringComparison.OrdinalIgnoreCase);

    private static string ResolveCommunityName(Property? property, Dictionary<Guid, Community> communityById)
    {
        if (property?.CommunityId is not { } id) return string.Empty;
        return communityById.TryGetValue(id, out var c) ? (c.Name ?? string.Empty) : string.Empty;
    }
}
