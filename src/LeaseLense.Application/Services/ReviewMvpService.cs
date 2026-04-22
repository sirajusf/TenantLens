using LeaseLense.Application.Abstractions;
using LeaseLense.Application.Abstractions.Persistence;
using LeaseLense.Application.Common;
using LeaseLense.Application.Reviews;
using LeaseLense.Domain.Entities;

namespace LeaseLense.Application.Services;

public sealed class ReviewMvpService : IReviewMvpService
{
    private readonly ILeaseLensDbContext _dbContext;

    public ReviewMvpService(ILeaseLensDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<ReviewListItemDto>> GetReviewsAsync(ReviewQueryDto query, CancellationToken cancellationToken = default)
    {
        var properties = await _dbContext.GetPropertiesAsync(cancellationToken);
        var renters = await _dbContext.GetRentersAsync(cancellationToken);
        var reviews = await _dbContext.GetReviewsAsync(cancellationToken);
        var ratings = await _dbContext.GetReviewRatingsAsync(cancellationToken);
        var verifications = await _dbContext.GetRenterPropertyVerificationsAsync(cancellationToken);

        var propertyLookup = properties.ToDictionary(x => x.PropertyId);
        var renterLookup = renters.ToDictionary(x => x.RenterId);
        var averageRatings = ratings
            .GroupBy(x => x.ReviewId)
            .ToDictionary(x => x.Key, x => Math.Round((decimal)x.Average(y => y.RatingScore), 1));

        var shaped = reviews
            .Select(review =>
            {
                propertyLookup.TryGetValue(review.PropertyId, out var property);
                renterLookup.TryGetValue(review.RenterId, out var renter);

                var reviewerAlias = AnonymizedNameGenerator.Generate(review.ReviewId);
                var hasVerifiedStay = verifications.Any(v =>
                    v.RenterId == review.RenterId
                    && v.PropertyId == review.PropertyId
                    && string.Equals(v.Status, "verified_stay", StringComparison.OrdinalIgnoreCase));

                return new
                {
                    Review = new ReviewListItemDto
                    {
                        ReviewId = review.ReviewId,
                        PropertyId = review.PropertyId,
                        PropertyTitle = property?.Title ?? "Unknown Property",
                        StreetAddress = property?.StreetAddress ?? "Unknown Address",
                        City = property?.City ?? "Unknown City",
                        MonthlyRent = review.MonthlyRent,
                        AverageRating = averageRatings.GetValueOrDefault(review.ReviewId, 0m),
                        VerificationBadge = hasVerifiedStay ? "Verified Stay" : "Unverified Stay",
                        AnonymizedReviewer = reviewerAlias,
                        ReviewText = string.IsNullOrWhiteSpace(review.ReviewText)
                            ? "No review details provided."
                            : review.ReviewText!
                    },
                    review.CreatedAt
                };
            });

        if (!string.IsNullOrWhiteSpace(query.PropertyQuery))
        {
            var queryText = query.PropertyQuery.Trim();
            shaped = shaped.Where(x =>
                x.Review.PropertyTitle.Contains(queryText, StringComparison.OrdinalIgnoreCase)
                || x.Review.StreetAddress.Contains(queryText, StringComparison.OrdinalIgnoreCase)
                || x.Review.City.Contains(queryText, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(query.City))
        {
            shaped = shaped.Where(x => string.Equals(x.Review.City, query.City, StringComparison.OrdinalIgnoreCase));
        }

        if (query.MinRent.HasValue)
        {
            shaped = shaped.Where(x => x.Review.MonthlyRent.HasValue && x.Review.MonthlyRent.Value >= query.MinRent.Value);
        }

        if (query.MaxRent.HasValue)
        {
            shaped = shaped.Where(x => x.Review.MonthlyRent.HasValue && x.Review.MonthlyRent.Value <= query.MaxRent.Value);
        }

        if (query.MinRating.HasValue)
        {
            shaped = shaped.Where(x => x.Review.AverageRating >= query.MinRating.Value);
        }

        shaped = query.SortBy?.ToLowerInvariant() switch
        {
            "rating" => shaped.OrderByDescending(x => x.Review.AverageRating).ThenBy(x => x.Review.PropertyTitle),
            "rent" => shaped.OrderBy(x => x.Review.MonthlyRent ?? decimal.MaxValue).ThenBy(x => x.Review.PropertyTitle),
            _ => shaped.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Review.PropertyTitle)
        };

        return shaped.Select(x => x.Review).Take(50).ToList();
    }

    public async Task<ReviewCreateMetadataDto> GetCreateMetadataAsync(string reporterEmail, CancellationToken cancellationToken = default)
    {
        var properties = await _dbContext.GetPropertiesAsync(cancellationToken);
        var canSubmit = false;
        var restrictionMessage = "You can submit for any property. Verified Stay applies only to your matched verified residency.";
        if (!string.IsNullOrWhiteSpace(reporterEmail))
        {
            var renter = await _dbContext.GetRenterByEmailAsync(reporterEmail.Trim(), cancellationToken);
            if (renter is not null)
            {
                canSubmit = true;
            }
        }

        return new ReviewCreateMetadataDto
        {
            CanSubmit = canSubmit,
            RestrictionMessage = restrictionMessage,
            Properties = properties
                .OrderBy(x => x.Title)
                .ThenBy(x => x.StreetAddress)
                .Select(x => new ReviewPropertyOptionDto
                {
                    PropertyId = x.PropertyId,
                    Label = $"{x.Title} - {x.StreetAddress}, {x.City}"
                })
                .ToList()
        };
    }

    public async Task SubmitReviewAsync(CreateReviewDto request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.ReporterEmail))
        {
            throw new InvalidOperationException("Reporter email is required.");
        }

        var renter = await _dbContext.GetRenterByEmailAsync(request.ReporterEmail.Trim(), cancellationToken);
        if (renter is null)
        {
            throw new InvalidOperationException("Authenticated renter profile was not found.");
        }

        var propertyId = request.PropertyId;
        if (!propertyId.HasValue)
        {
            propertyId = await CreateOrResolvePropertyAsync(renter.RenterId, request, cancellationToken);
        }

        var review = new Review
        {
            ReviewId = Guid.NewGuid(),
            PropertyId = propertyId.Value,
            RenterId = renter.RenterId,
            MonthlyRent = request.MonthlyRent,
            UnitType = string.IsNullOrWhiteSpace(request.UnitType) ? null : request.UnitType.Trim(),
            ReviewText = request.ReviewText.Trim(),
            VerificationStatus = "unverified",
            CreatedAt = DateTime.UtcNow
        };

        var rating = new ReviewRating
        {
            ReviewRatingId = Guid.NewGuid(),
            ReviewId = review.ReviewId,
            RatingCategory = "overall_experience",
            RatingScore = request.OverallRating
        };

        await _dbContext.AddReviewAsync(review, cancellationToken);
        await _dbContext.AddReviewRatingAsync(rating, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<Guid> CreateOrResolvePropertyAsync(
        Guid renterId,
        CreateReviewDto request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.NewPropertyTitle)
            || string.IsNullOrWhiteSpace(request.NewCommunityName)
            || string.IsNullOrWhiteSpace(request.NewPropertyStreetAddress)
            || string.IsNullOrWhiteSpace(request.NewPropertyCity)
            || string.IsNullOrWhiteSpace(request.NewPropertyCountry))
        {
            throw new InvalidOperationException("Property details are required for unlisted property submission.");
        }

        var title = request.NewPropertyTitle.Trim();
        var communityName = request.NewCommunityName.Trim();
        var street = request.NewPropertyStreetAddress.Trim();
        var city = request.NewPropertyCity.Trim();
        var country = request.NewPropertyCountry.Trim();
        var communities = await _dbContext.GetCommunitiesAsync(cancellationToken);
        var community = communities.FirstOrDefault(x =>
            string.Equals(x.Name, communityName, StringComparison.OrdinalIgnoreCase));
        if (community is null)
        {
            community = new Community
            {
                CommunityId = Guid.NewGuid(),
                Name = communityName,
                City = city,
                Country = country,
                CreatedAt = DateTime.UtcNow
            };
            await _dbContext.AddCommunityAsync(community, cancellationToken);
        }

        var existingProperty = (await _dbContext.GetPropertiesAsync(cancellationToken))
            .FirstOrDefault(x =>
                string.Equals(x.StreetAddress, street, StringComparison.OrdinalIgnoreCase)
                && string.Equals(x.City, city, StringComparison.OrdinalIgnoreCase)
                && string.Equals(x.Country, country, StringComparison.OrdinalIgnoreCase));

        if (existingProperty is not null)
        {
            return existingProperty.PropertyId;
        }

        var property = new Property
        {
            PropertyId = Guid.NewGuid(),
            CommunityId = community.CommunityId,
            Title = title,
            StreetAddress = street,
            City = city,
            Country = country,
            CreatedByRenterId = renterId,
            CreatedAt = DateTime.UtcNow
        };

        await _dbContext.AddPropertyAsync(property, cancellationToken);
        return property.PropertyId;
    }
}
