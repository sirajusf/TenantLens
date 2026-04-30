using LeaseLense.Application.Abstractions;
using LeaseLense.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace LeaseLense.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IPropertyReadService, PropertyReadService>();
        services.AddScoped<ICoreSearchService, CoreSearchService>();
        services.AddScoped<IPropertyDirectoryService, PropertyDirectoryService>();
        services.AddScoped<IHomePageReadService, HomePageReadService>();
        services.AddScoped<IReviewMvpService, ReviewMvpService>();
        services.AddScoped<IReputationMvpService, ReputationMvpService>();
        services.AddScoped<IScamReportMvpService, ScamReportMvpService>();
        services.AddScoped<IUserAccountService, UserAccountService>();
        services.AddScoped<IProfileService, ProfileService>();
        return services;
    }
}
