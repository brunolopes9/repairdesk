using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;

namespace RepairDesk.DAL.Persistence;

public static class DbInitializer
{
    public static readonly Guid LopesTechTenantId = new("a1b2c3d4-e5f6-7890-abcd-ef1234567890");
    private const string DefaultAdminPassword = "ChangeMe!2026";

    public static async Task InitializeAsync(IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        var logger = sp.GetRequiredService<ILogger<AppDbContext>>();

        logger.LogInformation("Applying database migrations...");
        await db.Database.MigrateAsync(ct);

        await SeedTenantAsync(db, logger, ct);
        await SeedRolesAsync(sp, logger);
        await SeedAdminAsync(sp, logger, ct);
    }

    private static async Task SeedTenantAsync(AppDbContext db, ILogger logger, CancellationToken ct)
    {
        if (await db.Tenants.IgnoreQueryFilters().AnyAsync(t => t.Id == LopesTechTenantId, ct))
            return;

        logger.LogInformation("Seeding LopesTech tenant...");
        db.Tenants.Add(new Tenant
        {
            Id = LopesTechTenantId,
            Name = "LopesTech",
            LegalName = "Bruno Miguel Martins da Silva Lopes",
            Address = "São Pedro de France, Viseu",
            Email = "bruno.miguel.martins.lopes@gmail.com",
            PrimaryColor = "#0EA5E9",
            IsActive = true
        });
        await db.SaveChangesAsync(ct);
    }

    private static async Task SeedRolesAsync(IServiceProvider sp, ILogger logger)
    {
        var roleManager = sp.GetRequiredService<RoleManager<AppRole>>();
        // Sprint 311 (Doc 72 Fase D): roles canónicos em AppRoles (Admin/Tech/Cashier/ReadOnly).
        // Os roles legacy do enum UserRole (Tecnico/Recepcao/Visualizador) deixaram de ser
        // semeados — nunca foram usados em [Authorize(Roles=...)] e ficam como cleanup pendente.
        foreach (var role in RepairDesk.Core.Auth.AppRoles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                logger.LogInformation("Seeding role {Role}", role);
                await roleManager.CreateAsync(new AppRole(role));
            }
        }
    }

    private static async Task SeedAdminAsync(IServiceProvider sp, ILogger logger, CancellationToken ct)
    {
        var config = sp.GetRequiredService<IConfiguration>();
        var db = sp.GetRequiredService<AppDbContext>();
        var hasher = sp.GetRequiredService<IPasswordHasher<AppUser>>();
        var lookupNormalizer = sp.GetRequiredService<ILookupNormalizer>();

        var email = config["Seed:AdminEmail"] ?? "bruno.miguel.martins.lopes@gmail.com";
        var password = config["Seed:AdminPassword"] ?? DefaultAdminPassword;
        var displayName = config["Seed:AdminDisplayName"] ?? "Bruno Lopes";

        var normalizedEmail = lookupNormalizer.NormalizeEmail(email);
        var existing = await db.Users.IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail, ct);

        if (existing is not null)
        {
            if (PasswordMatches(hasher, existing, DefaultAdminPassword))
            {
                if (!existing.RequireChangePasswordOnNextLogin)
                {
                    existing.RequireChangePasswordOnNextLogin = true;
                    await db.SaveChangesAsync(ct);
                }
                logger.LogWarning("Admin user already exists with DEFAULT seed password — change it.");
            }
            return;
        }

        logger.LogInformation("Seeding admin user {Email}", email);

        var adminRole = await db.Roles.IgnoreQueryFilters()
            .FirstAsync(r => r.NormalizedName == lookupNormalizer.NormalizeName(nameof(UserRole.Admin)), ct);

        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            NormalizedUserName = lookupNormalizer.NormalizeName(email),
            Email = email,
            NormalizedEmail = normalizedEmail,
            EmailConfirmed = true,
            DisplayName = displayName,
            TenantId = LopesTechTenantId,
            IsActive = true,
            RequireChangePasswordOnNextLogin = password == DefaultAdminPassword,
            SecurityStamp = Guid.NewGuid().ToString("N"),
            ConcurrencyStamp = Guid.NewGuid().ToString("N")
        };
        user.PasswordHash = hasher.HashPassword(user, password);

        db.Users.Add(user);
        db.UserRoles.Add(new IdentityUserRole<Guid> { UserId = user.Id, RoleId = adminRole.Id });
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Admin user {Email} seeded with role Admin.", email);

        if (password == DefaultAdminPassword)
            logger.LogWarning("Admin created with default seed password. Set Seed:AdminPassword env to override before exposing the app.");
    }

    private static bool PasswordMatches(IPasswordHasher<AppUser> hasher, AppUser user, string password)
        => !string.IsNullOrWhiteSpace(user.PasswordHash)
           && hasher.VerifyHashedPassword(user, user.PasswordHash, password) != PasswordVerificationResult.Failed;
}
