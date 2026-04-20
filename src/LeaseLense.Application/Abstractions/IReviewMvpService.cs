using LeaseLense.Application.Reviews;

namespace LeaseLense.Application.Abstractions;

public interface IReviewMvpService
{
    Task<IReadOnlyList<ReviewListItemDto>> GetReviewsAsync(ReviewQueryDto query, CancellationToken cancellationToken = default);
    Task<ReviewCreateMetadataDto> GetCreateMetadataAsync(CancellationToken cancellationToken = default);
    Task SubmitReviewAsync(CreateReviewDto request, CancellationToken cancellationToken = default);
}
