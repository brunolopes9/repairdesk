using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using RepairDesk.API.Backups;
using RepairDesk.API.HostedServices;
using RepairDesk.API.Infrastructure;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.DAL.Persistence;
using RepairDesk.Infrastructure.At;
using RepairDesk.Infrastructure.Storage;
using RepairDesk.Services.Auth;
using RepairDesk.Services.Audit;
using RepairDesk.Services.Billing;
using RepairDesk.Services.Billing.InvoiceXpress;
using RepairDesk.Services.Clientes;
using RepairDesk.Services.Dashboard;
using RepairDesk.Services.Despesas;
using RepairDesk.Services.Documents;
using RepairDesk.Services.Diagnostico;
using RepairDesk.Services.EquipmentFields;
using RepairDesk.Services.Parts;
using RepairDesk.Services.Push;
using RepairDesk.Services.PublicPortal;
using RepairDesk.Services.Reparacoes;
using RepairDesk.Services.Relatorios;
using RepairDesk.Services.TenantSettings;
using RepairDesk.Services.Trabalhos;
using RepairDesk.Services.Vendas;
using Serilog;

#pragma warning disable CA1852 // Program class is referenced by Mvc.Testing

Log.Logger = RepairDeskSerilog.CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog(RepairDeskSerilog.Configure);

    builder.Services.AddHttpContextAccessor();

    // DataProtection: persistir keys em volume montado para sobreviverem a rebuilds do container.
    // Sem isto, cada rebuild gera novas keys e os secrets cifrados em DB (tokens Moloni, etc)
    // tornam-se ilegiveis (CryptographicException: key was not found in the key ring).
    var dpKeysPath = builder.Configuration["DataProtection:KeysPath"] ?? "/data/dp-keys";
    if (!builder.Environment.IsEnvironment("Testing"))
    {
        try { Directory.CreateDirectory(dpKeysPath); } catch { /* ignore — bind mount tratara */ }
        builder.Services.AddDataProtection()
            .PersistKeysToFileSystem(new DirectoryInfo(dpKeysPath))
            .SetApplicationName("RepairDesk");
    }
    else
    {
        builder.Services.AddDataProtection().SetApplicationName("RepairDesk");
    }

    builder.Services.AddSingleton(TimeProvider.System);
    builder.Services.Configure<MetricsOptions>(builder.Configuration.GetSection(MetricsOptions.SectionName));
    builder.Services.Configure<BackupOptions>(builder.Configuration.GetSection(BackupOptions.SectionName));
    builder.Services.Configure<AtNifLookupOptions>(builder.Configuration.GetSection(AtNifLookupOptions.SectionName));
    builder.Services.Configure<PushOptions>(builder.Configuration.GetSection(PushOptions.SectionName));
    builder.Services.AddScoped<ITenantContext, HttpTenantContext>();
    builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();

    var redisConnection = builder.Configuration["Redis:Connection"];
    if (!builder.Environment.IsEnvironment("Testing") && !string.IsNullOrWhiteSpace(redisConnection))
    {
        builder.Services.AddStackExchangeRedisCache(o =>
        {
            o.Configuration = redisConnection;
            o.InstanceName = "repairdesk:";
        });
    }
    else
    {
        builder.Services.AddDistributedMemoryCache();
    }

    if (!builder.Environment.IsEnvironment("Testing"))
    {
        var connStr = builder.Configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("ConnectionStrings:Default not configured.");

        builder.Services.AddDbContext<AppDbContext>(opt =>
            opt.UseSqlServer(connStr, sql =>
                sql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName)));
    }

    // Identity
    builder.Services
        .AddIdentityCore<AppUser>(o =>
        {
            o.Password.RequiredLength = 8;
            o.Password.RequireDigit = true;
            o.Password.RequireLowercase = true;
            o.Password.RequireUppercase = true;
            o.Password.RequireNonAlphanumeric = false;
            o.User.RequireUniqueEmail = true;
            o.Lockout.MaxFailedAccessAttempts = 5;
            o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        })
        .AddRoles<AppRole>()
        .AddEntityFrameworkStores<AppDbContext>()
        .AddDefaultTokenProviders();

    // JWT auth — TokenValidationParameters configured lazily via IOptions<JwtOptions>
    builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
    builder.Services.AddSingleton<Microsoft.Extensions.Options.IConfigureOptions<JwtBearerOptions>, ConfigureJwtBearerOptions>();

    // Sprint 71: PolicyScheme "Multi" decide entre JWT (utilizadores) e ApiKey (integrações servidor-a-servidor).
    builder.Services
        .AddAuthentication("Multi")
        .AddJwtBearer()
        .AddScheme<RepairDesk.API.Infrastructure.ApiKeyAuthSchemeOptions, RepairDesk.API.Infrastructure.ApiKeyAuthHandler>(
            RepairDesk.API.Infrastructure.ApiKeyAuthHandler.SchemeName, _ => { })
        .AddPolicyScheme("Multi", "Multi (JWT + ApiKey)", options =>
        {
            options.ForwardDefaultSelector = ctx =>
            {
                var auth = ctx.Request.Headers["Authorization"].ToString();
                if (auth.StartsWith("ApiKey ", StringComparison.OrdinalIgnoreCase)) return RepairDesk.API.Infrastructure.ApiKeyAuthHandler.SchemeName;
                if (ctx.Request.Headers.ContainsKey("X-Api-Key")) return RepairDesk.API.Infrastructure.ApiKeyAuthHandler.SchemeName;
                return JwtBearerDefaults.AuthenticationScheme;
            };
        });

    builder.Services.AddAuthorization();

    builder.Services.AddScoped<ITokenService, JwtTokenService>();
    builder.Services.AddScoped<IRefreshTokenService, RefreshTokenService>();
    builder.Services.AddScoped<IRefreshTokenStore, RefreshTokenStore>();
    builder.Services.AddScoped<IAuditLogger, EfAuditLogger>();
    builder.Services.AddScoped<IAuditRepository, AuditRepository>();
    builder.Services.AddScoped<IAuditService, AuditService>();
    builder.Services.AddScoped<IAtNifLookupService, AtNifLookupService>();
    builder.Services.AddSingleton<IAtNifRemoteClient, AtDadosToiSoapClient>();
    builder.Services.AddSingleton<ISecretProtector, DataProtectionSecretProtector>();

    // Clientes
    builder.Services.AddScoped<IClienteRepository, ClienteRepository>();
    builder.Services.AddScoped<IClienteRgpdRepository, ClienteRgpdRepository>();
    builder.Services.AddScoped<IClienteService, ClienteService>();
    builder.Services.AddScoped<IClienteRgpdService, ClienteRgpdService>();
    builder.Services.AddScoped<FluentValidation.IValidator<CreateClienteRequest>, CreateClienteValidator>();
    builder.Services.AddScoped<FluentValidation.IValidator<UpdateClienteRequest>, UpdateClienteValidator>();

    // Reparações
    builder.Services.AddScoped<IReparacaoRepository, ReparacaoRepository>();
    builder.Services.AddScoped<IEquipmentFieldRepository, EquipmentFieldRepository>();
    builder.Services.AddScoped<IEquipmentFieldService, EquipmentFieldService>();
    builder.Services.AddScoped<IReparacaoService, ReparacaoService>();
    builder.Services.AddScoped<FluentValidation.IValidator<CreateReparacaoRequest>, CreateReparacaoValidator>();
    builder.Services.AddScoped<FluentValidation.IValidator<UpdateReparacaoRequest>, UpdateReparacaoValidator>();
    builder.Services.AddScoped<FluentValidation.IValidator<ChangeEstadoRequest>, ChangeEstadoValidator>();

    // Trabalhos
    builder.Services.AddScoped<ITrabalhoRepository, TrabalhoRepository>();
    builder.Services.AddScoped<ITrabalhoService, TrabalhoService>();
    builder.Services.AddScoped<FluentValidation.IValidator<CreateTrabalhoRequest>, CreateTrabalhoValidator>();
    builder.Services.AddScoped<FluentValidation.IValidator<UpdateTrabalhoRequest>, UpdateTrabalhoValidator>();

    // Despesas
    builder.Services.AddScoped<IDespesaRepository, DespesaRepository>();
    builder.Services.AddScoped<IDespesaService, DespesaService>();
    builder.Services.AddScoped<FluentValidation.IValidator<CreateDespesaRequest>, CreateDespesaValidator>();
    builder.Services.AddScoped<FluentValidation.IValidator<UpdateDespesaRequest>, UpdateDespesaValidator>();

    // Dashboard
    builder.Services.AddScoped<IDashboardRepository, DashboardRepository>();
    builder.Services.AddScoped<IDashboardService, DashboardService>();

    // Vendas / POS
    builder.Services.AddScoped<IVendaRepository, VendaRepository>();
    builder.Services.AddScoped<IVendaService, VendaService>();

    // Relatorios fiscais
    builder.Services.AddScoped<IRelatorioFiscalRepository, RelatorioFiscalRepository>();
    builder.Services.AddScoped<IRelatorioFiscalService, RelatorioFiscalService>();

    // Documents (PDF orçamento)
    QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
    builder.Services.AddScoped<ITenantRepository, TenantRepository>();
    builder.Services.AddScoped<IOrcamentoPdfService, OrcamentoPdfService>();
    builder.Services.AddScoped<IVendaPdfService, VendaPdfService>();

    // Tenant settings
    builder.Services.AddScoped<ITenantSettingsService, TenantSettingsService>();
    builder.Services.AddScoped<ITenantBillingSettingsRepository, TenantBillingSettingsRepository>();
    builder.Services.AddScoped<ITenantBillingSettingsService, TenantBillingSettingsService>();
    builder.Services.AddScoped<MoloniBillingProvider>();
    builder.Services.AddScoped<InvoiceXpressBillingProvider>();
    builder.Services.AddScoped<BillingProviderFactory>();
    builder.Services.AddScoped<IBillingProvider, TenantBillingProvider>();
    if (builder.Configuration.GetValue("E2E:UseMoloniStub", false))
        builder.Services.AddSingleton<IMoloniClient, E2eMoloniClient>();
    else
        builder.Services.AddHttpClient<IMoloniClient, MoloniClient>();
    builder.Services.AddHttpClient<IInvoiceXpressClient, InvoiceXpressClient>();

    // Public portal (anonymous, rate-limited)
    builder.Services.AddScoped<IPublicPortalService, PublicPortalService>();
    builder.Services.AddScoped<IPushSubscriptionRepository, PushSubscriptionRepository>();
    builder.Services.AddScoped<ISystemSettingRepository, SystemSettingRepository>();
    builder.Services.AddScoped<IVapidKeyProvider, VapidKeyProvider>();
    builder.Services.AddScoped<IPushNotificationService, PushNotificationService>();
    builder.Services.AddSingleton<IPushNotificationQueue, PushNotificationQueue>();
    builder.Services.AddSingleton<IWebPushSender, WebPushSender>();
    if (!builder.Environment.IsEnvironment("Testing"))
    {
        builder.Services.AddHostedService<PushNotificationWorker>();
        builder.Services.AddHostedService<PushSubscriptionCleanupWorker>();
    }

    // Diagnóstico guiado + Health Score
    builder.Services.AddScoped<IDiagnosticoRepository, RepairDesk.DAL.Persistence.DiagnosticoRepository>();
    builder.Services.AddScoped<IDiagnosticoService, DiagnosticoService>();

    // Garantia + Avaliações
    builder.Services.AddScoped<IGarantiaRepository, RepairDesk.DAL.Persistence.GarantiaRepository>();
    builder.Services.AddScoped<RepairDesk.Services.Garantias.IGarantiaService, RepairDesk.Services.Garantias.GarantiaService>();
    builder.Services.AddScoped<IAvaliacaoRepository, RepairDesk.DAL.Persistence.AvaliacaoRepository>();

    // Service API keys (Sprint 71)
    builder.Services.AddScoped<IServiceApiKeyRepository, RepairDesk.DAL.Persistence.ServiceApiKeyRepository>();
    builder.Services.AddScoped<RepairDesk.Services.ServiceApiKeys.IServiceApiKeyService, RepairDesk.Services.ServiceApiKeys.ServiceApiKeyService>();

    // Webhook subscriptions (Sprint 101) + delivery infra (Sprint 102)
    builder.Services.AddScoped<IWebhookSubscriptionRepository, RepairDesk.DAL.Persistence.WebhookSubscriptionRepository>();
    builder.Services.AddScoped<RepairDesk.Services.Webhooks.IWebhookSubscriptionService, RepairDesk.Services.Webhooks.WebhookSubscriptionService>();
    builder.Services.AddScoped<IWebhookDeliveryRepository, RepairDesk.DAL.Persistence.WebhookDeliveryRepository>();
    builder.Services.AddScoped<RepairDesk.Services.Webhooks.IWebhookPublisher, RepairDesk.Services.Webhooks.WebhookPublisher>();
    builder.Services.AddHttpClient("webhook")
        .ConfigureHttpClient(c => c.Timeout = TimeSpan.FromSeconds(20));
    builder.Services.AddHostedService<RepairDesk.API.Webhooks.WebhookDeliveryHostedService>();

    // External checkout (Sprint 73) — atómico para loja online / integrações
    builder.Services.AddScoped<RepairDesk.Services.External.IExternalCheckoutService, RepairDesk.Services.External.ExternalCheckoutService>();

    // Tabela de preços
    builder.Services.AddScoped<IPriceTableRepository, RepairDesk.DAL.Persistence.PriceTableRepository>();
    builder.Services.AddScoped<RepairDesk.Services.PriceTable.IPriceTableService, RepairDesk.Services.PriceTable.PriceTableService>();

    // Stock de peças
    builder.Services.AddScoped<IPartRepository, PartRepository>();
    builder.Services.AddScoped<IPartService, PartService>();
    builder.Services.AddScoped<FluentValidation.IValidator<CreatePartRequest>, CreatePartValidator>();
    builder.Services.AddScoped<FluentValidation.IValidator<UpdatePartRequest>, UpdatePartValidator>();
    builder.Services.AddScoped<FluentValidation.IValidator<CreatePartMovimentoRequest>, CreatePartMovimentoValidator>();

    // Fotos (storage abstracto — default: local filesystem)
    builder.Services.AddScoped<IReparacaoFotoRepository, RepairDesk.DAL.Persistence.ReparacaoFotoRepository>();
    var storageProvider = builder.Configuration["Storage:Provider"]?.Trim().ToLowerInvariant() ?? "local";
    switch (storageProvider)
    {
        case "local":
            builder.Services.AddSingleton<IPhotoStorage, LocalFileSystemPhotoStorage>();
            break;
        case "r2":
            builder.Services.AddSingleton<IPhotoStorage, CloudflareR2PhotoStorage>();
            break;
        default:
            throw new InvalidOperationException("Storage:Provider must be 'local' or 'r2'.");
    }
    builder.Services.AddScoped<RepairDesk.Services.Fotos.IFotoService, RepairDesk.Services.Fotos.FotoService>();
    builder.Services.AddScoped<RepairDesk.Services.Fotos.IPhotoExportLinkService, RepairDesk.Services.Fotos.PhotoExportLinkService>();

    // Backups (scheduler only registers when enabled; admin endpoint remains available)
    builder.Services.AddSingleton<IBackupFileSystem, BackupFileSystem>();
    builder.Services.AddSingleton<ISqlServerBackupExecutor, SqlServerBackupExecutor>();
    builder.Services.AddSingleton<IBackupRemoteStorage, R2BackupStorage>();
    builder.Services.AddSingleton<IBackupService, BackupService>();
    if (builder.Configuration.GetValue("Backup:Enabled", false))
        builder.Services.AddHostedService<BackupHostedService>();

    builder.Services.AddHealthChecks()
        .AddCheck("self", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy("API process is alive."), tags: ["live"])
        .AddCheck<RepairDeskDbHealthCheck>("db", tags: ["ready", "db"])
        .AddCheck<PhotoStorageHealthCheck>("storage", tags: ["ready", "storage"])
        .AddCheck<BackupHealthCheck>("backup", tags: ["backup"]);

    // Rate limiting (login: 5 per 15 min per IP). Disabled in Testing/E2E envs.
    var isTesting = builder.Environment.IsEnvironment("Testing");
    var disableRateLimits = isTesting || builder.Configuration.GetValue("E2E:Enabled", false);
    builder.Services.AddRateLimiter(opt =>
    {
        opt.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        opt.AddPolicy("login", ctx =>
        {
            if (disableRateLimits)
                return RateLimitPartition.GetNoLimiter("login");
            var key = ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(15),
                QueueLimit = 0,
                AutoReplenishment = true
            });
        });
        opt.AddPolicy("public-portal", ctx =>
        {
            if (disableRateLimits)
                return RateLimitPartition.GetNoLimiter("public-portal");
            var key = ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 30,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            });
        });
        // Sprint 80: rate limit por API key (não por IP — lojas Vercel partilham egress).
        // 120 req/min por chave: generoso para webhook bursts + paginação catálogo,
        // suficiente para impedir runaway loop de uma integração com bug.
        opt.AddPolicy("external-apikey", ctx =>
        {
            if (disableRateLimits)
                return RateLimitPartition.GetNoLimiter("external-apikey");
            // Particiona pelo claim service_api_key_id (preenchido pelo ApiKeyAuthHandler).
            // Sem chave válida, particiona por IP (defensa em profundidade — request rejeitado
            // depois pelo auth, mas evita brute-force scanning).
            var keyId = ctx.User.FindFirst("service_api_key_id")?.Value;
            var partition = keyId ?? $"ip:{ctx.Connection.RemoteIpAddress}";
            return RateLimitPartition.GetFixedWindowLimiter(partition, _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 120,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true,
            });
        });
    });

    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new() { Title = "RepairDesk API", Version = "v1" });
    });

    builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
        .WithOrigins("http://localhost:5173", "http://localhost:3000")
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials()));

    var app = builder.Build();

    app.UseMiddleware<CorrelationIdMiddleware>();
    app.UseSerilogRequestLogging(options =>
    {
        options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
        {
            if (httpContext.Items.TryGetValue(CorrelationIdMiddleware.ItemName, out var correlationId))
                diagnosticContext.Set("CorrelationId", correlationId);

            var tenantId = httpContext.User.FindFirst(HttpTenantContext.TenantIdClaim)?.Value;
            if (!string.IsNullOrWhiteSpace(tenantId))
                diagnosticContext.Set("TenantId", tenantId);
        };
    });
    app.UseMiddleware<ProblemDetailsExceptionMiddleware>();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseCors();
    app.UseRateLimiter();
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapHealthChecks("/api/health/live", HealthCheckJsonResponseWriter.OptionsForTag("live", forceHealthyStatusCode: true));
    app.MapHealthChecks("/api/health/ready", HealthCheckJsonResponseWriter.OptionsForTag("ready"));
    app.MapHealthChecks("/api/health/db", HealthCheckJsonResponseWriter.OptionsForTag("db"));
    app.MapHealthChecks("/api/health/storage", HealthCheckJsonResponseWriter.OptionsForTag("storage"));
    app.MapHealthChecks("/api/health/backup", HealthCheckJsonResponseWriter.OptionsForTag("backup"));
    MetricsEndpoint.Map(app);
    app.MapControllers();

    if (!app.Configuration.GetValue("Database:SkipAutoMigrate", false))
    {
        await DbInitializer.InitializeAsync(app.Services);
    }

    Log.Information("RepairDesk API starting");
    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "RepairDesk API host terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program;
