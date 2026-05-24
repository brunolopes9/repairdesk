using RepairDesk.API.Backups;

namespace RepairDesk.API.HostedServices;

/// <summary>
/// Sprint 352 (Doc 76 gap crítico): corre <see cref="IDpKeysBackupService"/> diário
/// ~30min depois do BackupHostedService SQL (03:30 default). Sem catch-up porque:
/// (a) as dp-keys mudam raramente — perder um dia de backup não é crítico,
/// (b) corremos diário todos os dias mesmo que conteúdo não mude (override timestamped).
/// </summary>
public sealed class DpKeysBackupHostedService : BackgroundService
{
    private readonly IDpKeysBackupService _service;
    private readonly DpKeysBackupOptions _options;
    private readonly TimeProvider _clock;
    private readonly ILogger<DpKeysBackupHostedService> _logger;

    public DpKeysBackupHostedService(
        IDpKeysBackupService service,
        DpKeysBackupOptions options,
        TimeProvider clock,
        ILogger<DpKeysBackupHostedService> logger)
    {
        _service = service;
        _options = options;
        _clock = clock;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("DpKeysBackupDisabled — sai sem fazer nada");
            return;
        }

        if (!_service.IsConfigured)
        {
            _logger.LogWarning(
                "DpKeysBackupMisconfigured — Enabled=true mas falta password/R2/path. Sai sem correr.");
            return;
        }

        var schedule = DailyBackupSchedule.Parse(_options.CronSchedule);
        _logger.LogInformation(
            "DpKeysBackupHostedServiceStarted CronSchedule={CronSchedule}",
            _options.CronSchedule);

        while (!stoppingToken.IsCancellationRequested)
        {
            var now = _clock.GetLocalNow();
            var next = schedule.GetNext(now);
            var delay = next - now;
            _logger.LogInformation(
                "DpKeysBackupScheduled NextRunAt={NextRunAt} DelaySeconds={DelaySeconds}",
                next, delay.TotalSeconds);

            try
            {
                await Task.Delay(delay, _clock, stoppingToken);
                var result = await _service.RunBackupAsync(stoppingToken);
                _logger.LogInformation(
                    "DpKeysBackupCompleted KeyCount={KeyCount} Bytes={Bytes} R2Key={R2Key}",
                    result.KeyCount, result.EncryptedBytes, result.R2Key);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DpKeysBackupFailed");
                // Aguardar 1h antes de retry para não martelar R2 em caso de auth fail.
                try { await Task.Delay(TimeSpan.FromHours(1), _clock, stoppingToken); }
                catch (OperationCanceledException) { break; }
            }
        }
    }
}
