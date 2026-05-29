using Microsoft.EntityFrameworkCore;
using RepairDesk.Core.Enums;
using RepairDesk.DAL.Persistence;
using RepairDesk.Services.Push;

namespace RepairDesk.API.HostedServices;

/// <summary>
/// Sprint 430 (Doc 90 secção 7.2 — "Automated overdue reminders"): digest diário de
/// reparações e trabalhos entregues há mais de N dias sem pagamento. Para cada tenant
/// envia UM push staff: "X cobranças em atraso há +N dias — abre Reparações para resolver".
///
/// Pattern espelhado dos cron S392 (StalledRepairs) e S428 (OverdueTasks): digest em vez
/// de spam, 24h poll, kill switch por config.
///
/// Config:
///   OverdueInvoices:Enabled (default true)
///   OverdueInvoices:Days    (default 7) — número de dias após Entregue/Concluido
///                                           para considerar cobrança em atraso
/// </summary>
public sealed class OverdueInvoicesHostedService : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromHours(24);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<OverdueInvoicesHostedService> _logger;

    public OverdueInvoicesHostedService(
        IServiceScopeFactory scopeFactory,
        IConfiguration config,
        ILogger<OverdueInvoicesHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _config = config;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var enabled = _config.GetValue("OverdueInvoices:Enabled", true);
        if (!enabled)
        {
            _logger.LogInformation("OverdueInvoicesHostedService desativado por config.");
            return;
        }
        var days = Math.Clamp(_config.GetValue("OverdueInvoices:Days", 7), 1, 90);

        _logger.LogInformation("OverdueInvoicesHostedService started (limiar {Days}d, poll {Hours}h)", days, PollInterval.TotalHours);
        try { await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try { await TickAsync(days, stoppingToken); }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { _logger.LogError(ex, "Overdue-invoices tick falhou — retry no próximo poll."); }
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

        // Reparações entregues há +N dias com pagamento não finalizado.
        var reparacoesPorTenant = await db.Reparacoes
            .IgnoreQueryFilters()
            .Where(r => r.Estado == RepairStatus.Entregue
                     && r.EntregueEm != null && r.EntregueEm < cutoff
                     && r.EstadoPagamento != PaymentStatus.Pago)
            .GroupBy(r => r.TenantId)
            .Select(g => new { TenantId = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        // Trabalhos concluídos há +N dias com pagamento não finalizado.
        var trabalhosPorTenant = await db.Trabalhos
            .IgnoreQueryFilters()
            .Where(t => t.Status == TrabalhoStatus.Concluido
                     && t.DataConclusao != null && t.DataConclusao < cutoff
                     && t.EstadoPagamento != PaymentStatus.Pago)
            .GroupBy(t => t.TenantId)
            .Select(g => new { TenantId = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        // Merge por tenant.
        var totals = new Dictionary<Guid, int>();
        foreach (var r in reparacoesPorTenant) totals[r.TenantId] = totals.GetValueOrDefault(r.TenantId) + r.Count;
        foreach (var t in trabalhosPorTenant) totals[t.TenantId] = totals.GetValueOrDefault(t.TenantId) + t.Count;

        if (totals.Count == 0) return;
        _logger.LogInformation("Overdue-invoices: {Tenants} tenant(s) com cobranças em atraso.", totals.Count);

        foreach (var (tenantId, count) in totals)
        {
            var body = count == 1
                ? $"1 reparação/trabalho entregue há mais de {days} dias sem pagamento."
                : $"{count} cobranças em atraso há mais de {days} dias.";
            await push.EnqueueAsync(new StaffPushJob(
                tenantId,
                "Cobranças em atraso",
                body,
                "/reparacoes",
                "overdue-invoices"), ct);
        }
    }
}
