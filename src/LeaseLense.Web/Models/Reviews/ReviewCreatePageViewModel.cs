namespace LeaseLense.Web.Models.Reviews;

public sealed class ReviewCreatePageViewModel
{
    public CreateReviewViewModel Form { get; init; } = new();
    public IReadOnlyList<ReviewPropertyOptionViewModel> Properties { get; init; } = [];
}
