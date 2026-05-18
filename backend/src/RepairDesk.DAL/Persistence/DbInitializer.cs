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
        await BackfillPublicSlugsAsync(db, logger, ct);
        await SeedDiagnosticoTemplatesAsync(db, logger, ct);
        await SeedEquipmentFieldTemplatesAsync(db, logger, ct);
    }

    private static async Task SeedEquipmentFieldTemplatesAsync(AppDbContext db, ILogger logger, CancellationToken ct)
    {
        if (db.Database.ProviderName?.Contains("InMemory", StringComparison.OrdinalIgnoreCase) == true) return;
        if (await db.EquipmentFieldTemplates.IgnoreQueryFilters().AnyAsync(t => t.TenantId == LopesTechTenantId, ct)) return;

        logger.LogInformation("Seeding default equipment field templates for LopesTech...");

        EquipmentFieldTemplate Build(string nome, DeviceCategory categoria, int ordem, params (string label, bool visible)[] fields)
        {
            var template = new EquipmentFieldTemplate
            {
                TenantId = LopesTechTenantId,
                Nome = nome,
                Categoria = categoria,
                IsActive = true,
                Ordem = ordem,
            };
            for (var i = 0; i < fields.Length; i++)
            {
                var (label, visible) = fields[i];
                template.Fields.Add(new EquipmentFieldDefinition
                {
                    TenantId = LopesTechTenantId,
                    Label = label,
                    Type = EquipmentFieldType.Text,
                    Required = false,
                    Ordem = i,
                    VisibleInPortal = visible,
                });
            }
            return template;
        }

        db.EquipmentFieldTemplates.AddRange(
            Build("Telemóvel", DeviceCategory.Smartphone, 0, ("IMEI", false), ("Marca", true), ("Modelo", true)),
            Build("Laptop", DeviceCategory.Laptop, 1, ("Marca", true), ("Modelo", true), ("CPU", true), ("RAM", true), ("Storage", true), ("GPU", true)),
            Build("Desktop", DeviceCategory.Desktop, 2, ("Marca", true), ("Modelo", true), ("CPU", true), ("RAM", true), ("Storage", true), ("GPU", true), ("MotherBoard", true)));
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Equipment field templates seeded.");
    }

    private static async Task SeedDiagnosticoTemplatesAsync(AppDbContext db, ILogger logger, CancellationToken ct)
    {
        if (db.Database.ProviderName?.Contains("InMemory", StringComparison.OrdinalIgnoreCase) == true) return;
        if (await db.DiagnosticoTemplates.IgnoreQueryFilters().AnyAsync(t => t.TenantId == LopesTechTenantId, ct)) return;

        logger.LogInformation("Seeding default diagnóstico templates for LopesTech...");

        DiagnosticoTemplate Build(string nome, DeviceCategory cat, (string label, string? grupo, int peso)[] itens)
        {
            var t = new DiagnosticoTemplate
            {
                TenantId = LopesTechTenantId,
                Nome = nome,
                Categoria = cat,
                IsDefault = true,
                Activo = true,
            };
            for (int i = 0; i < itens.Length; i++)
            {
                var (label, grupo, peso) = itens[i];
                t.Items.Add(new DiagnosticoTemplateItem
                {
                    TenantId = LopesTechTenantId,
                    Label = label,
                    Grupo = grupo,
                    Ordem = i,
                    Peso = peso,
                });
            }
            return t;
        }

        var smartphone = Build("Smartphone padrão", DeviceCategory.Smartphone, new (string, string?, int)[]
        {
            ("Ecrã sem fissuras ou pixeis mortos", "Ecrã", 8),
            ("Touch responsivo (toda a área)", "Ecrã", 8),
            ("Botão Power", "Botões", 5),
            ("Botões de volume", "Botões", 4),
            ("Botão Home / gestos", "Botões", 4),
            ("Câmara traseira (foco e foto)", "Câmaras", 6),
            ("Câmara frontal (selfie)", "Câmaras", 5),
            ("Altifalante principal", "Áudio", 6),
            ("Altifalante de chamada (ear-piece)", "Áudio", 6),
            ("Microfone (chamada + gravação)", "Áudio", 6),
            ("Wi-Fi conecta e mantém", "Conectividade", 7),
            ("Dados móveis / sinal SIM", "Conectividade", 7),
            ("Bluetooth conecta", "Conectividade", 4),
            ("Sensor de proximidade (ecrã apaga em chamada)", "Sensores", 4),
            ("Vibração", "Sensores", 3),
            ("Conector de carga + cabo original", "Energia", 7),
            ("Bateria mantém carga > 80%", "Energia", 7),
            ("Vidro traseiro sem danos", "Estrutura", 5),
            ("Frame/aros sem amolgadelas significativas", "Estrutura", 4),
            ("Resistência à água (selante intacto)", "Estrutura", 3),
        });

        var tablet = Build("Tablet padrão", DeviceCategory.Tablet, new (string, string?, int)[]
        {
            ("Ecrã sem fissuras ou pixeis mortos", "Ecrã", 8),
            ("Touch responsivo", "Ecrã", 8),
            ("Botão Power", "Botões", 5),
            ("Botões de volume", "Botões", 4),
            ("Câmara traseira", "Câmaras", 4),
            ("Câmara frontal", "Câmaras", 4),
            ("Altifalantes", "Áudio", 5),
            ("Microfone", "Áudio", 4),
            ("Wi-Fi", "Conectividade", 7),
            ("Bluetooth", "Conectividade", 4),
            ("Conector de carga + cabo", "Energia", 7),
            ("Bateria mantém carga > 80%", "Energia", 7),
            ("Vidro traseiro / frame", "Estrutura", 4),
            ("Apple Pencil / S Pen (se aplicável)", "Acessórios", 3),
        });

        var laptop = Build("Computador portátil padrão", DeviceCategory.Laptop, new (string, string?, int)[]
        {
            ("Liga e arranca sistema operativo", "Geral", 10),
            ("Ecrã sem fissuras / linhas / pixeis mortos", "Ecrã", 8),
            ("Brilho do ecrã funciona", "Ecrã", 4),
            ("Teclado — todas as teclas", "Teclado", 7),
            ("Touchpad / clique esquerdo e direito", "Teclado", 6),
            ("Webcam funciona", "Câmaras", 4),
            ("Altifalantes", "Áudio", 5),
            ("Microfone", "Áudio", 4),
            ("Wi-Fi conecta e mantém", "Conectividade", 7),
            ("Bluetooth", "Conectividade", 4),
            ("Portas USB (testar todas)", "Portas", 5),
            ("Porta HDMI/Display", "Portas", 4),
            ("Carregador original / conector funciona", "Energia", 7),
            ("Bateria — autonomia > 1h", "Energia", 6),
            ("Ventoinha / temperatura sob controlo", "Térmico", 5),
            ("SSD/HDD reconhecido + saúde", "Storage", 7),
            ("Quantidade de RAM disponível conforme spec", "Memória", 4),
            ("Sem corrupção visível de ficheiros / arranque", "Software", 5),
        });

        var desktop = Build("Computador desktop padrão", DeviceCategory.Desktop, new (string, string?, int)[]
        {
            ("Liga e arranca sistema operativo", "Geral", 10),
            ("Sinal de vídeo (testar portas)", "Vídeo", 8),
            ("Teclado e rato funcionam", "Periféricos", 5),
            ("Wi-Fi / Ethernet", "Conectividade", 7),
            ("Portas USB frente/trás", "Portas", 5),
            ("Áudio output (auscultadores/colunas)", "Áudio", 4),
            ("Áudio input (microfone)", "Áudio", 3),
            ("SSD/HDD reconhecido + saúde", "Storage", 7),
            ("Quantidade de RAM disponível", "Memória", 5),
            ("Ventoinhas + temperatura sob controlo", "Térmico", 6),
            ("Sem corrupção / arranque limpo", "Software", 5),
        });

        var smartwatch = Build("Smartwatch padrão", DeviceCategory.Smartwatch, new (string, string?, int)[]
        {
            ("Ecrã sem fissuras", "Ecrã", 8),
            ("Touch responsivo", "Ecrã", 8),
            ("Botão lateral / Crown", "Botões", 5),
            ("Sensor cardíaco / SpO2", "Sensores", 5),
            ("Wi-Fi / Bluetooth", "Conectividade", 6),
            ("Carregamento (base/cabo original)", "Energia", 7),
            ("Bateria mantém carga > 80%", "Energia", 6),
            ("Resistência à água (selante)", "Estrutura", 4),
            ("Bracelete em boas condições", "Acessórios", 2),
        });

        db.DiagnosticoTemplates.AddRange(smartphone, tablet, laptop, desktop, smartwatch);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Diagnóstico templates seeded.");
    }

    /// <summary>Backfill PublicSlug em reparações criadas antes da migration Sprint16.</summary>
    private static async Task BackfillPublicSlugsAsync(AppDbContext db, ILogger logger, CancellationToken ct)
    {
        if (db.Database.ProviderName?.Contains("InMemory", StringComparison.OrdinalIgnoreCase) == true) return;
        var semSlug = await db.Reparacoes
            .IgnoreQueryFilters()
            .Where(r => r.PublicSlug == null)
            .ToListAsync(ct);
        if (semSlug.Count == 0) return;
        foreach (var r in semSlug)
        {
            r.PublicSlug = RepairDesk.Common.Helpers.PublicSlugGenerator.New();
        }
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Atribuídos PublicSlug a {Count} reparações existentes", semSlug.Count);
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
        foreach (var role in Enum.GetNames<UserRole>())
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
            if (password == DefaultAdminPassword)
                logger.LogWarning("Admin user already exists with DEFAULT seed password — change it.");
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
}
