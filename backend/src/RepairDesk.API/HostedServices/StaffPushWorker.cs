using RepairDesk.Services.Push;

namespace RepairDesk.API.HostedServices;

/// <summary>
/// Sprint 366: consome a fila de notificações de staff e envia o Web Push num scope
/// próprio. Isolado do PushNotificationWorker (cliente) para que uma fila não bloqueie a
/// outra.
/// </summary>
public class StaffPushWorker : BackgroundService
{
    private readonly IStaffPushQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<StaffPushWorker> _logger;

    public StaffPushWorker(IStaffPushQueue queue, IServiceScopeFactory scopeFactory, ILogger<StaffPushWorker> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            StaffPushJob job;
            try
            {
                job = await _queue.DequeueAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<IStaffPushService>();
                await service.NotifyTenantAsync(job, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Staff push job falhou para tenant {TenantId}", job.TenantId);
            }
        }
    }
}
