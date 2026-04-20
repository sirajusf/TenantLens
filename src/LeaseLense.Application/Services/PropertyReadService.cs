using LeaseLense.Application.Abstractions;
using LeaseLense.Application.Abstractions.Persistence;
using LeaseLense.Application.Properties;

namespace LeaseLense.Application.Services;

public sealed class PropertyReadService : IPropertyReadService
{
    private readonly ILeaseLensDbContext _dbContext;

    public PropertyReadService(ILeaseLensDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<PropertyListItemDto>> GetPropertyListAsync(CancellationToken cancellationToken = default)
    {
        var properties = await _dbContext.GetPropertiesAsync(cancellationToken);

        return properties
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new PropertyListItemDto
            {
                PropertyId = x.PropertyId,
                Title = x.Title,
                StreetAddress = x.StreetAddress,
                City = x.City,
                Country = x.Country,
                CreatedAt = x.CreatedAt
            })
            .ToList();
    }
}
