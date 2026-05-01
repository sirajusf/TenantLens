using LeaseLense.Application.Abstractions;
using LeaseLense.Application.Properties;
using LeaseLense.Domain.Entities;

namespace LeaseLense.Application.Services;

public sealed class PropertyReadService : IPropertyReadService
{
    private readonly ILeaseLensRepository _repository;

    public PropertyReadService(ILeaseLensRepository repository)
    {
        _repository = repository;
    }

    public async Task<IReadOnlyList<PropertyListItemDto>> GetPropertyListAsync(CancellationToken cancellationToken = default)
    {
        var properties = await _repository.GetPropertiesAsync(cancellationToken);
        var communities = await _repository.GetCommunitiesAsync(cancellationToken);
        var communityById = communities.ToDictionary(x => x.CommunityId);

        return properties
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new PropertyListItemDto
            {
                PropertyId = x.PropertyId,
                Title = x.Title,
                CommunityName = ResolveCommunityName(x, communityById),
                StreetAddress = x.StreetAddress,
                City = x.City,
                Country = x.Country,
                CreatedAt = x.CreatedAt
            })
            .ToList();
    }

    private static string ResolveCommunityName(Property? property, Dictionary<Guid, Community> communityById)
    {
        if (property?.CommunityId is not { } id)
        {
            return string.Empty;
        }

        return communityById.TryGetValue(id, out var c) ? (c.Name ?? string.Empty) : string.Empty;
    }
}
