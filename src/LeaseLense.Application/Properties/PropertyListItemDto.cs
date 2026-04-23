namespace LeaseLense.Application.Properties;

public sealed class PropertyListItemDto
{
    public Guid PropertyId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string CommunityName { get; init; } = string.Empty;
    public string StreetAddress { get; init; } = string.Empty;
    public string City { get; init; } = string.Empty;
    public string Country { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
}
