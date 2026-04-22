namespace LeaseLense.Domain.Entities;

public sealed class Community
{
    public Guid CommunityId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? City { get; set; }
    public string? StateOrRegion { get; set; }
    public string? Country { get; set; }
    public DateTime CreatedAt { get; set; }
}
