using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RepairDesk.Core.Entities;

namespace RepairDesk.DAL.Persistence;

public static class DbInitializer
{
    public static readonly Guid LopesTechTenantId = new("a1b2c3d4-e5f6-7890-abcd-ef1234567890");

    public static async Task InitializeAsync(IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<AppDbContext>>();

        logger.LogInformation("Applying database migrations...");
        await db.Database.MigrateAsync(ct);

        var hasLopesTech = await db.Tenants.IgnoreQueryFilters()
            .AnyAsync(t => t.Id == LopesTechTenantId, ct);

        if (!hasLopesTech)
        {
            logger.LogInformation("Seeding LopesTech tenant...");
            db.Tenants.Add(new Tenant
            {
                Id = LopesTechTenantId,
                Name = "LopesTech",
                LegalName = "Bruno Miguel Martins da Silva Lopes",
                Nif = null,
                Address = "São Pedro de France, Viseu",
                Email = "bruno.miguel.martins.lopes@gmail.com",
                PrimaryColor = "#0EA5E9",
                IsActive = true
            });
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Seed complete.");
        }
    }
}
