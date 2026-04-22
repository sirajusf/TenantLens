using LeaseLense.Application.Abstractions;
using LeaseLense.Application.Abstractions.Persistence;
using LeaseLense.Application.Common;
using LeaseLense.Application.Home;
using LeaseLense.Application.Properties;

namespace LeaseLense.Application.Services;

public sealed class HomePageReadService : IHomePageReadService
{
    private readonly ILeaseLensDbContext _dbContext;

    public HomePageReadService(ILeaseLensDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<HomePageDataDto> GetHomePageDataAsync(CancellationToken cancellationToken = default)
    {
        var properties = await _dbContext.GetPropertiesAsync(cancellationToken);
        var reviews = await _dbContext.GetReviewsAsync(cancellationToken);
        var reviewRatings = await _dbContext.GetReviewRatingsAsync(cancellationToken);
        var scamReports = await _dbContext.GetScamReportsAsync(cancellationToken);
        var verifications = await _dbContext.GetRenterPropertyVerificationsAsync(cancellationToken);

        var propertyLookup = properties.ToDictionary(x => x.PropertyId, x => x);
        var reviewAverageRatings = reviewRatings
            .GroupBy(x => x.ReviewId)
            .ToDictionary(
                group => group.Key,
                group => (decimal?)Math.Round(group.Average(x => x.RatingScore), 1));

        var propertyDtos = properties
            .OrderByDescending(x => x.CreatedAt)
            .Take(6)
            .Select(x => new PropertyListItemDto
            {
                PropertyId = x.PropertyId,
                Title = x.Title,
                StreetAddress = x.StreetAddress,
                City = x.City,
                Country = x.Country,
                CreatedAt = x.CreatedAt
            })
            .ToList();

        var reviewDtos = reviews
            .OrderByDescending(x => x.CreatedAt)
            .Take(3)
            .Select(x =>
            {
                propertyLookup.TryGetValue(x.PropertyId, out var property);
                var hasVerifiedStay = verifications.Any(v =>
                    v.RenterId == x.RenterId
                    && v.PropertyId == x.PropertyId
                    && string.Equals(v.Status, "verified_stay", StringComparison.OrdinalIgnoreCase));
                return new HomeReviewListItemDto
                {
                    ReviewId = x.ReviewId,
                    PropertyTitle = property?.Title ?? "Unknown Property",
                    City = property?.City ?? "Unknown City",
                    AverageRating = reviewAverageRatings.GetValueOrDefault(x.ReviewId),
                    MonthlyRent = x.MonthlyRent,
                    VerificationBadge = hasVerifiedStay ? "Verified Stay" : null,
                    ReviewText = string.IsNullOrWhiteSpace(x.ReviewText)
                        ? "No review details provided."
                        : x.ReviewText
                };
            })
            .ToList();

        var scamDtos = scamReports
            .OrderByDescending(x => x.DateReported)
            .Take(3)
            .Select(x =>
            {
                propertyLookup.TryGetValue(x.PropertyId, out var property);
                var hasVerifiedStay = verifications.Any(v =>
                    v.RenterId == x.RenterId
                    && v.PropertyId == x.PropertyId
                    && string.Equals(v.Status, "verified_stay", StringComparison.OrdinalIgnoreCase));
                return new HomeScamReportListItemDto
                {
                    ScamReportId = x.ScamReportId,
                    PropertyTitle = property?.Title ?? "Unknown Property",
                    City = property?.City ?? "Unknown City",
                    ScamType = DisplayTextFormatter.ToTitleLabel(x.ScamType),
                    SeverityScore = x.SeverityScore,
                    VerificationBadge = hasVerifiedStay ? "Verified Stay" : null,
                    Description = string.IsNullOrWhiteSpace(x.Description)
                        ? "No scam details provided."
                        : x.Description
                };
            })
            .ToList();

        return new HomePageDataDto
        {
            Properties = propertyDtos,
            Reviews = reviewDtos,
            ScamReports = scamDtos
        };
    }
}
