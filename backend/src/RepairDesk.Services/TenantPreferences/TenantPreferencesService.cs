using System.Collections.Concurrent;
using System.Text.Json;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Enums;
using RepairDesk.Core.Exceptions;
using TenantPreferencesEntity = RepairDesk.Core.Entities.TenantPreferences;

namespace RepairDesk.Services.TenantPreferences;

public interface ITenantPreferencesService
{
    Task<TenantPreferencesRoot> GetAsync(CancellationToken ct = default);
    Task<TenantPreferencesRoot> GetForTenantAsync(Guid tenantId, CancellationToken ct = default);
    Task<TenantPreferencesRoot> UpdateAsync(TenantPreferencesRoot preferences, CancellationToken ct = default);
    Task<TenantPreferencesRoot> ResetGroupAsync(string group, CancellationToken ct = default);
}

public sealed class TenantPreferencesService : ITenantPreferencesService
{
    private static readonly ConcurrentDictionary<Guid, TenantPreferencesRoot> Cache = new();
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ITenantContext _tenant;
    private readonly ITenantPreferencesRepository _repo;

    public TenantPreferencesService(ITenantContext tenant, ITenantPreferencesRepository repo)
    {
        _tenant = tenant;
        _repo = repo;
    }

    public Task<TenantPreferencesRoot> GetAsync(CancellationToken ct = default)
    {
        var tenantId = RequireTenantId();
        return GetInternalAsync(tenantId, ignoreQueryFilters: false, ct);
    }

    public Task<TenantPreferencesRoot> GetForTenantAsync(Guid tenantId, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
            throw new ValidationException("tenant_invalid", "Tenant invalido.");

        return GetInternalAsync(tenantId, ignoreQueryFilters: true, ct);
    }

    public async Task<TenantPreferencesRoot> UpdateAsync(TenantPreferencesRoot preferences, CancellationToken ct = default)
    {
        var tenantId = RequireTenantId();
        var normalized = Normalize(preferences);
        var entity = await _repo.FindByTenantIdAsync(tenantId, ct: ct);
        if (entity is null)
        {
            entity = CreateEntity(tenantId, normalized);
            await _repo.AddAsync(entity, ct);
        }
        else
        {
            entity.Version = TenantPreferencesDefaults.SchemaVersion;
            entity.PreferencesJson = Serialize(normalized);
        }

        await _repo.SaveAsync(ct);
        Cache[tenantId] = normalized;
        return normalized;
    }

    public async Task<TenantPreferencesRoot> ResetGroupAsync(string group, CancellationToken ct = default)
    {
        var current = await GetAsync(ct);
        var defaults = TenantPreferencesDefaults.Create();
        var normalizedGroup = (group ?? string.Empty).Trim().ToLowerInvariant();

        var reset = normalizedGroup switch
        {
            "communication" or "comunicacao" => current with { Communication = defaults.Communication },
            "portal" or "portalcliente" or "portal-cliente" => current with { Portal = defaults.Portal },
            "repairs" or "reparacoes" or "reparações" => current with { Repairs = defaults.Repairs },
            "sales" or "vendas" => current with { Sales = defaults.Sales },
            _ => throw new ValidationException("preferences_group_invalid", "Grupo de preferencias invalido."),
        };

        return await UpdateAsync(reset, ct);
    }

    private async Task<TenantPreferencesRoot> GetInternalAsync(Guid tenantId, bool ignoreQueryFilters, CancellationToken ct)
    {
        if (Cache.TryGetValue(tenantId, out var cached))
            return cached;

        var entity = await _repo.FindByTenantIdAsync(tenantId, ignoreQueryFilters, ct);
        if (entity is null)
        {
            var defaults = TenantPreferencesDefaults.Create();
            entity = CreateEntity(tenantId, defaults);
            await _repo.AddAsync(entity, ct);
            await _repo.SaveAsync(ct);
            Cache[tenantId] = defaults;
            return defaults;
        }

        var parsed = DeserializeOrDefault(entity.PreferencesJson, out var hadInvalidJson);
        var normalized = Normalize(parsed);
        if (hadInvalidJson || entity.Version != TenantPreferencesDefaults.SchemaVersion || entity.PreferencesJson != Serialize(normalized))
        {
            entity.Version = TenantPreferencesDefaults.SchemaVersion;
            entity.PreferencesJson = Serialize(normalized);
            await _repo.SaveAsync(ct);
        }

        Cache[tenantId] = normalized;
        return normalized;
    }

    private Guid RequireTenantId()
        => _tenant.TenantId ?? throw new ForbiddenException("no_tenant", "Tenant nao definido.");

    private static TenantPreferencesEntity CreateEntity(Guid tenantId, TenantPreferencesRoot preferences)
        => new()
        {
            TenantId = tenantId,
            Version = TenantPreferencesDefaults.SchemaVersion,
            PreferencesJson = Serialize(preferences),
        };

