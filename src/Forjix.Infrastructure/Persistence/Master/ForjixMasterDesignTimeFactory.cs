using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Forjix.Infrastructure.Persistence.Master;

public sealed class ForjixMasterDesignTimeFactory : IDesignTimeDbContextFactory<ForjixMasterDbContext>
{
    public ForjixMasterDbContext CreateDbContext(string[] args)
    {
        const string designTimeConnection = "Server=(localdb)\\mssqllocaldb;Database=ForjixMaster;Trusted_Connection=True;TrustServerCertificate=True";
        var options = new DbContextOptionsBuilder<ForjixMasterDbContext>().UseSqlServer(designTimeConnection).Options;
        return new ForjixMasterDbContext(options);
    }
}

