using LeaseLense.Application.Abstractions;
using LeaseLense.Application.Abstractions.Persistence;
using LeaseLense.Application.Search;
using LeaseLense.Domain.Entities;

namespace LeaseLense.Application.Services;

public sealed class CoreSearchService : ICoreSearchService
{
    private readonly ILeaseLensDbContext _dbContext;

    public CoreSearchService(ILeaseLensDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<PropertyMatch>> SearchPropertiesAsync(
        string? queryText,
        string? city,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var properties = await _dbContext.GetPropertiesAsync(cancellationToken);
        var communities = await _dbContext.GetCommunitiesAsync(cancellationToken);
        var communityById = communities.ToDictionary(x => x.CommunityId);

        var capped = Math.Clamp(limit, 1, 200);

        IEnumerable<Property> filtered = properties;

        if (!string.IsNullOrWhiteSpace(city))
        {
            filtered = filtered.Where(p =>
                string.Equals(p.City, city.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        filtered = filtered.Where(p =>
        {
            var communityName = ResolveCommunityName(p, communityById);
            return MatchesPropertyQuery(queryText, p, communityName);
        });

        var result = filtered
            .OrderBy(p => p.Title)
            .ThenBy(p => p.StreetAddress)
            .ThenBy(p => p.PropertyId)
            .Take(capped)
            .Select(p => new PropertyMatch
            {
                Property = p,
                CommunityName = ResolveCommunityName(p, communityById)
            })
            .ToList();

        return result;
    }

    public async Task<IReadOnlyList<ReviewMatch>> SearchReviewsAsync(
        string? queryText,
        string? city,
        decimal? minRent,
        decimal? maxRent,
        decimal? minRating,
        CancellationToken cancellationToken = default)
    {
        var properties = await _dbContext.GetPropertiesAsync(cancellationToken);
        var communities = await _dbContext.GetCommunitiesAsync(cancellationToken);
        var communityById = communities.ToDictionary(x => x.CommunityId);
        var reviews = await _dbContext.GetReviewsAsync(cancellationToken);
        var ratings = await _dbContext.GetReviewRatingsAsync(cancellationToken);

        var propertyLookup = properties.ToDictionary(x => x.PropertyId);
        var averageRatings = ratings
            .GroupBy(x => x.ReviewId)
            .ToDictionary(x => x.Key, x => Math.Round((decimal)x.Average(y => y.RatingScore), 1));

        var list = new List<ReviewMatch>();

        foreach (var review in reviews)
        {
            propertyLookup.TryGetValue(review.PropertyId, out var property);
            var communityName = ResolveCommunityName(property, communityById);

            if (!string.IsNullOrWhiteSpace(city))
            {
                var cityOk = property is not null
                    && string.Equals(property.City, city.Trim(), StringComparison.OrdinalIgnoreCase);
                if (!cityOk)
                {
                    continue;
                }
            }

            if (minRent.HasValue)
            {
                if (!review.MonthlyRent.HasValue || review.MonthlyRent.Value < minRent.Value)
                {
                    continue;
                }
            }

            if (maxRent.HasValue)
            {
                if (!review.MonthlyRent.HasValue || review.MonthlyRent.Value > maxRent.Value)
                {
                    continue;
                }
            }

            if (minRating.HasValue)
            {
                var avg = averageRatings.GetValueOrDefault(review.ReviewId, 0m);
                if (avg < minRating.Value)
                {
                    continue;
                }
            }

            if (!MatchesReviewPropertyQuery(queryText, property, communityName))
            {
                continue;
            }

            list.Add(new ReviewMatch
            {
                Review = review,
                Property = property,
                CommunityName = communityName
            });
        }

        return list;
    }

    public async Task<IReadOnlyList<ScamReportMatch>> SearchScamReportsAsync(
        string? queryText,
        string? city,
        decimal? minSeverity,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var properties = await _dbContext.GetPropertiesAsync(cancellationToken);
        var communities = await _dbContext.GetCommunitiesAsync(cancellationToken);
        var communityById = communities.ToDictionary(x => x.CommunityId);
        var scams = await _dbContext.GetScamReportsAsync(cancellationToken);

        var propertyLookup = properties.ToDictionary(x => x.PropertyId);

        var capped = Math.Clamp(limit, 1, 500);

        IEnumerable<ScamReport> filtered = scams;

        if (!string.IsNullOrWhiteSpace(city))
        {
            filtered = filtered.Where(s =>
            {
                propertyLookup.TryGetValue(s.PropertyId, out var p);
                return p is not null
                    && string.Equals(p.City, city.Trim(), StringComparison.OrdinalIgnoreCase);
            });
        }

        if (minSeverity.HasValue)
        {
            filtered = filtered.Where(s =>
                s.SeverityScore.HasValue && s.SeverityScore.Value >= minSeverity.Value);
        }

        filtered = filtered.Where(s =>
        {
            propertyLookup.TryGetValue(s.PropertyId, out var p);
            var communityName = ResolveCommunityName(p, communityById);
            return MatchesScamReportQuery(queryText, s, p, communityName);
        });

        var result = filtered
            .OrderByDescending(s => s.DateReported)
            .Take(capped)
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

        return result;
    }

    public async Task<object> SearchAsync(SearchQueryDto query, CancellationToken cancellationToken = default)
    {
        return query.EntityType switch
        {
            SearchEntityType.Property => await SearchPropertiesAsync(
                query.QueryText,
                query.City,
                query.Limit,
                cancellationToken),
            SearchEntityType.Review => await SearchReviewsAsync(
                query.QueryText,
                query.City,
                query.MinRent,
                query.MaxRent,
                query.MinRating,
                cancellationToken),
            SearchEntityType.ScamReport => await SearchScamReportsAsync(
                query.QueryText,
                query.City,
                query.MinSeverity,
                query.Limit,
                cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(query.EntityType), query.EntityType, null)
        };
    }

    private static bool MatchesPropertyQuery(string? queryText, Property property, string communityName)
    {
        if (string.IsNullOrWhiteSpace(queryText))
        {
            return true;
        }

        var q = queryText.Trim();
        return ContainsIgnoreCase(property.Title, q)
            || ContainsIgnoreCase(property.StreetAddress, q)
            || ContainsIgnoreCase(property.City, q)
            || ContainsIgnoreCase(communityName, q);
    }

    private static bool MatchesReviewPropertyQuery(string? queryText, Property? property, string communityName)
    {
        if (string.IsNullOrWhiteSpace(queryText))
        {
            return true;
        }

        var q = queryText.Trim();
        var title = property?.Title ?? string.Empty;
        var street = property?.StreetAddress ?? string.Empty;
        var city = property?.City ?? string.Empty;

        return ContainsIgnoreCase(title, q)
            || ContainsIgnoreCase(street, q)
            || ContainsIgnoreCase(city, q)
            || (!string.IsNullOrEmpty(communityName) && ContainsIgnoreCase(communityName, q));
    }

    private static bool MatchesScamReportQuery(
        string? queryText,
        ScamReport scam,
        Property? property,
        string communityName)
    {
        if (string.IsNullOrWhiteSpace(queryText))
        {
            return true;
        }

        var q = queryText.Trim();
        var title = property?.Title ?? string.Empty;
        var street = property?.StreetAddress ?? string.Empty;
        var city = property?.City ?? string.Empty;

        return ContainsIgnoreCase(title, q)
            || ContainsIgnoreCase(street, q)
            || ContainsIgnoreCase(city, q)
            || ContainsIgnoreCase(communityName, q)
            || ContainsIgnoreCase(scam.ScamType, q)
            || ContainsIgnoreCase(scam.Description, q);
    }

    private static bool ContainsIgnoreCase(string? field, string query)
    {
        return !string.IsNullOrEmpty(field)
            && field.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolveCommunityName(Property? property, Dictionary<Guid, Community> communityById)
    {
        if (property?.CommunityId is not { } id)
        {
            return string.Empty;
        }

        return communityById.TryGetValue(id, out var c) ? (c.Name ?? string.Empty) : string.Empty;
    }
}
