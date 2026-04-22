namespace LeaseLense.Application.Reviews;

public sealed class ReviewCreateMetadataDto
{
    public bool CanSubmit { get; init; }
    public string? RestrictionMessage { get; init; }
    public IReadOnlyList<ReviewPropertyOptionDto> Properties { get; init; } = [];
}
