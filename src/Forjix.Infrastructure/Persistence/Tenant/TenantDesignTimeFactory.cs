using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Forjix.Infrastructure.Persistence.Tenant;

public sealed class TenantDesignTimeFactory : IDesignTimeDbContextFactory<TenantDbContext>
{
    public TenantDbContext CreateDbContext(string[] args)
    {
        const string designTimeConnection = "Server=(localdb)\\mssqllocaldb;Database=ForjixTemplate;Trusted_Connection=True;TrustServerCertificate=True";
        var options = new DbContextOptionsBuilder<TenantDbContext>().UseSqlServer(designTimeConnection).Options;
        return new TenantDbContext(options);
    }
}

