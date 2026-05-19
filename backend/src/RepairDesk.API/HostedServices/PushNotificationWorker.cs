using Microsoft.Extensions.Options;
using RepairDesk.Services.Push;

namespace RepairDesk.API.HostedServices;

public class PushNotificationWorker : BackgroundService
{
    private readonly IPushNotificationQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PushNotificationWorker> _logger;

    public PushNotificationWorker(
        IPushNotificationQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<PushNotificationWorker> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            RepairStatusChangedPushJob job;
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
                var service = scope.ServiceProvider.GetRequiredService<IPushNotificationService>();
                await service.SendRepairStatusChangedAsync(job.ReparacaoId, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Push notification job failed for repair {RepairId}", job.ReparacaoId);
            }
        }
    }
}

public class PushSubscriptionCleanupWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PushSubscriptionCleanupWorker> _logger;
    private readonly IOptions<PushOptions> _options;

    public PushSubscriptionCleanupWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<PushSubscriptionCleanupWorker> logger,
        IOptions<PushOptions> options)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _options = options;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Value.Enabled)
            return;

        using var timer = new PeriodicTimer(TimeSpan.FromHours(24));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<IPushNotificationService>();
                var removed = await service.PurgeDeliveredOlderThanAsync(stoppingToken);
                if (removed > 0)
                    _logger.LogInformation("Purged {Count} old push subscriptions", removed);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Push subscription cleanup failed");
            }
        }
    }
}
