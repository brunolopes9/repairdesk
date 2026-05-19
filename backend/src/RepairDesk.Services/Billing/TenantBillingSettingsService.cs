using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;
using RepairDesk.Core.Exceptions;

namespace RepairDesk.Services.Billing;

public interface ITenantBillingSettingsService
{
    Task<TenantBillingSettingsDto> GetMineAsync(CancellationToken ct = default);
    Task<TenantBillingSettingsDto> UpdateMineAsync(UpdateTenantBillingSettingsRequest req, CancellationToken ct = default);
    Task<BillingConnectionTestDto> TestConnectionAsync(CancellationToken ct = default);
    Task<IReadOnlyList<BillingSerieDto>> SyncSeriesAsync(CancellationToken ct = default);
    Task<TenantBillingSettingsDto> ConnectMoloniAsync(ConnectMoloniRequest req, CancellationToken ct = default);
    Task<MoloniOAuthStartDto> StartMoloniOAuthAsync(string redirectUri, CancellationToken ct = default);
    Task<TenantBillingSettingsDto> CompleteMoloniOAuthAsync(string code, string state, CancellationToken ct = default);
    Task<TenantBillingSettingsDto> DisconnectMoloniAsync(CancellationToken ct = default);
    Task<IReadOnlyList<MoloniCompanyDto>> ListCompaniesAsync(CancellationToken ct = default);
    Task<MoloniAutoDiscoverResultDto> AutoDiscoverAsync(CancellationToken ct = default);
}

public class TenantBillingSettingsService : ITenantBillingSettingsService
{
    private const string Mask = "****";
    private const string OAuthStateCachePrefix = "billing:moloni:oauth:";
    private static readonly TimeSpan OAuthStateTtl = TimeSpan.FromMinutes(10);

    private readonly ITenantBillingSettingsRepository _repo;
    private readonly ITenantContext _tenant;
    private readonly ITenantRepository _tenants;
    private readonly ISecretProtector _secrets;
    private readonly IMoloniClient _moloni;
    private readonly IDistributedCache _cache;
    private readonly IConfiguration _configuration;
    private readonly ILogger<TenantBillingSettingsService> _logger;

