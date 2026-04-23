using LeaseLense.Application.Reviews;

namespace LeaseLense.Application.Abstractions;

public interface IReviewMvpService
{
    Task<ReviewListPageDto> GetReviewsAsync(ReviewQueryDto query, CancellationToken cancellationToken = default);
    Task<ReviewCreateMetadataDto> GetCreateMetadataAsync(string reporterEmail, CancellationToken cancellationToken = default);
    Task SubmitReviewAsync(CreateReviewDto request, CancellationToken cancellationToken = default);
}
