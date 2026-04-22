using LeaseLense.Application.Abstractions;
using LeaseLense.Application.Abstractions.Persistence;
using LeaseLense.Application.Common;
using LeaseLense.Application.Properties;

namespace LeaseLense.Application.Services;

public sealed class PropertyDirectoryService : IPropertyDirectoryService
{
    private readonly ILeaseLensDbContext _dbContext;

    public PropertyDirectoryService(ILeaseLensDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PropertyDirectoryResultDto> SearchAsync(PropertyDirectoryQueryDto query, CancellationToken cancellationToken = default)
    {
        var normalizedQuery = Normalize(query.QueryText);
        var properties = await _dbContext.GetPropertiesAsync(cancellationToken);
        var reviews = await _dbContext.GetReviewsAsync(cancellationToken);
        var ratings = await _dbContext.GetReviewRatingsAsync(cancellationToken);
        var scamReports = await _dbContext.GetScamReportsAsync(cancellationToken);
        var verifications = await _dbContext.GetRenterPropertyVerificationsAsync(cancellationToken);

        var reviewIdsByProperty = reviews
            .GroupBy(x => x.PropertyId)
            .ToDictionary(x => x.Key, x => x.Select(y => y.ReviewId).ToHashSet());

        var avgRatingsByReview = ratings
            .GroupBy(x => x.ReviewId)
            .ToDictionary(x => x.Key, x => (decimal?)Math.Round((decimal)x.Average(y => y.RatingScore), 1));

        var avgRatingByProperty = new Dictionary<Guid, decimal?>();
        foreach (var pair in reviewIdsByProperty)
        {
            var values = pair.Value
                .Select(reviewId => avgRatingsByReview.GetValueOrDefault(reviewId))
                .Where(v => v.HasValue)
                .Select(v => v!.Value)
                .ToList();
            avgRatingByProperty[pair.Key] = values.Count == 0 ? null : Math.Round(values.Average(), 1);
        }

        var avgScamSeverityByProperty = scamReports
            .Where(x => x.SeverityScore.HasValue)
            .GroupBy(x => x.PropertyId)
            .ToDictionary(x => x.Key, x => (decimal?)Math.Round(x.Average(y => y.SeverityScore!.Value), 1));

        var items = properties
            .Select(p => new
            {
                Property = p,
                MatchScore = CalculateMatchScore(normalizedQuery, p.Title, p.StreetAddress, p.City)
            })
            .Where(x => string.IsNullOrWhiteSpace(normalizedQuery) || x.MatchScore > 0)
            .OrderByDescending(x => x.MatchScore)
            .ThenBy(x => x.Property.Title)
            .ThenBy(x => x.Property.StreetAddress)
            .ThenBy(x => x.Property.PropertyId)
            .Take(Math.Clamp(query.Limit, 1, 200))
            .Select(x => new PropertyDirectoryItemDto
            {
                PropertyId = x.Property.PropertyId,
                Title = x.Property.Title,
                StreetAddress = x.Property.StreetAddress,
                City = x.Property.City,
                Country = x.Property.Country,
                AverageRating = avgRatingByProperty.GetValueOrDefault(x.Property.PropertyId),
                AverageScamSeverity = avgScamSeverityByProperty.GetValueOrDefault(x.Property.PropertyId),
                MatchScore = x.MatchScore
            })
            .ToList();

        return new PropertyDirectoryResultDto
        {
            QueryText = query.QueryText?.Trim() ?? string.Empty,
            Items = items
        };
    }

    public async Task<PropertyProfileDto?> GetProfileAsync(Guid propertyId, CancellationToken cancellationToken = default)
    {
        var property = (await _dbContext.GetPropertiesAsync(cancellationToken))
            .FirstOrDefault(x => x.PropertyId == propertyId);
        if (property is null)
        {
            return null;
        }

        var reviews = (await _dbContext.GetReviewsAsync(cancellationToken))
            .Where(x => x.PropertyId == propertyId)
            .OrderByDescending(x => x.CreatedAt)
            .ToList();
        var ratings = await _dbContext.GetReviewRatingsAsync(cancellationToken);
        var scamReports = (await _dbContext.GetScamReportsAsync(cancellationToken))
            .Where(x => x.PropertyId == propertyId)
            .OrderByDescending(x => x.DateReported)
            .ToList();
        var verifications = await _dbContext.GetRenterPropertyVerificationsAsync(cancellationToken);

        var avgRatingByReview = ratings
            .GroupBy(x => x.ReviewId)
            .ToDictionary(x => x.Key, x => Math.Round((decimal)x.Average(y => y.RatingScore), 1));

        var reviewItems = reviews
            .Select(x => new PropertyProfileReviewDto
            {
                ReviewId = x.ReviewId,
                ReviewerAlias = AnonymizedNameGenerator.Generate(x.ReviewId),
                MonthlyRent = x.MonthlyRent,
                AverageRating = avgRatingByReview.GetValueOrDefault(x.ReviewId, 0m),
                VerificationBadge = verifications.Any(v =>
                    v.RenterId == x.RenterId
                    && v.PropertyId == x.PropertyId
                    && string.Equals(v.Status, "verified_stay", StringComparison.OrdinalIgnoreCase))
                    ? "Verified Stay"
                    : null,
                ReviewText = string.IsNullOrWhiteSpace(x.ReviewText) ? "No review details provided." : x.ReviewText!,
                CreatedAt = x.CreatedAt
            })
            .ToList();

        var scamItems = scamReports
            .Select(x => new PropertyProfileScamReportDto
            {
                ScamReportId = x.ScamReportId,
                ScamType = DisplayTextFormatter.ToTitleLabel(x.ScamType),
                SeverityScore = x.SeverityScore,
                Description = string.IsNullOrWhiteSpace(x.Description) ? "No details provided." : x.Description!,
                VerificationBadge = verifications.Any(v =>
                    v.RenterId == x.RenterId
                    && v.PropertyId == x.PropertyId
                    && string.Equals(v.Status, "verified_stay", StringComparison.OrdinalIgnoreCase))
                    ? "Verified Stay"
                    : null,
                DateReported = x.DateReported
            })
            .ToList();

        var averageRating = reviewItems.Count == 0 ? (decimal?)null : Math.Round(reviewItems.Average(x => x.AverageRating), 1);
        var scamSeverityValues = scamItems
            .Where(x => x.SeverityScore.HasValue)
            .Select(x => x.SeverityScore!.Value)
            .ToList();
        var averageScamSeverity = scamSeverityValues.Count == 0
            ? (decimal?)null
            : Math.Round(scamSeverityValues.Average(), 1);

        return new PropertyProfileDto
        {
            PropertyId = property.PropertyId,
            Title = property.Title,
            StreetAddress = property.StreetAddress,
            City = property.City,
            Country = property.Country,
            LandlordName = property.LandlordName,
            ManagementCompanyName = property.ManagementCompanyName,
            AverageRating = averageRating,
            ReviewCount = reviewItems.Count,
            AverageScamSeverity = averageScamSeverity,
            Reviews = reviewItems,
            ScamReports = scamItems
        };
    }

    private static string Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        return string.Join(" ", text.Trim().ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static int CalculateMatchScore(string query, string title, string streetAddress, string city)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return 1;
        }

        var score = 0;
        var titleNorm = Normalize(title);
        var streetNorm = Normalize(streetAddress);
        var cityNorm = Normalize(city);

        if (titleNorm == query || streetNorm == query)
        {
            score += 100;
        }
        if (titleNorm.StartsWith(query, StringComparison.Ordinal) || streetNorm.StartsWith(query, StringComparison.Ordinal))
        {
            score += 70;
        }
        if (titleNorm.Contains(query, StringComparison.Ordinal) || streetNorm.Contains(query, StringComparison.Ordinal))
        {
            score += 40;
        }
        if (cityNorm.Contains(query, StringComparison.Ordinal))
        {
            score += 10;
        }

        return score;
    }
}
