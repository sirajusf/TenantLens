using LeaseLense.Domain.Entities;

namespace LeaseLense.Application.Abstractions.Persistence;

public interface ILeaseLensDbContext
{
    Task<List<Property>> GetPropertiesAsync(CancellationToken cancellationToken = default);
    Task<List<Renter>> GetRentersAsync(CancellationToken cancellationToken = default);
    Task<Renter?> GetRenterByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<List<Review>> GetReviewsAsync(CancellationToken cancellationToken = default);
    Task<List<ReviewRating>> GetReviewRatingsAsync(CancellationToken cancellationToken = default);
    Task<List<ReviewIssueTag>> GetReviewIssueTagsAsync(CancellationToken cancellationToken = default);
    Task<List<ScamReport>> GetScamReportsAsync(CancellationToken cancellationToken = default);
    Task AddPropertyAsync(Property property, CancellationToken cancellationToken = default);
    Task AddRenterAsync(Renter renter, CancellationToken cancellationToken = default);
    Task AddReviewAsync(Review review, CancellationToken cancellationToken = default);
    Task AddReviewRatingAsync(ReviewRating reviewRating, CancellationToken cancellationToken = default);
    Task AddScamReportAsync(ScamReport scamReport, CancellationToken cancellationToken = default);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
