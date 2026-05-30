using Microsoft.EntityFrameworkCore;
using RepairDesk.Core.Enums;
using RepairDesk.DAL.Persistence;
using RepairDesk.Services.Push;

namespace RepairDesk.API.HostedServices;

/// <summary>
/// Sprint 458 (Doc 91 ponto 3 — lembretes via push): digest diário de reparações
/// que mudaram para um estado "comunicável" (Diagnóstico/AguardaPeça/Pronto) há
/// mais de N horas mas para as quais o staff ainda não registou nenhuma comunicação
/// Outbound desde a mudança de estado.
///
/// Fecha o loop S456/S457: a UX já tem CTAs "Avisar diagnóstico/peça/pronto" que
/// criam Outbound; este cron deteta quem caiu nas malhas.
///
/// Pattern espelhado de S441 (ReadyForPickup), S392 (StalledRepairs), S428
/// (OverdueTasks), S430 (OverdueInvoices): poll 24h, kill switch por config,
/// digest único por tenant em vez de spam.
///
/// Config:
///   ClienteNotificar:Enabled        (default true)
///   ClienteNotificar:HoursThreshold (default 8) — horas desde a mudança de estado
///                                                 sem outbound a partir das quais
///                                                 conta para o digest. 8h cobre
///                                                 o ciclo "marcado de manhã, ainda
///                                                 não avisado ao fim do dia".
/// </summary>
public sealed class ClienteNotificarPendingHostedService : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromHours(24);

    // Estados em que faz sentido avisar o cliente — alinhados com os CTAs do S456/S457.
    private static readonly RepairStatus[] EstadosComunicaveis =
    {
        RepairStatus.Diagnostico,
        RepairStatus.AguardaPeca,
        RepairStatus.Pronto,
    };

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<ClienteNotificarPendingHostedService> _logger;

    public ClienteNotificarPendingHostedService(
        IServiceScopeFactory scopeFactory,
        IConfiguration config,
        ILogger<ClienteNotificarPendingHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _config = config;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var enabled = _config.GetValue("ClienteNotificar:Enabled", true);
        if (!enabled)
        {
            _logger.LogInformation("ClienteNotificarPendingHostedService desativado por config.");
            return;
        }
        var hours = Math.Clamp(_config.GetValue("ClienteNotificar:HoursThreshold", 8), 1, 168);

        _logger.LogInformation("ClienteNotificarPending started (limiar {Hours}h, poll {PollHours}h)", hours, PollInterval.TotalHours);
        try { await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try { await TickAsync(hours, stoppingToken); }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { _logger.LogError(ex, "ClienteNotificarPending tick falhou — retry no próximo poll."); }
            try { await Task.Delay(PollInterval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task TickAsync(int hours, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var push = scope.ServiceProvider.GetRequiredService<IStaffPushQueue>();

        var cutoff = DateTime.UtcNow.AddHours(-hours);

        // Reparações em estado comunicável há > N horas SEM Outbound desde a mudança de estado.
        // Outbound = (Direcao=1) ou (Direcao=0 sai do escopo — Inbound é o cliente a falar connosco).
        // ComunicacaoTipo é irrelevante (Telefone/WhatsApp/Email/SMS contam).
        var pendentes = await db.Reparacoes
            .IgnoreQueryFilters()
            .Where(r => EstadosComunicaveis.Contains(r.Estado) && r.EstadoSince < cutoff)
            .Where(r => !db.ReparacaoComunicacoes
                .Where(c => c.ReparacaoId == r.Id && c.Direcao == ComunicacaoDirecao.Outbound)
                .Any(c => c.CreatedAt >= r.EstadoSince))
            .GroupBy(r => r.TenantId)
            .Select(g => new { TenantId = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        if (pendentes.Count == 0) return;
        _logger.LogInformation("ClienteNotificarPending: {Tenants} tenant(s) com reparações por avisar.", pendentes.Count);

        foreach (var t in pendentes)
        {
            var body = t.Count == 1
                ? $"1 cliente aguarda novidades há mais de {hours}h."
                : $"{t.Count} clientes aguardam novidades há mais de {hours}h.";
            await push.EnqueueAsync(new StaffPushJob(
                t.TenantId,
                "Avisar cliente",
                body,
                "/reparacoes",
                "cliente-notificar-pending"), ct);
        }
    }
}
