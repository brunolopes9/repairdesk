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
}

public class TenantBillingSettingsService : ITenantBillingSettingsService
{
    private const string Mask = "****";

    private readonly ITenantBillingSettingsRepository _repo;
    private readonly ITenantContext _tenant;
    private readonly ISecretProtector _secrets;
    private readonly IMoloniClient _moloni;

    public TenantBillingSettingsService(
        ITenantBillingSettingsRepository repo,
        ITenantContext tenant,
        ISecretProtector secrets,
        IMoloniClient moloni)
    {
        _repo = repo;
        _tenant = tenant;
        _secrets = secrets;
        _moloni = moloni;
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
}
