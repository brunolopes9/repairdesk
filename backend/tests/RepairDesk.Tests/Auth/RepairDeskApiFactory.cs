using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RepairDesk.Core.Entities;
using RepairDesk.DAL.Persistence;

namespace RepairDesk.Tests.Auth;

public class RepairDeskApiFactory : WebApplicationFactory<Program>
{
    public static readonly Guid TenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    public static readonly Guid SecondTenantId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    public const string AdminEmail = "admin@test.local";
    public const string AdminPassword = "Test!Pass2026";
    public const string AdminDisplayName = "Test Admin";
    public const string SecondAdminEmail = "admin-b@test.local";

    private readonly string _dbName = $"rd-test-{Guid.NewGuid():N}";

    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, cfg) =>
        {
            cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = "Server=ignored;Database=ignored;",
                ["Database:SkipAutoMigrate"] = "true",
                ["Jwt:Issuer"] = "rd-test",
                ["Jwt:Audience"] = "rd-test",
                ["Jwt:SigningKey"] = "test-signing-key-with-at-least-32-chars-and-some-padding",
                ["Jwt:AccessTokenMinutes"] = "15",
                ["Jwt:RefreshTokenDays"] = "7",
                ["Seed:AdminEmail"] = AdminEmail,
                ["Seed:AdminPassword"] = AdminPassword,
                ["Seed:AdminDisplayName"] = AdminDisplayName,
            });
        });

        builder.ConfigureServices(services =>
        {
            var dbOptions = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (dbOptions is not null) services.Remove(dbOptions);
            var dbCtx = services.SingleOrDefault(d => d.ServiceType == typeof(AppDbContext));
            if (dbCtx is not null) services.Remove(dbCtx);

            services.AddDbContext<AppDbContext>(opt => opt.UseInMemoryDatabase(_dbName));
        });

        var host = base.CreateHost(builder);
        SeedAsync(host.Services).GetAwaiter().GetResult();
        return host;
    }

    private static async Task SeedAsync(IServiceProvider sp)
    {
        using var scope = sp.CreateScope();
        var s = scope.ServiceProvider;
        var db = s.GetRequiredService<AppDbContext>();
        await db.Database.EnsureCreatedAsync();

        var hasher = s.GetRequiredService<IPasswordHasher<AppUser>>();
        var normalizer = s.GetRequiredService<ILookupNormalizer>();

        if (!await db.Tenants.IgnoreQueryFilters().AnyAsync(t => t.Id == TenantId))
        {
            db.Tenants.Add(new Tenant
            {
                Id = TenantId,
                Name = "Test Tenant",
                LegalName = "Test",
                IsActive = true
            });
        }

        if (!await db.Tenants.IgnoreQueryFilters().AnyAsync(t => t.Id == SecondTenantId))
        {
            db.Tenants.Add(new Tenant
            {
                Id = SecondTenantId,
                Name = "Second Tenant",
                LegalName = "Second",
                IsActive = true
            });
        }

        var roleName = "Admin";
        var role = await db.Roles.IgnoreQueryFilters().FirstOrDefaultAsync(r => r.Name == roleName);
        if (role is null)
        {
            role = new AppRole(roleName)
            {
                Id = Guid.NewGuid(),
                NormalizedName = normalizer.NormalizeName(roleName)
            };
            db.Roles.Add(role);
        }

        await EnsureUserAsync(db, hasher, normalizer, AdminEmail, AdminDisplayName, AdminPassword, TenantId, role);
        await EnsureUserAsync(db, hasher, normalizer, SecondAdminEmail, "Tenant B Admin", AdminPassword, SecondTenantId, role);

        await db.SaveChangesAsync();
    }

    private static async Task EnsureUserAsync(
        AppDbContext db,
        IPasswordHasher<AppUser> hasher,
        ILookupNormalizer normalizer,
        string email,
        string displayName,
        string password,
        Guid tenantId,
        AppRole role)
    {
        var normalizedEmail = normalizer.NormalizeEmail(email);
        var existing = await db.Users.IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail);
        if (existing is not null) return;

        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            NormalizedUserName = normalizer.NormalizeName(email),
            Email = email,
            NormalizedEmail = normalizedEmail,
            EmailConfirmed = true,
            DisplayName = displayName,
            TenantId = tenantId,
            IsActive = true,
            SecurityStamp = Guid.NewGuid().ToString("N"),
            ConcurrencyStamp = Guid.NewGuid().ToString("N")
        };
        user.PasswordHash = hasher.HashPassword(user, password);
        db.Users.Add(user);
        db.UserRoles.Add(new IdentityUserRole<Guid> { UserId = user.Id, RoleId = role.Id });
    }
}
