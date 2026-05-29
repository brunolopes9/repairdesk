using Microsoft.EntityFrameworkCore;
using RepairDesk.Core.Enums;
using RepairDesk.DAL.Persistence;
using RepairDesk.Services.Push;

namespace RepairDesk.API.HostedServices;

/// <summary>
/// Sprint 428 (Doc 90 cross-feature S422+S147): digest diário de tarefas internas atrasadas.
///
/// Para cada tenant que tenha tarefas Pendentes com DueAt já passado, envia UM push-resumo
/// aos staff: "X tarefas atrasadas — vê em /tarefas". Pattern espelhado do S392
/// <see cref="StalledRepairsHostedService"/> (digest em vez de spam por task).
///
/// Config:
///   OverdueTasks:Enabled   (default true)  — flag para desligar globalmente
///   OverdueTasks:GraceHours (default 0)    — tolerância antes de considerar atrasada
/// </summary>
public sealed class OverdueTasksHostedService : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromHours(24);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<OverdueTasksHostedService> _logger;

    public OverdueTasksHostedService(
        IServiceScopeFactory scopeFactory,
        IConfiguration config,
        ILogger<OverdueTasksHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _config = config;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var enabled = _config.GetValue("OverdueTasks:Enabled", true);
        if (!enabled)
        {
            _logger.LogInformation("OverdueTasksHostedService desativado por config.");
            return;
        }
        var graceHours = Math.Clamp(_config.GetValue("OverdueTasks:GraceHours", 0), 0, 168);

        _logger.LogInformation("OverdueTasksHostedService started (grace {Grace}h, poll {Hours}h)", graceHours, PollInterval.TotalHours);
        // Pequeno delay inicial — evita pico no startup junto com restantes hosted services.
        try { await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try { await TickAsync(graceHours, stoppingToken); }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { _logger.LogError(ex, "Overdue-tasks tick falhou — retry no próximo poll."); }
            try { await Task.Delay(PollInterval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task TickAsync(int graceHours, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var push = scope.ServiceProvider.GetRequiredService<IStaffPushQueue>();

        var cutoff = DateTime.UtcNow.AddHours(-graceHours);
        var groups = await db.InternalTasks
            .IgnoreQueryFilters()
            .Where(t => t.Status == InternalTaskStatus.Pendente
                     && t.DueAt != null
                     && t.DueAt < cutoff)
            .GroupBy(t => t.TenantId)
            .Select(g => new { TenantId = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        if (groups.Count == 0) return;
        _logger.LogInformation("Overdue-tasks: {Tenants} tenant(s) com tarefas atrasadas.", groups.Count);

        foreach (var g in groups)
        {
            var body = g.Count == 1
                ? "1 tarefa atrasada — abre Tarefas para resolver."
                : $"{g.Count} tarefas atrasadas — abre Tarefas para resolver.";
            await push.EnqueueAsync(new StaffPushJob(
                g.TenantId,
                "Tarefas atrasadas",
                body,
                "/tarefas",
                "overdue-tasks"), ct);
        }
    }
}