    public TenantBillingSettingsService(
        ITenantBillingSettingsRepository repo,
        ITenantContext tenant,
        ISecretProtector secrets,
        IMoloniClient moloni,
        IDistributedCache cache,
        IConfiguration configuration,
        ITenantRepository tenants,
        ILogger<TenantBillingSettingsService> logger)
    {
        _repo = repo;
        _tenant = tenant;
        _tenants = tenants;
        _secrets = secrets;
        _moloni = moloni;
        _cache = cache;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<TenantBillingSettingsDto> GetMineAsync(CancellationToken ct = default)
    {
        var settings = await FindOrCreateAsync(ct);
        return ToDto(settings);
    }

    public async Task<TenantBillingSettingsDto> UpdateMineAsync(UpdateTenantBillingSettingsRequest req, CancellationToken ct = default)
    {
        var settings = await FindOrCreateAsync(ct);

        settings.Provider = req.Provider;
        settings.ClientId = Clean(req.ClientId);
        settings.CompanyId = req.CompanyId;
        settings.DefaultDocumentType = req.DefaultDocumentType;
        settings.DefaultSerieId = req.DefaultSerieId;
        settings.SandboxMode = req.SandboxMode;
        settings.DefaultProductId = req.DefaultProductId;
        settings.DefaultTaxId = req.DefaultTaxId;
        settings.DefaultPaymentMethodId = req.DefaultPaymentMethodId;
        settings.DefaultMaturityDateId = req.DefaultMaturityDateId;
        settings.FallbackCustomerId = req.FallbackCustomerId;
        settings.ExemptionReason = Clean(req.ExemptionReason);

        ApplySecret(req.ApiKey, value => settings.ApiKeyCipherText = value);
        ApplySecret(req.ClientSecret, value => settings.ClientSecretCipherText = value);
        ApplySecret(req.RefreshToken, value => settings.RefreshTokenCipherText = value);

        await _repo.SaveAsync(ct);
        return ToDto(settings);
    }

    public async Task<BillingConnectionTestDto> TestConnectionAsync(CancellationToken ct = default)
    {
        var settings = await FindOrCreateAsync(ct);
        await _moloni.TestConnectionAsync(settings, ct);
        return new BillingConnectionTestDto(true, "Ligacao Moloni validada.");
    }

    public async Task<IReadOnlyList<BillingSerieDto>> SyncSeriesAsync(CancellationToken ct = default)
    {
        var settings = await FindOrCreateAsync(ct);
        var series = await _moloni.GetSeriesAsync(settings, ct);
        if (settings.DefaultSerieId is null or <= 0)
        {
            var preferred = series.FirstOrDefault(s => s.IsActive) ?? series.FirstOrDefault();
            if (preferred is not null)
            {
                settings.DefaultSerieId = preferred.Id;
                await _repo.SaveAsync(ct);
            }
        }
        return series;
    }

    public async Task<TenantBillingSettingsDto> ConnectMoloniAsync(ConnectMoloniRequest req, CancellationToken ct = default)
    {
        var settings = await FindOrCreateAsync(ct);
        await _moloni.ConnectViaPasswordGrantAsync(settings, req.Username, req.Password, ct);

        // Auto-descobrir Company ID se ainda não definido e só houver 1 empresa
        if (settings.CompanyId is null or <= 0)
        {
            try
            {
                var companies = await _moloni.GetCompaniesAsync(settings, ct);
                if (companies.Count == 1)
                {
                    settings.CompanyId = companies[0].Id;
                    await _repo.SaveAsync(ct);
                }
            }
            catch
            {
                // não bloqueia ligação se auto-descoberta falhar
            }
        }

        return ToDto(settings);
    }

    public async Task<MoloniOAuthStartDto> StartMoloniOAuthAsync(string redirectUri, CancellationToken ct = default)
    {
        if (_tenant.TenantId is not { } tenantId)
            throw new ValidationException("no_tenant_context", "Sem contexto de tenant.");

        var settings = await FindOrCreateAsync(ct);
        if (settings.Provider != BillingProvider.Moloni)
            throw new ValidationException("billing_provider_not_moloni", "Configura Moloni como provider antes de ligar.");
        if (settings.HasMoloniTokens())
            throw new ConflictException("moloni_already_connected", "A conta Moloni ja esta ligada. Desliga primeiro para voltar a autorizar.");
        if (string.IsNullOrWhiteSpace(settings.ClientId))
            throw new ValidationException("moloni_client_id_missing", "Preenche o Developer ID Moloni antes de ligar.");
        if (string.IsNullOrWhiteSpace(settings.ClientSecretCipherText))
            throw new ValidationException("moloni_client_secret_missing", "Preenche o Client Secret Moloni antes de ligar.");
        if (string.IsNullOrWhiteSpace(redirectUri))
            throw new ValidationException("moloni_redirect_uri_missing", "Redirect URI Moloni nao configurado.");

        var state = GenerateState();
        var expiresAt = DateTime.UtcNow.Add(OAuthStateTtl);
        var payload = new MoloniOAuthState(tenantId, redirectUri.Trim(), expiresAt);

        await _cache.SetStringAsync(
            OAuthStateCachePrefix + state,
            JsonSerializer.Serialize(payload),
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = OAuthStateTtl },
            ct);

        return new MoloniOAuthStartDto(BuildAuthorizationUrl(settings.ClientId, payload.RedirectUri, state), expiresAt);
    }

