using RepairDesk.API.Backups;

namespace RepairDesk.API.HostedServices;

public sealed class BackupHostedService : BackgroundService
{
    private readonly IBackupService _backup;
    private readonly TimeProvider _clock;
    private readonly ILogger<BackupHostedService> _logger;

    public BackupHostedService(
        IBackupService backup,
        TimeProvider clock,
        ILogger<BackupHostedService> logger)
    {
        _backup = backup;
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
