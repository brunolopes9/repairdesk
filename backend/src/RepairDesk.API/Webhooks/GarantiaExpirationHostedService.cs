using Microsoft.EntityFrameworkCore;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.DAL.Persistence;
using RepairDesk.Services.Webhooks;

namespace RepairDesk.API.Webhooks;

/// <summary>
/// Cron diário que detecta garantias que acabaram de expirar e publica o evento
/// <c>garantia.expirada</c> uma vez por garantia. Idempotente via
/// <see cref="Garantia.ExpirationNotifiedAt"/>: garantias com este campo NULL e <c>DataFim &lt; now</c>
/// disparam o webhook; após publish, o campo é set para timestamp UTC e nunca mais notifica.
///
/// Atenção a backfill: à primeira execução em prod com garantias antigas no estado expirado,
/// vai publicar tudo. Para evitar, pré-set <c>ExpirationNotifiedAt = NOW</c> manualmente em
/// garantias já passadas antes do deploy (script de migração).
/// </summary>
public class GarantiaExpirationHostedService : BackgroundService
{
    private const int BatchSize = 50;
    private static readonly TimeSpan PollInterval = TimeSpan.FromHours(6);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<GarantiaExpirationHostedService> _logger;

    public GarantiaExpirationHostedService(IServiceScopeFactory scopeFactory, ILogger<GarantiaExpirationHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("GarantiaExpirationHostedService started (poll every {Hours}h)", PollInterval.TotalHours);
        // Espera 5min antes do primeiro tick para o app arrancar limpo (migrations, warmup, etc).
        try { await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessExpiredAsync(stoppingToken);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Garantia expiration tick failed — retry next poll.");
            }
            try { await Task.Delay(PollInterval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task ProcessExpiredAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IGarantiaRepository>();
        var publisher = scope.ServiceProvider.GetRequiredService<IWebhookPublisher>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var agora = DateTime.UtcNow;
        var expired = await repo.ListExpiredPendingNotificationAsync(agora, BatchSize, ct);
        if (expired.Count == 0) return;

        _logger.LogInformation("Found {Count} expired garantias pending notification.", expired.Count);

        foreach (var g in expired)
        {
            try
            {
                await publisher.PublishAsync(g.TenantId, WebhookEvents.GarantiaExpirada, new
                {
                    garantiaId = g.Id,
                    slug = g.Slug,
                    origem = g.SourceType.ToString(),
                    vendaId = g.VendaId,
                    reparacaoId = g.ReparacaoId,
                    dataFim = g.DataFim,
                }, ct);

                // Marca como notificada via update tracked (sem .Anular sem motivo, sem alterar outros campos).
                await db.Garantias
                    .IgnoreQueryFilters()
                    .Where(x => x.Id == g.Id)
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.ExpirationNotifiedAt, agora), ct);
            }
            catch (Exception ex)
            {
                // Falha numa garantia não bloqueia as outras. Próximo tick volta a tentar
                // porque ExpirationNotifiedAt continua null.
                _logger.LogWarning(ex, "Falha a publicar garantia.expirada para garantia {GarantiaId}.", g.Id);
            }
        }
    }
}
