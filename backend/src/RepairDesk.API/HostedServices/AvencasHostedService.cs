using Microsoft.EntityFrameworkCore;
using RepairDesk.DAL.Persistence;
using RepairDesk.Services.Push;

namespace RepairDesk.API.HostedServices;

/// <summary>
/// Sprint 546 (Doc 93 #1): digest diário de avenças DEVIDAS (ativas com ProximaEmissao ≤ hoje).
/// Modo conservador deliberado: o cron NÃO emite faturas sozinho — manda push staff "pronta a
/// emitir, 1 clique" (emitir documentos fiscais automaticamente sem olhos é uma decisão que o
/// tenant tem de tomar explicitamente; fica para uma fase 2 opt-in).
/// Pattern dos crons S392/S428/S430: digest, 24h poll, kill switch por config.
///
/// Config: Avencas:Enabled (default true)
/// </summary>
public sealed class AvencasHostedService : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromHours(24);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<AvencasHostedService> _logger;

    public AvencasHostedService(
        IServiceScopeFactory scopeFactory,
        IConfiguration config,
        ILogger<AvencasHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _config = config;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_config.GetValue("Avencas:Enabled", true))
        {
            _logger.LogInformation("AvencasHostedService desativado por config.");
            return;
        }

        _logger.LogInformation("AvencasHostedService started (poll {Hours}h)", PollInterval.TotalHours);
        try { await Task.Delay(TimeSpan.FromMinutes(6), stoppingToken); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try { await TickAsync(stoppingToken); }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { _logger.LogError(ex, "Avenças tick falhou — retry no próximo poll."); }
            try { await Task.Delay(PollInterval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task TickAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var push = scope.ServiceProvider.GetRequiredService<IStaffPushQueue>();

        var devidas = await ListDevidasAsync(db, DateTime.UtcNow.Date, ct);
        foreach (var grupo in devidas.GroupBy(a => a.TenantId))
        {
            var lista = grupo.ToList();
            var (corpo, url) = lista.Count == 1
                ? ($"\"{lista[0].Descricao}\" ({lista[0].ClienteNome}) — {lista[0].ValorCents / 100m:0.00}€. Abre o cliente e emite com 1 clique.",
                   $"/clientes/{lista[0].ClienteId}")
                : ($"{lista.Count} avenças prontas a emitir ({lista.Sum(a => a.ValorCents) / 100m:0.00}€ no total).",
                   "/clientes");
            await push.EnqueueAsync(new StaffPushJob(grupo.Key, "Avenças prontas a emitir", corpo, url, "avencas-devidas"), ct);
        }
    }

    public sealed record AvencaDevida(Guid TenantId, Guid ClienteId, string Descricao, string? ClienteNome, int ValorCents);

    /// <summary>Avenças ativas devidas (ProximaEmissao ≤ hoje), todos os tenants. Estático e testável.</summary>
    public static async Task<List<AvencaDevida>> ListDevidasAsync(AppDbContext db, DateTime hojeUtc, CancellationToken ct = default)
        => await db.Avencas
            .IgnoreQueryFilters() // cron multi-tenant — repor soft-delete à mão
            .Where(a => !a.IsDeleted && a.Ativa && a.ProximaEmissao <= hojeUtc)
            .Select(a => new AvencaDevida(a.TenantId, a.ClienteId, a.Descricao,
                a.Cliente != null ? a.Cliente.Nome : null, a.ValorCents))
            .ToListAsync(ct);
}
