using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using RepairDesk.Core.Abstractions;

namespace RepairDesk.DAL.Persistence;

/// EF Core tools (migrations) instantiate the context outside of DI. This factory
/// uses LocalDB-style connection only for design-time scaffolding; runtime uses DI.
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var connStr = Environment.GetEnvironmentVariable("REPAIRDESK_CONNECTION")
            ?? "Server=localhost,1433;Database=RepairDesk;User Id=sa;Password=YourStrong!Pass1;TrustServerCertificate=True;";

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(connStr, x => x.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName))
            .Options;

        return new AppDbContext(options, new DesignTimeTenantContext());
    }

    private sealed class DesignTimeTenantContext : ITenantContext
    {
        public Guid? TenantId => null;
        public bool HasTenant => false;
    }
}
