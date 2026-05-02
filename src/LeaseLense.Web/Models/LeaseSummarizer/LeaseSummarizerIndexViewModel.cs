namespace LeaseLense.Web.Models.LeaseSummarizer;

public sealed class LeaseSummarizerIndexViewModel
{
    public List<LeaseSummarizerPropertyOptionViewModel> Properties { get; init; } = [];
}

public sealed class LeaseSummarizerPropertyOptionViewModel
{
    public Guid PropertyId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string City { get; init; } = string.Empty;
    public string Country { get; init; } = string.Empty;

    public string DisplayName => string.Join(", ", new[] { Title, City, Country }.Where(x => !string.IsNullOrWhiteSpace(x)));
}

