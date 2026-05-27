using Microsoft.EntityFrameworkCore;
using RepairDesk.Core.Enums;
using RepairDesk.DAL.Persistence;
using RepairDesk.Services.Push;

namespace RepairDesk.API.HostedServices;

/// <summary>
/// Sprint 392 (Doc 84): 4.º gatilho de push staff — reparações paradas. Uma vez por dia, varre
/// (cross-tenant) as reparações que estão num estado não-terminal há mais de N dias e envia ao staff
/// de cada tenant UM push-resumo ("X reparações paradas há +N dias"). Digest diário em vez de
/// um-push-por-reparação para não ser spam; abre /reparacoes. Threshold/ativação por config.
/// </summary>
public sealed class StalledRepairsHostedService : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromHours(24);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<StalledRepairsHostedService> _logger;

    public StalledRepairsHostedService(IServiceScopeFactory scopeFactory, IConfiguration config, ILogger<StalledRepairsHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _config = config;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var enabled = _config.GetValue("StalledRepairs:Enabled", true);
        if (!enabled)
        {
            _logger.LogInformation("StalledRepairsHostedService desativado por config.");
            return;
        }
        var days = Math.Clamp(_config.GetValue("StalledRepairs:Days", 5), 1, 90);

        _logger.LogInformation("StalledRepairsHostedService started (limiar {Days}d, poll {Hours}h)", days, PollInterval.TotalHours);
        try { await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try { await TickAsync(days, stoppingToken); }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { _logger.LogError(ex, "Stalled-repairs tick falhou — retry no próximo poll."); }
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
        var groups = await db.Reparacoes
            .IgnoreQueryFilters()
            .Where(r => r.Estado != RepairStatus.Entregue
                     && r.Estado != RepairStatus.Cancelado
                     && r.EstadoSince < cutoff)
            .GroupBy(r => r.TenantId)
            .Select(g => new { TenantId = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        if (groups.Count == 0) return;
        _logger.LogInformation("Stalled-repairs: {Tenants} tenant(s) com reparações paradas.", groups.Count);

        foreach (var g in groups)
        {
            var body = g.Count == 1
                ? $"1 reparação há mais de {days} dias sem avançar."
                : $"{g.Count} reparações há mais de {days} dias sem avançar.";
            await push.EnqueueAsync(new StaffPushJob(
                g.TenantId,
                "Reparações paradas",
                body,
                "/reparacoes",
                "stalled-repairs"), ct);
        }
    }
}
