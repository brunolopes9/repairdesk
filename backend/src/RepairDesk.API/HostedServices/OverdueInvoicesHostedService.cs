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
///   OverdueInvoices:Enabled    (default true)
///   OverdueInvoices:Days       (default 7)  — dias após Entregue/Concluido para cobrança em atraso
///   OverdueInvoices:FaturaDays (default 30) — Sprint 545: dias após emissão de uma Fatura a
///                                             crédito (FT) sem Recibo para a dívida contar
///                                             como vencida (prazo habitual PT)
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
        var faturaDays = Math.Clamp(_config.GetValue("OverdueInvoices:FaturaDays", 30), 1, 365);

        _logger.LogInformation("OverdueInvoicesHostedService started (limiar {Days}d, FT {FaturaDays}d, poll {Hours}h)", days, faturaDays, PollInterval.TotalHours);
        try { await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try { await TickAsync(days, faturaDays, stoppingToken); }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { _logger.LogError(ex, "Overdue-invoices tick falhou — retry no próximo poll."); }
            try { await Task.Delay(PollInterval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task TickAsync(int days, int faturaDays, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var push = scope.ServiceProvider.GetRequiredService<IStaffPushQueue>();

        // Sprint 545: dívida formal — FT emitida há +N dias ainda sem Recibo de liquidação.
        var ftPorTenant = await CountFaturasEmDividaPorTenantAsync(db, DateTime.UtcNow.AddDays(-faturaDays), ct);
        foreach (var (tenantId, info) in ftPorTenant)
        {
            var corpo = info.Count == 1
                ? $"1 fatura a crédito há mais de {faturaDays} dias sem recibo ({info.TotalCents / 100m:0.00}€ por receber)."
                : $"{info.Count} faturas a crédito há mais de {faturaDays} dias sem recibo ({info.TotalCents / 100m:0.00}€ por receber).";
            await push.EnqueueAsync(new StaffPushJob(
                tenantId,
                "Faturas em dívida vencidas",
                corpo,
                "/documentos",
                "overdue-faturas"), ct);
        }

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

    /// <summary>Sprint 545: contagem + total por tenant de FT em dívida vencida.</summary>
    public sealed record FtDividaInfo(int Count, long TotalCents);

    /// <summary>
    /// Sprint 545: Faturas a crédito (FT) ativas emitidas antes do cutoff e ainda SEM Recibo de
    /// liquidação — a mesma semântica do KPI "Em dívida" da página /documentos (S544). Cobre
    /// Reparações, Trabalhos e Vendas locais; documentos criados só no painel Moloni ficam de
    /// fora (o cron não chama a API Moloni — a página mostra-os na mesma). Pode sobrepor-se ao
    /// digest de cobranças acima (visões diferentes: operacional vs dívida fiscal formal).
    /// Público e estático para ser testável com AppDbContext InMemory.
    /// </summary>
    public static async Task<Dictionary<Guid, FtDividaInfo>> CountFaturasEmDividaPorTenantAsync(
        AppDbContext db, DateTime cutoffUtc, CancellationToken ct = default)
    {
        // IgnoreQueryFilters salta o filtro de tenant (cron corre para todos) MAS também o de
        // soft-delete → repor !IsDeleted à mão.
        var reparacoes = await db.Reparacoes
            .IgnoreQueryFilters()
            .Where(r => !r.IsDeleted
                     && r.InvoiceNumber != null && r.InvoiceNumber.StartsWith("FT")
                     && r.InvoiceEmittedAt != null && r.InvoiceEmittedAt < cutoffUtc
                     && r.ReciboNumero == null)
            .GroupBy(r => r.TenantId)
            .Select(g => new { TenantId = g.Key, Count = g.Count(), Total = g.Sum(r => (long)(r.PrecoFinalCents ?? r.OrcamentoCents ?? 0)) })
            .ToListAsync(ct);

        var trabalhos = await db.Trabalhos
            .IgnoreQueryFilters()
            .Where(t => !t.IsDeleted
                     && t.InvoiceNumber != null && t.InvoiceNumber.StartsWith("FT")
                     && t.InvoiceEmittedAt != null && t.InvoiceEmittedAt < cutoffUtc
                     && t.ReciboNumero == null)
            .GroupBy(t => t.TenantId)
            .Select(g => new { TenantId = g.Key, Count = g.Count(), Total = g.Sum(t => (long)(t.PrecoFinalCents ?? t.OrcamentoCents ?? 0)) })
            .ToListAsync(ct);

        var vendas = await db.Vendas
            .IgnoreQueryFilters()
            .Where(v => !v.IsDeleted
                     && v.InvoiceNumber != null && v.InvoiceNumber.StartsWith("FT")
                     && v.InvoiceEmittedAt != null && v.InvoiceEmittedAt < cutoffUtc
                     && v.ReciboNumero == null)
            .Select(v => new { v.TenantId, Total = v.Items.Sum(i => (long)(i.Quantidade * i.PrecoUnitarioCents - i.DescontoCents)) })
            .ToListAsync(ct);

        var result = new Dictionary<Guid, FtDividaInfo>();
        void Add(Guid tenantId, int count, long total)
        {
            var atual = result.GetValueOrDefault(tenantId, new FtDividaInfo(0, 0));
            result[tenantId] = new FtDividaInfo(atual.Count + count, atual.TotalCents + total);
        }
        foreach (var r in reparacoes) Add(r.TenantId, r.Count, r.Total);
        foreach (var t in trabalhos) Add(t.TenantId, t.Count, t.Total);
        foreach (var v in vendas) Add(v.TenantId, 1, v.Total);
        return result;
    }
}
