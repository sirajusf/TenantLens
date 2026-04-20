using LeaseLense.Application.Properties;

namespace LeaseLense.Application.Abstractions;

public interface IPropertyDirectoryService
{
    Task<PropertyDirectoryResultDto> SearchAsync(PropertyDirectoryQueryDto query, CancellationToken cancellationToken = default);
    Task<PropertyProfileDto?> GetProfileAsync(Guid propertyId, CancellationToken cancellationToken = default);
}
