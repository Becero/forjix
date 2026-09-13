using Forjix.Application.Abstractions.Security;
using Forjix.Application.Abstractions.Tenancy;
using Forjix.Infrastructure.Persistence.Master;
using Forjix.Infrastructure.Persistence.Tenant;
using Forjix.Infrastructure.Secrets;
using Forjix.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Forjix.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var masterConnectionString = configuration.GetConnectionString("ForjixMaster");

        services.AddDbContext<ForjixMasterDbContext>(options =>
        {
            if (!string.IsNullOrWhiteSpace(masterConnectionString))
            {
                options.UseSqlServer(masterConnectionString);
            }
        });

        services.AddScoped<ITenantContext, TenantContext>();
        services.AddScoped<ITenantDatabaseResolver, TenantDatabaseResolver>();
        services.AddScoped<ITenantDbContextFactory, TenantDbContextFactory>();
        services.AddSingleton<ISecretProvider, ConfigurationSecretProvider>();

        return services;
    }
}

