using RepairDesk.API.Backups;

namespace RepairDesk.API.HostedServices;

public sealed class BackupHostedService : BackgroundService
{
    private readonly IBackupService _backup;
    private readonly IBackupFileSystem _files;
    private readonly TimeProvider _clock;
    private readonly ILogger<BackupHostedService> _logger;

    public BackupHostedService(
        IBackupService backup,
        IBackupFileSystem files,
        TimeProvider clock,
        ILogger<BackupHostedService> logger)
    {
        _backup = backup;
        _files = files;
        _clock = clock;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var options = _backup.GetOptions();
        options.Validate();
        var schedule = DailyBackupSchedule.Parse(options.CronSchedule);

        _logger.LogInformation(
            "BackupHostedServiceStarted CronSchedule={CronSchedule} RetentionDays={RetentionDays}",
            options.CronSchedule,
            options.RetentionDays);

        // Sprint 231: se nunca houve backup, correr 1 imediato em vez de esperar até 03:00.
        // Evita health check spam 'No local backup found' por horas após primeiro deploy.
        try
        {
            var existing = _files.ListLocalBackups(options.LocalPath);
            if (existing.Count == 0)
            {
                _logger.LogInformation("BackupInitialRun: nenhum backup local — a executar agora");
                await _backup.RunBackupAsync(BackupTrigger.Scheduled, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "BackupInitialRunFailed");
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            var now = _clock.GetLocalNow();
            var next = schedule.GetNext(now);
            var delay = next - now;

            _logger.LogInformation(
                "BackupScheduled NextRunAt={NextRunAt} DelaySeconds={DelaySeconds}",
                next,
                delay.TotalSeconds);

            try
            {
                await Task.Delay(delay, _clock, stoppingToken);
                await _backup.RunBackupAsync(BackupTrigger.Scheduled, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "BackupFailed Trigger={Trigger}", BackupTrigger.Scheduled);
            }

            options = _backup.GetOptions();
            schedule = DailyBackupSchedule.Parse(options.CronSchedule);
        }
    }
}