    public async Task<TenantBillingSettingsDto> CompleteMoloniOAuthAsync(string code, string state, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ValidationException("moloni_code_missing", "A Moloni nao devolveu codigo de autorizacao.");
        if (string.IsNullOrWhiteSpace(state))
            throw new ValidationException("moloni_state_missing", "State OAuth em falta.");

        var cacheKey = OAuthStateCachePrefix + state.Trim();
        var rawState = await _cache.GetStringAsync(cacheKey, ct);
        if (string.IsNullOrWhiteSpace(rawState))
            throw new ValidationException("moloni_state_invalid", "Sessao OAuth expirada ou invalida. Tenta ligar novamente.");

        await _cache.RemoveAsync(cacheKey, ct);

        var payload = JsonSerializer.Deserialize<MoloniOAuthState>(rawState)
            ?? throw new ValidationException("moloni_state_invalid", "Sessao OAuth invalida. Tenta ligar novamente.");
        if (payload.ExpiresAt <= DateTime.UtcNow)
            throw new ValidationException("moloni_state_expired", "Sessao OAuth expirada. Tenta ligar novamente.");

        var settings = await FindOrCreateForTenantAsync(payload.TenantId, ct);
        if (settings.HasMoloniTokens())
            throw new ConflictException("moloni_already_connected", "A conta Moloni ja esta ligada.");

        await _moloni.ExchangeAuthorizationCodeAsync(settings, code.Trim(), payload.RedirectUri, ct);
        await TryAutoSelectSingleCompanyAsync(settings, ct);
        return ToDto(settings);
    }

    public async Task<TenantBillingSettingsDto> DisconnectMoloniAsync(CancellationToken ct = default)
    {
        var settings = await FindOrCreateAsync(ct);
        settings.ApiKeyCipherText = null;
        settings.RefreshTokenCipherText = null;
        await _repo.SaveAsync(ct);
        return ToDto(settings);
    }

    public async Task<IReadOnlyList<MoloniCompanyDto>> ListCompaniesAsync(CancellationToken ct = default)
    {
        var settings = await FindOrCreateAsync(ct);
        return await _moloni.GetCompaniesAsync(settings, ct);
    }

