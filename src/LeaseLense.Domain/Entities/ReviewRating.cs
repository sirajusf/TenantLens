namespace LeaseLense.Domain.Entities;

public sealed class ReviewRating
{
    public Guid ReviewRatingId { get; set; }
    public Guid ReviewId { get; set; }
    public string RatingCategory { get; set; } = string.Empty;
    public int RatingScore { get; set; }
}
