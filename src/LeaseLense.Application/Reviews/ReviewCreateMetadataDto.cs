namespace LeaseLense.Application.Reviews;

public sealed class ReviewCreateMetadataDto
{
    public IReadOnlyList<ReviewPropertyOptionDto> Properties { get; init; } = [];
}
