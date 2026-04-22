namespace LeaseLense.Web.Models.Reviews;

public sealed class ReviewCreatePageViewModel
{
    public CreateReviewViewModel Form { get; init; } = new();
    public bool CanSubmit { get; init; } = true;
    public string? RestrictionMessage { get; init; }
    public IReadOnlyList<ReviewPropertyOptionViewModel> Properties { get; init; } = [];
}
