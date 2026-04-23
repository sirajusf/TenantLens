namespace LeaseLense.Application.Reviews;

public sealed class ReviewListPageDto
{
    public ReviewListSummaryDto Summary { get; init; } = new();
    public IReadOnlyList<ReviewListItemDto> Items { get; init; } = [];
}
