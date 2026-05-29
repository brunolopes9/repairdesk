using Microsoft.EntityFrameworkCore;
using RepairDesk.Core.Enums;
using RepairDesk.DAL.Persistence;
using RepairDesk.Services.Push;

namespace RepairDesk.API.HostedServices;

/// <summary>
/// Sprint 441 (Doc 91 follow-up): digest diário de reparações em estado Pronto há
/// mais de N dias — o cliente foi avisado mas ainda não veio levantar. Para cada
/// tenant envia UM push staff: "X reparações prontas há +N dias".
///
/// Pattern espelhado dos cron S392 (StalledRepairs), S428 (OverdueTasks) e S430
/// (OverdueInvoices): digest em vez de spam, 24h poll, kill switch por config.
///
/// Config:
///   ReadyForPickup:Enabled  (default true)
///   ReadyForPickup:Days     (default 5) — número de dias desde a transição para
///                                          Pronto a partir do qual conta como atraso
/// </summary>
public sealed class ReadyForPickupHostedService : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromHours(24);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<ReadyForPickupHostedService> _logger;

    public ReadyForPickupHostedService(
        IServiceScopeFactory scopeFactory,
        IConfiguration config,
        ILogger<ReadyForPickupHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _config = config;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var enabled = _config.GetValue("ReadyForPickup:Enabled", true);
        if (!enabled)
        {
            _logger.LogInformation("ReadyForPickupHostedService desativado por config.");
            return;
        }
        var days = Math.Clamp(_config.GetValue("ReadyForPickup:Days", 5), 1, 60);

        _logger.LogInformation("ReadyForPickupHostedService started (limiar {Days}d, poll {Hours}h)", days, PollInterval.TotalHours);
        try { await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try { await TickAsync(days, stoppingToken); }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { _logger.LogError(ex, "ReadyForPickup tick falhou — retry no próximo poll."); }
            try { await Task.Delay(PollInterval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task TickAsync(int days, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var push = scope.ServiceProvider.GetRequiredService<IStaffPushQueue>();

        var cutoff = DateTime.UtcNow.AddDays(-days);

        // Reparações em Pronto há mais de N dias.
        var porTenant = await db.Reparacoes
            .IgnoreQueryFilters()
            .Where(r => r.Estado == RepairStatus.Pronto && r.EstadoSince < cutoff)
            .GroupBy(r => r.TenantId)
            .Select(g => new { TenantId = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        if (porTenant.Count == 0) return;
        _logger.LogInformation("ReadyForPickup: {Tenants} tenant(s) com reparações prontas por levantar.", porTenant.Count);

        foreach (var t in porTenant)
        {
            var body = t.Count == 1
                ? $"1 reparação pronta há mais de {days} dias por levantar."
                : $"{t.Count} reparações prontas há mais de {days} dias por levantar.";
            await push.EnqueueAsync(new StaffPushJob(
                t.TenantId,
                "Por levantar",
                body,
                "/reparacoes?estado=4",
                "ready-for-pickup"), ct);
        }
    }
}