    private static TenantPreferencesRoot DeserializeOrDefault(string? json, out bool invalid)
    {
        invalid = false;
        if (string.IsNullOrWhiteSpace(json))
            return TenantPreferencesDefaults.Create();

        try
        {
            return JsonSerializer.Deserialize<TenantPreferencesRoot>(json, JsonOptions)
                ?? TenantPreferencesDefaults.Create();
        }
        catch (JsonException)
        {
            invalid = true;
            return TenantPreferencesDefaults.Create();
        }
    }

    private static TenantPreferencesRoot Normalize(TenantPreferencesRoot? input)
    {
        var defaults = TenantPreferencesDefaults.Create();
        if (input is null) return defaults;

        return new TenantPreferencesRoot(
            NormalizeCommunication(input.Communication, defaults.Communication),
            NormalizePortal(input.Portal, defaults.Portal),
            NormalizeRepairs(input.Repairs, defaults.Repairs),
            NormalizeSales(input.Sales, defaults.Sales));
    }

    private static CommunicationPrefs NormalizeCommunication(CommunicationPrefs? input, CommunicationPrefs defaults)
    {
        if (input is null) return defaults;

        var templates = new Dictionary<string, WhatsAppStateTemplate>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in defaults.TemplatesByState)
            templates[item.Key] = item.Value;

        if (input.TemplatesByState is not null)
        {
            foreach (var item in input.TemplatesByState)
            {
                if (string.IsNullOrWhiteSpace(item.Key)) continue;
                var key = item.Key.Trim();
                var template = item.Value ?? templates.GetValueOrDefault(key, new WhatsAppStateTemplate(true, string.Empty, templates.Count * 10));
                var text = (template.Texto ?? string.Empty).Trim();
                if (text.Length > 2000) text = text[..2000];
                templates[key] = template with
                {
                    Texto = string.IsNullOrWhiteSpace(text) && templates.TryGetValue(key, out var fallback) ? fallback.Texto : text,
                    Order = Math.Clamp(template.Order, 0, 10_000),
                };
            }
        }

        var push = input.Push is null
            ? defaults.Push
            : new PushPrefs(
                input.Push.Enabled,
                NormalizeAllowedStates(input.Push.EstadosPermitidos, defaults.Push.EstadosPermitidos));

        return new CommunicationPrefs(
            input.WhatsAppEnabled,
            templates,
            Enum.IsDefined(input.RepeatMode) ? input.RepeatMode : defaults.RepeatMode,
            Math.Clamp(input.StaleDaysThreshold <= 0 ? defaults.StaleDaysThreshold : input.StaleDaysThreshold, 1, 90),
            push);
    }

    private static PortalPrefs NormalizePortal(PortalPrefs? input, PortalPrefs defaults)
    {
        if (input is null) return defaults;

        var url = string.IsNullOrWhiteSpace(input.GoogleReviewUrl) ? null : input.GoogleReviewUrl.Trim();
        if (url?.Length > 1000) url = url[..1000];

        return input with
        {
            GoogleReviewMinScore = Math.Clamp(input.GoogleReviewMinScore <= 0 ? defaults.GoogleReviewMinScore : input.GoogleReviewMinScore, 1, 5),
            GoogleReviewUrl = url,
        };
    }

    private static RepairsPrefs NormalizeRepairs(RepairsPrefs? input, RepairsPrefs defaults)
    {
        if (input is null) return defaults;
        return new RepairsPrefs(
            Enum.IsDefined(input.EntregarMarcaPago) ? input.EntregarMarcaPago : defaults.EntregarMarcaPago,
            Enum.IsDefined(input.GarantiaAutomatica) ? input.GarantiaAutomatica : defaults.GarantiaAutomatica);
    }

    private static SalesPrefs NormalizeSales(SalesPrefs? input, SalesPrefs defaults)
    {
        if (input is null) return defaults;

        var payment = string.IsNullOrWhiteSpace(input.DefaultMetodoPagamento)
            ? defaults.DefaultMetodoPagamento
            : input.DefaultMetodoPagamento.Trim();
        if (!Enum.TryParse<PaymentMethod>(payment, ignoreCase: true, out _))
            payment = defaults.DefaultMetodoPagamento;

        var condition = Enum.IsDefined(typeof(CondicaoArtigo), input.DefaultCondicaoArtigo)
            ? input.DefaultCondicaoArtigo
            : defaults.DefaultCondicaoArtigo;

        return new SalesPrefs(
            payment,
            condition,
            Enum.IsDefined(input.EmitirFatura) ? input.EmitirFatura : defaults.EmitirFatura,
            Enum.IsDefined(input.VendaGarantia) ? input.VendaGarantia : defaults.VendaGarantia);
    }

    private static string[] NormalizeAllowedStates(string[]? states, string[] defaults)
    {
        if (states is null || states.Length == 0) return defaults;

        var valid = Enum.GetNames<RepairStatus>().ToHashSet(StringComparer.OrdinalIgnoreCase);
        return states
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Where(valid.Contains)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string Serialize(TenantPreferencesRoot preferences)
        => JsonSerializer.Serialize(preferences, JsonOptions);
}
