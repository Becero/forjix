using Forjix.Application.Abstractions.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace Forjix.Infrastructure.Persistence.Tenant;

internal sealed class TenantDbContextFactory : ITenantDbContextFactory
{
    public TenantDbContext Create(ResolvedTenantDatabase tenantDatabase)
    {
        ArgumentNullException.ThrowIfNull(tenantDatabase);

        var options = new DbContextOptionsBuilder<TenantDbContext>()
            .UseSqlServer(tenantDatabase.ConnectionString)
            .Options;

        return new TenantDbContext(options);
    }
}