    public async Task<MoloniAutoDiscoverResultDto> AutoDiscoverAsync(CancellationToken ct = default)
    {
        var settings = await FindOrCreateAsync(ct);
        EnsureMoloniReadyForAutoDiscover(settings);

        var tenant = await _tenants.FindByIdAsync(settings.TenantId, ct);
        var isVatExempt = tenant?.RegimeFiscal == RegimeFiscal.IsentoArt53;
        var targetTax = isVatExempt ? 0m : 23m;
        var steps = new List<MoloniAutoDiscoverStepDto>();

        IReadOnlyList<MoloniProductDto> products = Array.Empty<MoloniProductDto>();
        IReadOnlyList<MoloniTaxDto> taxes = Array.Empty<MoloniTaxDto>();
        IReadOnlyList<MoloniPaymentMethodDto> paymentMethods = Array.Empty<MoloniPaymentMethodDto>();
        IReadOnlyList<MoloniMaturityDateDto> maturityDates = Array.Empty<MoloniMaturityDateDto>();
        IReadOnlyList<MoloniCustomerDto> customers = Array.Empty<MoloniCustomerDto>();

        try
        {
            products = await _moloni.GetProductsAsync(settings, ct);
            var product = SelectPreferredProduct(products);
            var created = false;
            if (product is null)
            {
                product = await _moloni.InsertProductAsync(settings, "Serviço de reparação", ct);
                created = true;
            }

            settings.DefaultProductId = product.Id;
            AddSuccess(steps, "product", "Produto/serviço", created, product.Id, product.Name);
            LogDecision(settings, "product", created, product.Id, product.Name);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            AddFailure(steps, settings, "product", "Produto/serviço", ex);
        }

        try
        {
            taxes = await _moloni.GetTaxesAsync(settings, ct);
            var tax = SelectTax(taxes, targetTax);
            if (tax is null)
            {
                AddFailure(steps, settings, "tax", "IVA", $"Não encontrei imposto ativo com {targetTax:0}%.");
            }
            else
            {
                settings.DefaultTaxId = tax.Id;
                if (isVatExempt && string.IsNullOrWhiteSpace(settings.ExemptionReason))
                    settings.ExemptionReason = "M01";

                AddSuccess(steps, "tax", "IVA", created: false, tax.Id, tax.Name);
                LogDecision(settings, "tax", created: false, tax.Id, tax.Name);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            AddFailure(steps, settings, "tax", "IVA", ex);
        }

        try
        {
            paymentMethods = await _moloni.GetPaymentMethodsAsync(settings, ct);
            var payment = SelectPaymentMethod(paymentMethods);
            if (payment is null)
            {
                AddFailure(steps, settings, "payment", "Método de pagamento", "Não encontrei métodos de pagamento ativos.");
            }
            else
            {
                settings.DefaultPaymentMethodId = payment.Id;
                AddSuccess(steps, "payment", "Método de pagamento", created: false, payment.Id, payment.Name);
                LogDecision(settings, "payment", created: false, payment.Id, payment.Name);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            AddFailure(steps, settings, "payment", "Método de pagamento", ex);
        }

        try
        {
            maturityDates = await _moloni.GetMaturityDatesAsync(settings, ct);
            var maturity = SelectMaturityDate(maturityDates);
            if (maturity is null)
            {
                AddFailure(steps, settings, "maturity", "Prazo de vencimento", "Não encontrei prazos de vencimento ativos.");
            }
            else
            {
                settings.DefaultMaturityDateId = maturity.Id;
                AddSuccess(steps, "maturity", "Prazo de vencimento", created: false, maturity.Id, maturity.Name);
                LogDecision(settings, "maturity", created: false, maturity.Id, maturity.Name);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            AddFailure(steps, settings, "maturity", "Prazo de vencimento", ex);
        }

        try
        {
            customers = await _moloni.GetCustomersAsync(settings, ct);
            var customer = SelectFallbackCustomer(customers);
            var created = false;
            if (customer is null)
            {
                customer = await _moloni.InsertCustomerAsync(settings, "Consumidor Final", "999999990", ct);
                created = true;
            }

            settings.FallbackCustomerId = customer.Id;
            AddSuccess(steps, "customer", "Cliente fallback", created, customer.Id, customer.Name);
            LogDecision(settings, "customer", created, customer.Id, customer.Name);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            AddFailure(steps, settings, "customer", "Cliente fallback", ex);
        }

        await _repo.SaveAsync(ct);

        return new MoloniAutoDiscoverResultDto(
            products.Count,
            taxes.Count,
            paymentMethods.Count,
            maturityDates.Count,
            customers.Count,
            steps,
            ToDto(settings));
    }

    private async Task<TenantBillingSettings> FindOrCreateAsync(CancellationToken ct)
    {
        if (_tenant.TenantId is not { } tenantId)
            throw new ValidationException("no_tenant_context", "Sem contexto de tenant.");

        var settings = await _repo.FindByTenantIdAsync(tenantId, ct);
        if (settings is not null) return settings;

        settings = new TenantBillingSettings
        {
            TenantId = tenantId,
            Provider = BillingProvider.None,
            SandboxMode = true,
            DefaultDocumentType = BillingDocumentType.FaturaSimplificada,
        };
        await _repo.AddAsync(settings, ct);
        await _repo.SaveAsync(ct);
        return settings;
    }

    private async Task<TenantBillingSettings> FindOrCreateForTenantAsync(Guid tenantId, CancellationToken ct)
    {
        var settings = await _repo.FindByTenantIdAsync(tenantId, ct);
        if (settings is not null) return settings;

        settings = new TenantBillingSettings
        {
            TenantId = tenantId,
            Provider = BillingProvider.None,
            SandboxMode = true,
            DefaultDocumentType = BillingDocumentType.FaturaSimplificada,
        };
        await _repo.AddAsync(settings, ct);
        await _repo.SaveAsync(ct);
        return settings;
    }

    private async Task TryAutoSelectSingleCompanyAsync(TenantBillingSettings settings, CancellationToken ct)
    {
        if (settings.CompanyId is not null and > 0)
            return;

        try
        {
            var companies = await _moloni.GetCompaniesAsync(settings, ct);
            if (companies.Count == 1)
            {
                settings.CompanyId = companies[0].Id;
                await _repo.SaveAsync(ct);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogInformation(ex, "Moloni company auto-select skipped for tenant {TenantId}", settings.TenantId);
        }
    }

    private string BuildAuthorizationUrl(string clientId, string redirectUri, string state)
    {
        var authorizeUrl = _configuration["Billing:Moloni:OAuthAuthorizeUrl"];
        if (string.IsNullOrWhiteSpace(authorizeUrl))
            authorizeUrl = "https://www.moloni.pt/ac/root/oauth/";

        return $"{authorizeUrl.TrimEnd('/')}/" +
               "?response_type=code" +
               $"&client_id={Uri.EscapeDataString(clientId)}" +
               $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
               $"&state={Uri.EscapeDataString(state)}";
    }

    private static string GenerateState()
    {
        const string alphabet = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        Span<char> chars = stackalloc char[32];
        for (var i = 0; i < chars.Length; i++)
            chars[i] = alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];

        return new string(chars);
    }

    private void ApplySecret(string? raw, Action<string?> assign)
    {
        if (raw is null || raw == Mask) return;
        var clean = Clean(raw);
        assign(clean is null ? null : _secrets.Protect(clean));
    }

    private static TenantBillingSettingsDto ToDto(TenantBillingSettings s) => new(
        s.Provider,
        !string.IsNullOrWhiteSpace(s.ApiKeyCipherText),
        !string.IsNullOrWhiteSpace(s.ApiKeyCipherText) ? Mask : null,
        s.ClientId,
        !string.IsNullOrWhiteSpace(s.ClientSecretCipherText),
        !string.IsNullOrWhiteSpace(s.RefreshTokenCipherText),
        s.CompanyId,
        s.DefaultDocumentType,
        s.DefaultSerieId,
        s.SandboxMode,
        s.DefaultProductId,
        s.DefaultTaxId,
        s.DefaultPaymentMethodId,
        s.DefaultMaturityDateId,
        s.FallbackCustomerId,
        s.ExemptionReason);

    private static string? Clean(string? s)
        => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static void EnsureMoloniReadyForAutoDiscover(TenantBillingSettings settings)
    {
        if (settings.Provider != BillingProvider.Moloni)
            throw new ValidationException("billing_provider_not_moloni", "Configura Moloni como provider de faturação.");
        if (string.IsNullOrWhiteSpace(settings.ApiKeyCipherText))
            throw new ValidationException("moloni_not_connected", "Liga a conta Moloni antes de auto-configurar.");
        if (settings.CompanyId is null or <= 0)
            throw new ValidationException("moloni_company_missing", "Escolhe a empresa Moloni antes de auto-configurar.");
    }

    private static MoloniProductDto? SelectPreferredProduct(IReadOnlyList<MoloniProductDto> products)
        => PreferActive(products, p => p.IsActive)
            .Select(p => new { Product = p, Score = ProductScore(p.Name) })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Product.Id)
            .Select(x => x.Product)
            .FirstOrDefault();

    private static int ProductScore(string name)
    {
        var normalized = NormalizeForSearch(name);
        if (normalized.Contains("reparacao", StringComparison.Ordinal)) return 3;
        if (normalized.Contains("servico", StringComparison.Ordinal)) return 2;
        if (normalized.Contains("mao de obra", StringComparison.Ordinal)) return 1;
        return 0;
    }

    private static MoloniTaxDto? SelectTax(IReadOnlyList<MoloniTaxDto> taxes, decimal targetPercent)
    {
        var matches = PreferActive(taxes, t => t.IsActive)
            .Where(t => Math.Abs(t.Value - targetPercent) < 0.01m);

        if (targetPercent == 0m)
        {
            return matches
                .OrderByDescending(t => string.Equals(t.ExemptionReasonCode, "M01", StringComparison.OrdinalIgnoreCase))
                .ThenBy(t => t.Id)
                .FirstOrDefault();
        }

        return matches.OrderBy(t => t.Id).FirstOrDefault();
    }

    private static MoloniPaymentMethodDto? SelectPaymentMethod(IReadOnlyList<MoloniPaymentMethodDto> methods)
    {
        var active = PreferActive(methods, m => m.IsActive);
        return active.FirstOrDefault(m => NormalizeForSearch(m.Name).Contains("numerario", StringComparison.Ordinal))
               ?? active.FirstOrDefault();
    }

    private static MoloniMaturityDateDto? SelectMaturityDate(IReadOnlyList<MoloniMaturityDateDto> dates)
    {
        var active = PreferActive(dates, d => d.IsActive);
        return active.FirstOrDefault(d =>
               {
                   var name = NormalizeForSearch(d.Name);
                   return name.Contains("pronto pagamento", StringComparison.Ordinal)
                          || name.Contains("pronto", StringComparison.Ordinal)
                          || name.Contains("imediato", StringComparison.Ordinal);
               })
               ?? active.FirstOrDefault();
    }

    private static MoloniCustomerDto? SelectFallbackCustomer(IReadOnlyList<MoloniCustomerDto> customers)
    {
        var active = PreferActive(customers, c => c.IsActive);
        return active.FirstOrDefault(c => string.Equals(c.Vat, "999999990", StringComparison.Ordinal))
               ?? active.FirstOrDefault(c => NormalizeForSearch(c.Name).Contains("consumidor final", StringComparison.Ordinal));
    }

    private static IReadOnlyList<T> PreferActive<T>(IReadOnlyList<T> items, Func<T, bool> isActive)
    {
        var active = items.Where(isActive).ToList();
        return active.Count > 0 ? active : items;
    }

    private static string NormalizeForSearch(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);
        foreach (var c in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(char.ToLowerInvariant(c));
        }

        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    private static void AddSuccess(
        List<MoloniAutoDiscoverStepDto> steps,
        string key,
        string label,
        bool created,
        int id,
        string name)
        => steps.Add(new MoloniAutoDiscoverStepDto(
            key,
            label,
            Success: true,
            Created: created,
            Id: id,
            Name: name,
            Message: created ? "Criado automaticamente." : "Encontrado na Moloni."));

    private void AddFailure(
        List<MoloniAutoDiscoverStepDto> steps,
        TenantBillingSettings settings,
        string key,
        string label,
        Exception ex)
    {
        AddFailure(steps, settings, key, label, ex.Message);
        _logger.LogWarning(ex, "Moloni auto-discover {Step} failed for tenant {TenantId}", key, settings.TenantId);
    }

    private static void AddFailure(
        List<MoloniAutoDiscoverStepDto> steps,
        TenantBillingSettings settings,
        string key,
        string label,
        string message)
        => steps.Add(new MoloniAutoDiscoverStepDto(
            key,
            label,
            Success: false,
            Created: false,
            Id: null,
            Name: null,
            Message: message));

    private void LogDecision(TenantBillingSettings settings, string step, bool created, int id, string name)
        => _logger.LogInformation(
            "Moloni auto-discover {Step} {Decision} for tenant {TenantId}: {Id} {Name}",
            step,
            created ? "created" : "found",
            settings.TenantId,
            id,
            name);

    private sealed record MoloniOAuthState(Guid TenantId, string RedirectUri, DateTime ExpiresAt);
}

internal static class TenantBillingSettingsExtensions
{
    public static bool HasMoloniTokens(this TenantBillingSettings settings)
        => !string.IsNullOrWhiteSpace(settings.ApiKeyCipherText)
           && !string.IsNullOrWhiteSpace(settings.RefreshTokenCipherText);
}
