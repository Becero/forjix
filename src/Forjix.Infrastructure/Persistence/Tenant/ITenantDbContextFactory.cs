using Forjix.Application.Abstractions.Tenancy;

namespace Forjix.Infrastructure.Persistence.Tenant;

public interface ITenantDbContextFactory
{
    TenantDbContext Create(ResolvedTenantDatabase tenantDatabase);
}

