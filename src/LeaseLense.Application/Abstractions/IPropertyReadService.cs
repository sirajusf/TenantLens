using LeaseLense.Application.Properties;

namespace LeaseLense.Application.Abstractions;

public interface IPropertyReadService
{
    Task<IReadOnlyList<PropertyListItemDto>> GetPropertyListAsync(CancellationToken cancellationToken = default);
}
