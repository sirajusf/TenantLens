using LeaseLense.Application.Abstractions;
using LeaseLense.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace LeaseLense.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var connectionString = environment.IsDevelopment()
            ? configuration.GetConnectionString("LocalSqlConnection")
                ?? configuration.GetConnectionString("DefaultConnection")
            : configuration.GetConnectionString("DefaultConnection");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Connection string was not found. Set ConnectionStrings:DefaultConnection (or LocalSqlConnection in Development).");
        }

        services.AddDbContext<LeaseLensDbContext>(options =>
        {
            options.UseSqlServer(connectionString, sql =>
            {
                sql.EnableRetryOnFailure(3, TimeSpan.FromSeconds(3), null);
            });
        });
        services.AddDbContext<AuthDbContext>(options =>
        {
            options.UseSqlServer(connectionString, sql =>
            {
                sql.EnableRetryOnFailure(3, TimeSpan.FromSeconds(3), null);
            });
        });

        services.AddScoped<ILeaseLensRepository>(sp => sp.GetRequiredService<LeaseLensDbContext>());

        return services;
    }
}
