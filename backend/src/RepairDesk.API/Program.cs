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
using RepairDesk.Services.TenantPreferences;
using RepairDesk.Services.Trabalhos;
using RepairDesk.Services.Vendas;
using Serilog;

#pragma warning disable CA1852 // Program class is referenced by Mvc.Testing

Log.Logger = RepairDeskSerilog.CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Sprint 250 (Doc 75 área 11 P0): Sentry para captura de exceptions não-tratadas.
    // Env-gated — sem SENTRY_DSN o SDK é no-op e não envia nada. Bruno mete o DSN
    // como secret no docker-compose.prod.yml (Sentry__Dsn) quando a infra estiver pronta.
    var sentryDsn = builder.Configuration["Sentry:Dsn"];
    if (!string.IsNullOrWhiteSpace(sentryDsn))
    {
        builder.WebHost.UseSentry(o =>
        {
            o.Dsn = sentryDsn;
            o.Environment = builder.Environment.EnvironmentName;
            o.TracesSampleRate = builder.Environment.IsProduction() ? 0.1 : 0.0;
            // Não envia HTTP request body por defeito (PII risk). Bruno active manualmente
            // via env var se precisar de debug profundo.
            o.MaxRequestBodySize = Sentry.Extensibility.RequestSize.None;
            o.SendDefaultPii = false;
            o.AttachStacktrace = true;
            o.Release = typeof(Program).Assembly.GetName().Version?.ToString();
        });
    }

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
    builder.Services.Configure<RefreshTokenCleanupOptions>(builder.Configuration.GetSection(RefreshTokenCleanupOptions.SectionName));
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

    builder.Services.AddAuthorization(options =>
    {
        // Sprint 111: policies para scopes de API key. JWT (admin) passa sempre.
        foreach (var scope in RepairDesk.Core.Entities.ServiceApiKeyScopes.All)
        {
            options.AddPolicy($"api_scope:{scope}", policy =>
                policy.Requirements.Add(new RepairDesk.API.Infrastructure.ApiScopeRequirement(scope)));
        }

        // Sprint 311 (Doc 72 Fase D): policies de roles granulares. Controllers existentes
        // com [Authorize(Roles = "Admin")] continuam a funcionar — estas policies são
        // ADITIVAS para refactor incremental.
        options.AddPolicy(RepairDesk.Core.Auth.AppPolicies.RequireAdmin, p =>
            p.RequireAuthenticatedUser().RequireRole(RepairDesk.Core.Auth.AppRoles.Admin));
        options.AddPolicy(RepairDesk.Core.Auth.AppPolicies.RequireTechOrAdmin, p =>
            p.RequireAuthenticatedUser().RequireRole(
                RepairDesk.Core.Auth.AppRoles.Admin,
                RepairDesk.Core.Auth.AppRoles.Tech));
        options.AddPolicy(RepairDesk.Core.Auth.AppPolicies.RequireCashierOrAdmin, p =>
            p.RequireAuthenticatedUser().RequireRole(
                RepairDesk.Core.Auth.AppRoles.Admin,
                RepairDesk.Core.Auth.AppRoles.Cashier));
        options.AddPolicy(RepairDesk.Core.Auth.AppPolicies.RequireWrite, p =>
            p.RequireAuthenticatedUser().RequireRole(
                RepairDesk.Core.Auth.AppRoles.Admin,
                RepairDesk.Core.Auth.AppRoles.Tech,
                RepairDesk.Core.Auth.AppRoles.Cashier));
    });
    builder.Services.AddSingleton<Microsoft.AspNetCore.Authorization.IAuthorizationHandler, RepairDesk.API.Infrastructure.ApiScopeHandler>();

    builder.Services.AddScoped<ITokenService, JwtTokenService>();
    builder.Services.AddScoped<IRefreshTokenService, RefreshTokenService>();
    builder.Services.AddScoped<IRefreshTokenStore, RefreshTokenStore>();
    builder.Services.AddScoped<IAuditLogger, EfAuditLogger>();
    builder.Services.AddScoped<IAuditRepository, AuditRepository>();
    builder.Services.AddScoped<IAuditService, AuditService>();
    builder.Services.AddScoped<IAtNifLookupService, AtNifLookupService>();
    // Sprint 364: lookup de NIF encadeado — AT dadosTOI (cert) → VIES (grátis, empresas).
    builder.Services.AddSingleton<AtDadosToiSoapClient>();
    builder.Services.AddHttpClient<ViesNifRemoteClient>(c =>
    {
        c.BaseAddress = new Uri("https://ec.europa.eu/taxation_customs/vies/");
        c.Timeout = TimeSpan.FromSeconds(12);
    });
    builder.Services.AddScoped<IAtNifRemoteClient, CompositeNifRemoteClient>();
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
    builder.Services.AddScoped<IDashboardKpiHojeService, DashboardKpiHojeService>();

    // Vendas / POS
    builder.Services.AddScoped<IVendaRepository, VendaRepository>();
    builder.Services.AddScoped<IVendaService, VendaService>();

    // Relatorios fiscais
    builder.Services.AddScoped<IRelatorioFiscalRepository, RelatorioFiscalRepository>();
    builder.Services.AddScoped<IRelatorioFiscalService, RelatorioFiscalService>();
    builder.Services.AddScoped<IRelatorioNegocioRepository, RelatorioNegocioRepository>();
    builder.Services.AddScoped<IRelatorioNegocioService, RelatorioNegocioService>();

    // Documents (PDF orçamento)
    QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
    builder.Services.AddScoped<ITenantRepository, TenantRepository>();
    builder.Services.AddScoped<ITenantPreferencesRepository, TenantPreferencesRepository>();
    builder.Services.AddScoped<ITenantPreferencesService, TenantPreferencesService>();
    builder.Services.AddScoped<IWhatsAppNotificationLogRepository, WhatsAppNotificationLogRepository>();
    builder.Services.AddScoped<IOrcamentoPdfService, OrcamentoPdfService>();
    // Sprint 347 (fix Sprint 360): registo do serviço de etiquetas — em falta desde a criação,
    // causava 500 'No service for ILabelPdfService' no GET /label.pdf.
    builder.Services.AddScoped<ILabelPdfService, LabelPdfService>();
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
    // Sprint 366: push de staff (eventos internos: pedido online, venda, stock, reparação parada).
    builder.Services.AddScoped<IStaffPushSubscriptionRepository, StaffPushSubscriptionRepository>();
    builder.Services.AddScoped<IStaffPushService, StaffPushService>();
    builder.Services.AddSingleton<IStaffPushQueue, StaffPushQueue>();
    if (!builder.Environment.IsEnvironment("Testing"))
    {
        builder.Services.AddHostedService<PushNotificationWorker>();
        builder.Services.AddHostedService<PushSubscriptionCleanupWorker>();
        builder.Services.AddHostedService<StaffPushWorker>();
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

    // Fornecedores (Sprint 120)
    builder.Services.AddScoped<IFornecedorRepository, RepairDesk.DAL.Persistence.FornecedorRepository>();
    builder.Services.AddScoped<RepairDesk.Services.Fornecedores.IFornecedorService, RepairDesk.Services.Fornecedores.FornecedorService>();

    // Products (Sprint 122)
    builder.Services.AddScoped<IProductRepository, RepairDesk.DAL.Persistence.ProductRepository>();
    builder.Services.AddScoped<RepairDesk.Services.Products.IProductService, RepairDesk.Services.Products.ProductService>();

    builder.Services.AddScoped<IWebhookDeliveryRepository, RepairDesk.DAL.Persistence.WebhookDeliveryRepository>();
    builder.Services.AddScoped<RepairDesk.Services.Webhooks.IWebhookPublisher, RepairDesk.Services.Webhooks.WebhookPublisher>();
    builder.Services.AddHttpClient("webhook")
        .ConfigureHttpClient(c => c.Timeout = TimeSpan.FromSeconds(20));
    builder.Services.AddHostedService<RepairDesk.API.Webhooks.WebhookDeliveryHostedService>();
    builder.Services.AddHostedService<RepairDesk.API.Webhooks.GarantiaExpirationHostedService>();
    // Sprint 175: retention cleanup diário às 3h UTC.
    builder.Services.AddHostedService<RepairDesk.API.HostedServices.SupplierInvoiceRetentionHostedService>();
    if (!builder.Environment.IsEnvironment("Testing"))
        builder.Services.AddHostedService<RefreshTokenCleanupHostedService>();

    // External checkout (Sprint 73) — atómico para loja online / integrações
    builder.Services.AddScoped<RepairDesk.Services.External.IExternalCheckoutService, RepairDesk.Services.External.ExternalCheckoutService>();

    // Sprint 147: ingest de faturas de fornecedor via n8n IMAP
    builder.Services.AddScoped<ISupplierInvoiceImportRepository, RepairDesk.DAL.Persistence.SupplierInvoiceImportRepository>();
    // Sprint 157: SKU mapping tabela aprendida — fornecedor → Part/Product interno.
    builder.Services.AddScoped<ISkuMappingRepository, RepairDesk.DAL.Persistence.SkuMappingRepository>();
    // Sprint 162: supplier fingerprinting (detect fornecedor antes do parser).
    builder.Services.AddScoped<RepairDesk.Services.Documents.ISupplierFingerprintingService, RepairDesk.Services.Documents.SupplierFingerprintingService>();
    // Sprint 163: LLM fallback parser (Claude Haiku) com PII redaction. Configured via ANTHROPIC_API_KEY env.
    builder.Services.AddHttpClient<RepairDesk.Services.Documents.IAnthropicSupplierParser, RepairDesk.Services.Documents.AnthropicSupplierParser>();
    // Sprint 167a: tracking de uso LLM per-tenant (DB → UI /definicoes/llm-usage).
    builder.Services.AddScoped<ILlmUsageRepository, RepairDesk.DAL.Persistence.LlmUsageRepository>();
    builder.Services.AddScoped<RepairDesk.Services.Documents.ILlmUsageTracker, RepairDesk.Services.Documents.LlmUsageTracker>();
    // Sprint 167b: quota enforcement per-tenant (free/pro/enterprise).
    builder.Services.AddScoped<RepairDesk.Services.Documents.ILlmQuotaService, RepairDesk.Services.Documents.LlmQuotaService>();
    // Sprint 166a: pacote SEO completo (title+description+alt+markdown) gerado por Claude.
    builder.Services.AddHttpClient<RepairDesk.Services.Products.IProductSeoGenerator, RepairDesk.Services.Products.AnthropicAltTextService>();
    // Sprint 203: detector de mapeamento colunas CSV (universal importer com Claude).
    builder.Services.AddHttpClient<RepairDesk.Services.Products.ICsvColumnDetector, RepairDesk.Services.Products.CsvColumnDetectionService>();
    // Sprint 188: Shop AI Bridge — assistant NL + image search via Anthropic central.
    builder.Services.AddHttpClient<RepairDesk.Services.Shop.IShopAiService, RepairDesk.Services.Shop.ShopAiService>();
    // Sprint 369: assistente interno read-only (tool-use sobre dados do tenant).
    builder.Services.AddHttpClient<RepairDesk.API.Assistant.IAssistantService, RepairDesk.API.Assistant.AssistantService>();
    // Sprint 371: agendamentos (booking).
    builder.Services.AddScoped<IAppointmentRepository, RepairDesk.DAL.Persistence.AppointmentRepository>();
    builder.Services.AddScoped<RepairDesk.Services.Appointments.IAppointmentService, RepairDesk.Services.Appointments.AppointmentService>();
    // Sprint 189: pipeline imagens SEO (resize WebP + blur LQIP) — usa IPhotoStorage para R2.
    builder.Services.AddScoped<RepairDesk.Services.Products.IImageOptimizationService, RepairDesk.Services.Products.ImageOptimizationService>();
    builder.Services.AddSingleton<RepairDesk.Services.Documents.ISupplierInvoiceStorage, RepairDesk.Services.Documents.SupplierInvoiceStorage>();
    builder.Services.AddScoped<RepairDesk.Services.Documents.ISupplierInvoiceImportService, RepairDesk.Services.Documents.SupplierInvoiceImportService>();

    // Sprint 344 (Doc 83 Pillar 3): assinaturas digitais ligadas a reparações.
    builder.Services.AddScoped<ISignatureRepository, RepairDesk.DAL.Persistence.SignatureRepository>();
    // Sprint 346 (Doc 83 Pillar 6): tags categóricas para reparações.
    builder.Services.AddScoped<IReparacaoTagRepository, RepairDesk.DAL.Persistence.ReparacaoTagRepository>();
    // Sprint 349 (Doc 83 Pillar 6): time tracker por reparação.
    builder.Services.AddScoped<IReparacaoTimeEntryRepository, RepairDesk.DAL.Persistence.ReparacaoTimeEntryRepository>();
    // Sprint 353 (Doc 83 Pillar 5): kits de peças.
    builder.Services.AddScoped<IPartKitRepository, RepairDesk.DAL.Persistence.PartKitRepository>();
    // Sprint 354 (Doc 83 Pillar 9): pedidos de reparação via widget público.
    builder.Services.AddScoped<IRepairRequestRepository, RepairDesk.DAL.Persistence.RepairRequestRepository>();
    // Sprint 359 (Doc 83): templates de modelo.
    builder.Services.AddScoped<IProductModelRepository, RepairDesk.DAL.Persistence.ProductModelRepository>();

    // Sprint 385 (Doc 87): vista unificada "Catálogo & Stock" (read model Product/ProductModel/Part).
    builder.Services.AddScoped<ICatalogReadRepository, RepairDesk.DAL.Persistence.CatalogReadRepository>();
    builder.Services.AddScoped<RepairDesk.Services.Catalog.ICatalogService, RepairDesk.Services.Catalog.CatalogService>();

    // Sprint 390 (Doc 04): lookup TAC→modelo offline. Base num JSON em disco (mountar volume em prod
    // para sobreviver a redeploys; override via TacDb:Path). Importada pelo admin a partir de dump aberto.
    var tacDbPath = builder.Configuration["TacDb:Path"]
        ?? Path.Combine(builder.Environment.ContentRootPath, "Data", "tac-db.json");
    builder.Services.AddSingleton<RepairDesk.Services.Imei.ITacLookupService>(
        _ => new RepairDesk.Services.Imei.TacLookupService(tacDbPath));

    // Sprint 303: Payments — providers (Mock + IFTHENPAY) + orquestrador.
    builder.Services.AddScoped<IPaymentRepository, RepairDesk.DAL.Persistence.PaymentRepository>();
    builder.Services.AddSingleton<IPaymentProvider, RepairDesk.Services.Payments.MockPaymentProvider>();
    builder.Services.AddScoped<RepairDesk.Services.Payments.IPaymentService, RepairDesk.Services.Payments.PaymentService>();
    // IFTHENPAY: registado apenas quando há pelo menos uma chave configurada. Caso contrário
    // Mock continua a ser o único provider (útil para dev sem credenciais).
    var ifthenpayOptions = new RepairDesk.Services.Payments.Ifthenpay.IfthenpayOptions
    {
        MBWayKey = builder.Configuration["Ifthenpay:MBWayKey"],
        MultibancoKey = builder.Configuration["Ifthenpay:MultibancoKey"],
        AntiPhishingKey = builder.Configuration["Ifthenpay:AntiPhishingKey"],
        BaseUrl = builder.Configuration["Ifthenpay:BaseUrl"] ?? "https://api.ifthenpay.com",
    };
    builder.Services.AddSingleton(ifthenpayOptions);
    if (ifthenpayOptions.IsConfigured)
    {
        builder.Services.AddHttpClient<IPaymentProvider, RepairDesk.Services.Payments.Ifthenpay.IfthenpayProvider>();
    }

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
    // Sprint 246 (Doc 73): validador central de uploads por magic bytes.
    builder.Services.AddSingleton<RepairDesk.Services.Files.IFileValidator, RepairDesk.Services.Files.FileValidator>();

    // Sprint 300 (Doc 80 Pillar A.1): controlo de caixa POS PT.
    builder.Services.AddScoped<RepairDesk.API.Cash.ICashService, RepairDesk.API.Cash.CashService>();
    // Sprint 302 (Doc 80 Pillar A.1): PDF Z-report.
    builder.Services.AddScoped<RepairDesk.API.Cash.IZReportPdfService, RepairDesk.API.Cash.ZReportPdfService>();

    // Backups (scheduler only registers when enabled; admin endpoint remains available)
    builder.Services.AddSingleton<IBackupFileSystem, BackupFileSystem>();
    builder.Services.AddSingleton<ISqlServerBackupExecutor, SqlServerBackupExecutor>();
    builder.Services.AddSingleton<IBackupRemoteStorage, R2BackupStorage>();
    builder.Services.AddSingleton<IBackupService, BackupService>();
    if (builder.Configuration.GetValue("Backup:Enabled", false))
        builder.Services.AddHostedService<BackupHostedService>();

    // Sprint 352 (Doc 76 gap crítico): dp-keys backup off-VPS encriptado.
    var dpBackupOptions = new DpKeysBackupOptions();
    builder.Configuration.GetSection("DpKeysBackup").Bind(dpBackupOptions);
    if (!string.IsNullOrWhiteSpace(builder.Configuration["DpKeysBackup:R2:Bucket"]))
    {
        // R2 dpBackupOptions partilha BackupR2Options — bind manual das credenciais R2 padrão
        // se DpKeysBackup:R2 não tiver — Bruno pode reutilizar mesmo bucket.
        builder.Configuration.GetSection("DpKeysBackup:R2").Bind(dpBackupOptions.R2);
    }
    builder.Services.AddSingleton(dpBackupOptions);
    builder.Services.AddSingleton<IDpKeysBackupService, DpKeysBackupService>();
    if (dpBackupOptions.Enabled)
        builder.Services.AddHostedService<DpKeysBackupHostedService>();

    builder.Services.AddHealthChecks()
        .AddCheck("self", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy("API process is alive."), tags: ["live"])
        .AddCheck<RepairDeskDbHealthCheck>("db", tags: ["ready", "db"])
        .AddCheck<PhotoStorageHealthCheck>("storage", tags: ["ready", "storage"])
        .AddCheck<BackupHealthCheck>("backup", tags: ["backup"]);

    // Rate limiting (auth-strict: 5 login attempts per 15 min per IP). Auth is disabled in Testing/E2E;
    // public portal remains active in tests so anti-enumeration has regression coverage.
    var isTesting = builder.Environment.IsEnvironment("Testing");
    var e2eEnabled = builder.Configuration.GetValue("E2E:Enabled", false);
    var disableAuthRateLimits = isTesting || e2eEnabled;
    var disablePublicPortalRateLimit = e2eEnabled;
    builder.Services.AddRateLimiter(opt =>
    {
        opt.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        opt.AddPolicy("auth-strict", ctx =>
        {
            if (disableAuthRateLimits)
                return RateLimitPartition.GetNoLimiter("auth-strict");
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
            if (disablePublicPortalRateLimit)
                return RateLimitPartition.GetNoLimiter("public-portal");
            var key = ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 60,
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
            if (disableAuthRateLimits)
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

    // Sprint 252 (Doc 75 área 9 P2): output cache em endpoints portal público.
    // In-memory por defeito — quando houver >1 instância da API, Redis cache.
    builder.Services.AddOutputCache(options =>
    {
        options.AddPolicy("public-portal-30s", b => b
            .Expire(TimeSpan.FromSeconds(30))
            .SetVaryByRouteValue("slug"));
        options.AddPolicy("public-warranty-5min", b => b
            .Expire(TimeSpan.FromMinutes(5))
            .SetVaryByRouteValue("slug"));
    });

    // Sprint 250 (Doc 75 área 9 P2): response compression Brotli + Gzip.
    // Reduz payload de JSON/HTML/SVG em 70-90%. ASP.NET Core não comprime HTTPS
    // por defeito (BREACH/CRIME), mas vamos correr atrás de Caddy/Cloudflare —
    // o downstream tem TLS. Aqui o link API↔proxy é HTTP em rede privada.
    builder.Services.AddResponseCompression(options =>
    {
        options.EnableForHttps = true;
        options.Providers.Add<Microsoft.AspNetCore.ResponseCompression.BrotliCompressionProvider>();
        options.Providers.Add<Microsoft.AspNetCore.ResponseCompression.GzipCompressionProvider>();
        options.MimeTypes =
        [
            "application/json",
            "application/problem+json",
            "application/xml",
            "text/csv",
            "text/plain",
            "text/html",
            "image/svg+xml",
        ];
    });
    builder.Services.Configure<Microsoft.AspNetCore.ResponseCompression.BrotliCompressionProviderOptions>(o =>
        o.Level = System.IO.Compression.CompressionLevel.Fastest);
    builder.Services.Configure<Microsoft.AspNetCore.ResponseCompression.GzipCompressionProviderOptions>(o =>
        o.Level = System.IO.Compression.CompressionLevel.Fastest);
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new() { Title = "Mender API", Version = "v1" });
    });

    // Sprint 249 (Doc 74): CORS apertado — apenas origens explícitas em Cors:AllowedOrigins
    // (mais Frontend:BaseUrl), headers e métodos restringidos ao que a SPA realmente envia.
    // AllowCredentials necessário para enviar refresh cookie no /api/auth/refresh.
    var corsOrigins = BuildCorsOrigins(builder.Configuration, builder.Environment);
    builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
        .WithOrigins(corsOrigins)
        .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE", "OPTIONS")
        .WithHeaders(
            "Authorization",
            "Content-Type",
            "Accept",
            "X-Correlation-Id",
            "X-Requested-With",
            // Webhooks externos têm o seu próprio handler — estes só aparecem aqui se um
            // cliente integrador chamar a SPA com a sua API key. Mantém compatibilidade.
            "X-API-Key")
        .WithExposedHeaders("X-Correlation-Id")
        .AllowCredentials()
        .SetPreflightMaxAge(TimeSpan.FromMinutes(10))));

    var app = builder.Build();

    // Sprint 250 (Doc 75): response compression antes de tudo — comprime mesmo
    // as responses de erro do exception middleware.
    app.UseResponseCompression();
    app.UseMiddleware<CorrelationIdMiddleware>();
    // Sprint 249 (Doc 74): security headers aplicados a TODAS as responses, incluindo
    // preflight CORS. Tem que vir antes de UseCors para apanhar o response do OPTIONS.
    app.UseMiddleware<SecurityHeadersMiddleware>();
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
    // Sprint 252 (Doc 75): output cache nos endpoints públicos (atributo [OutputCache]).
    // Após rate-limiter para que rate-limit seja sempre verificado, mesmo em cache hit
    // (caso contrário um único IP podia spammar via cache warm).
    app.UseOutputCache();
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

static string[] BuildCorsOrigins(IConfiguration configuration, IWebHostEnvironment environment)
{
    var origins = configuration
        .GetSection("Cors:AllowedOrigins")
        .Get<string[]>()?
        .Where(o => !string.IsNullOrWhiteSpace(o))
        .Select(o => o.Trim().TrimEnd('/'))
        .ToList() ?? [];

    var frontendBaseUrl = configuration["Frontend:BaseUrl"];
    if (!string.IsNullOrWhiteSpace(frontendBaseUrl))
        origins.Add(frontendBaseUrl.Trim().TrimEnd('/'));

    if (environment.IsDevelopment() || environment.IsEnvironment("Testing"))
    {
        origins.Add("http://localhost:5173");
        origins.Add("http://localhost:3000");
    }

    origins = origins.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    if (origins.Count == 0)
        throw new InvalidOperationException("CORS origins not configured. Set Frontend:BaseUrl or Cors:AllowedOrigins.");

    return origins.ToArray();
}

public partial class Program;
