namespace LeaseLense.Application.Reviews;

public sealed class ReviewPropertyOptionDto
{
    public Guid PropertyId { get; init; }
    public string Label { get; init; } = string.Empty;
}
