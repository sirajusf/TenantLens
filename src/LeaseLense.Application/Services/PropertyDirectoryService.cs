using LeaseLense.Application.Abstractions;
using LeaseLense.Application.Common;
using LeaseLense.Application.Properties;
using LeaseLense.Domain.Entities;

namespace LeaseLense.Application.Services;

public sealed class PropertyDirectoryService : IPropertyDirectoryService
{
    private readonly ILeaseLensRepository _repository;
    private readonly ICoreSearchService _coreSearch;

    public PropertyDirectoryService(ILeaseLensRepository repository, ICoreSearchService coreSearch)
    {
        _repository = repository;
        _coreSearch = coreSearch;
    }

    public async Task<PropertyDirectoryResultDto> SearchAsync(PropertyDirectoryQueryDto query, CancellationToken cancellationToken = default)
    {
        var matches = await _coreSearch.SearchPropertiesAsync(
            query.QueryText,
            city: null,
            Math.Clamp(query.Limit, 1, 200),
            cancellationToken);

        var reviews = await _repository.GetReviewsAsync(cancellationToken);
        var ratings = await _repository.GetReviewRatingsAsync(cancellationToken);
        var scamReports = await _repository.GetScamReportsAsync(cancellationToken);

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

        var items = matches
            .Select(x => new PropertyDirectoryItemDto
            {
                PropertyId = x.Property.PropertyId,
                Title = x.Property.Title,
                CommunityName = x.CommunityName,
                StreetAddress = x.Property.StreetAddress,
                City = x.Property.City,
                Country = x.Property.Country,
                AverageRating = avgRatingByProperty.GetValueOrDefault(x.Property.PropertyId),
                AverageScamSeverity = avgScamSeverityByProperty.GetValueOrDefault(x.Property.PropertyId)
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
        var property = (await _repository.GetPropertiesAsync(cancellationToken))
            .FirstOrDefault(x => x.PropertyId == propertyId);
        if (property is null)
        {
            return null;
        }

        var communities = await _repository.GetCommunitiesAsync(cancellationToken);
        var communityById = communities.ToDictionary(x => x.CommunityId);
        var communityName = ResolveCommunityName(property, communityById);

        var reviews = (await _repository.GetReviewsAsync(cancellationToken))
            .Where(x => x.PropertyId == propertyId)
            .OrderByDescending(x => x.CreatedAt)
            .ToList();
        var ratings = await _repository.GetReviewRatingsAsync(cancellationToken);
        var scamReports = (await _repository.GetScamReportsAsync(cancellationToken))
            .Where(x => x.PropertyId == propertyId)
            .OrderByDescending(x => x.DateReported)
            .ToList();
        var verifications = await _repository.GetRenterPropertyVerificationsAsync(cancellationToken);

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
            CommunityName = communityName,
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

    private static string ResolveCommunityName(Property? property, Dictionary<Guid, Community> communityById)
    {
        if (property?.CommunityId is not { } id)
        {
            return string.Empty;
        }

        return communityById.TryGetValue(id, out var c) ? (c.Name ?? string.Empty) : string.Empty;
    }
}
