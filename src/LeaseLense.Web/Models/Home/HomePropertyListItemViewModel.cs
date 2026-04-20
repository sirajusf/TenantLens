namespace LeaseLense.Web.Models.Home;

public sealed class HomePropertyListItemViewModel
{
    public Guid PropertyId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string StreetAddress { get; init; } = string.Empty;
    public string City { get; init; } = string.Empty;
    public string Country { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
}
