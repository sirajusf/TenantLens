using LeaseLense.Application.Reputation;

namespace LeaseLense.Application.Abstractions;

public interface IReputationMvpService
{
    Task<IReadOnlyList<PropertyReputationDto>> GetPropertyReputationsAsync(CancellationToken cancellationToken = default);
}
