using LeaseLense.Application.Home;

namespace LeaseLense.Application.Abstractions;

public interface IHomePageReadService
{
    Task<HomePageDataDto> GetHomePageDataAsync(CancellationToken cancellationToken = default);
}
