using Microsoft.EntityFrameworkCore;
using RepairDesk.DAL.Persistence;
using RepairDesk.Services.Push;

namespace RepairDesk.API.HostedServices;

/// <summary>
/// Sprint 468 (Doc 90 Tier 2 #6 — cross-sell): digest diário de Devices com
/// GarantiaFabricanteUntil entre hoje e hoje+N dias. Permite ao Bruno contactar
/// o cliente proactivamente para oferecer garantia loja antes do fabricante
/// acabar — oportunidade de upsell sem custo de aquisição.
///
/// Complementa o widget Dashboard do S467 (que mostra a lista visualmente).
/// Pattern espelhado de S392/S428/S430/S441/S458: poll 24h, kill switch,
/// digest único por tenant.
///
/// Config:
///   DeviceGarantiaFabricante:Enabled    (default true)
///   DeviceGarantiaFabricante:DaysWindow (default 30) — janela à frente em dias
/// </summary>
public sealed class DeviceGarantiaFabricanteExpiryHostedService : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromHours(24);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<DeviceGarantiaFabricanteExpiryHostedService> _logger;

    public DeviceGarantiaFabricanteExpiryHostedService(
        IServiceScopeFactory scopeFactory,
        IConfiguration config,
        ILogger<DeviceGarantiaFabricanteExpiryHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _config = config;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var enabled = _config.GetValue("DeviceGarantiaFabricante:Enabled", true);
        if (!enabled)
        {
            _logger.LogInformation("DeviceGarantiaFabricanteExpiryHostedService desativado por config.");
            return;
        }
        var days = Math.Clamp(_config.GetValue("DeviceGarantiaFabricante:DaysWindow", 30), 1, 365);

        _logger.LogInformation("DeviceGarantiaFabricante started (janela {Days}d, poll {Hours}h)", days, PollInterval.TotalHours);
        try { await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try { await TickAsync(days, stoppingToken); }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { _logger.LogError(ex, "DeviceGarantiaFabricante tick falhou — retry no próximo poll."); }
            try { await Task.Delay(PollInterval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task TickAsync(int days, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var push = scope.ServiceProvider.GetRequiredService<IStaffPushQueue>();

        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
        var cutoff = hoje.AddDays(days);

        var porTenant = await db.Devices
            .IgnoreQueryFilters()
            .Where(d => !d.Arquivado
                && d.GarantiaFabricanteUntil != null
                && d.GarantiaFabricanteUntil >= hoje
                && d.GarantiaFabricanteUntil <= cutoff)
            .GroupBy(d => d.TenantId)
            .Select(g => new { TenantId = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        if (porTenant.Count == 0) return;
        _logger.LogInformation("DeviceGarantiaFabricante: {Tenants} tenant(s) com Devices em fim de garantia.", porTenant.Count);

        foreach (var t in porTenant)
        {
            var body = t.Count == 1
                ? $"1 equipamento em fim de garantia ({days}d). Oferecer garantia loja?"
                : $"{t.Count} equipamentos em fim de garantia ({days}d). Oferecer garantia loja?";
            await push.EnqueueAsync(new StaffPushJob(
                t.TenantId,
                "Cross-sell garantia",
                body,
                "/",
                "device-garantia-fabricante"), ct);
        }
    }
}
