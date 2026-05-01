using LeaseLense.Application.Abstractions;
using LeaseLense.Application.Reputation;

namespace LeaseLense.Application.Services;

public sealed class ReputationMvpService : IReputationMvpService
{
    private readonly ILeaseLensRepository _repository;

    public ReputationMvpService(ILeaseLensRepository repository)
    {
        _repository = repository;
    }

    public async Task<IReadOnlyList<PropertyReputationDto>> GetPropertyReputationsAsync(CancellationToken cancellationToken = default)
    {
        var properties = await _repository.GetPropertiesAsync(cancellationToken);
        var reviews = await _repository.GetReviewsAsync(cancellationToken);
        var ratings = await _repository.GetReviewRatingsAsync(cancellationToken);
        var issueTags = await _repository.GetReviewIssueTagsAsync(cancellationToken);
        var scamReports = await _repository.GetScamReportsAsync(cancellationToken);

        var reviewByProperty = reviews.GroupBy(x => x.PropertyId).ToDictionary(x => x.Key, x => x.Select(y => y.ReviewId).ToHashSet());
        var ratingByReview = ratings.GroupBy(x => x.ReviewId).ToDictionary(x => x.Key, x => x.ToList());
        var issuesByReview = issueTags.GroupBy(x => x.ReviewId).ToDictionary(x => x.Key, x => x.Select(y => y.IssueType).ToList());
        var scamsByProperty = scamReports.GroupBy(x => x.PropertyId).ToDictionary(x => x.Key, x => x.ToList());

        var result = new List<PropertyReputationDto>();

        foreach (var property in properties)
        {
            reviewByProperty.TryGetValue(property.PropertyId, out var reviewIdsForProperty);
            reviewIdsForProperty ??= [];

            var propertyRatings = reviewIdsForProperty
                .Where(ratingByReview.ContainsKey)
                .SelectMany(reviewId => ratingByReview[reviewId])
                .ToList();

            var maintenanceScores = propertyRatings
                .Where(x => x.RatingCategory.Contains("maintenance", StringComparison.OrdinalIgnoreCase))
                .Select(x => x.RatingScore)
                .ToList();

            var communicationScores = propertyRatings
                .Where(x => x.RatingCategory.Contains("communication", StringComparison.OrdinalIgnoreCase))
                .Select(x => x.RatingScore)
                .ToList();

            var trustBase = propertyRatings.Select(x => x.RatingScore).ToList();

            var issuePenalty = reviewIdsForProperty
                .Where(issuesByReview.ContainsKey)
                .SelectMany(reviewId => issuesByReview[reviewId])
                .Count() * 0.08m;

            scamsByProperty.TryGetValue(property.PropertyId, out var propertyScams);
            propertyScams ??= [];
            var scamPenalty = propertyScams.Count * 0.22m;

            var maintenanceScore = maintenanceScores.Count == 0 ? 0m : Math.Round((decimal)maintenanceScores.Average(), 1);
            var communicationScore = communicationScores.Count == 0 ? 0m : Math.Round((decimal)communicationScores.Average(), 1);
            var trustRaw = trustBase.Count == 0 ? 0m : (decimal)trustBase.Average();
            var trustScore = Math.Clamp(Math.Round(trustRaw - issuePenalty - scamPenalty, 1), 0m, 5m);

            var nonZeroScores = new[] { maintenanceScore, communicationScore, trustScore }.Where(x => x > 0m).ToList();
            var overall = nonZeroScores.Count == 0 ? 0m : Math.Round(nonZeroScores.Average(), 1);

            result.Add(new PropertyReputationDto
            {
                PropertyId = property.PropertyId,
                Title = property.Title,
                City = property.City,
                LandlordName = property.LandlordName,
                OverallScore = overall,
                MaintenanceScore = maintenanceScore,
                CommunicationScore = communicationScore,
                TrustScore = trustScore,
                ReviewCount = reviewIdsForProperty.Count,
                ScamReportCount = propertyScams.Count
            });
        }

        return result
            .OrderByDescending(x => x.OverallScore)
            .ThenByDescending(x => x.ReviewCount)
            .Take(50)
            .ToList();
    }
}
