namespace LeaseLense.Domain.Entities;

public sealed class ReviewIssueTag
{
    public Guid ReviewIssueTagId { get; set; }
    public Guid ReviewId { get; set; }
    public string IssueType { get; set; } = string.Empty;
}
