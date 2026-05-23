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

        // Sprint 231 (v2): correr backup imediato se nunca houve OU se mais recente > 24h.
        // Evita health check spam '/api/health/backup 503' quando container fica off mais
        // tempo que o intervalo (ex: weekend, dev local 2-3 dias).
        try
        {
            var existing = _files.ListLocalBackups(options.LocalPath);
            var latest = existing.Count == 0 ? (DateTimeOffset?)null : existing.Max(b => b.Timestamp);
            var needsBackup = latest is null
                || (_clock.GetUtcNow() - latest.Value.ToUniversalTime()) > TimeSpan.FromHours(24);
            if (needsBackup)
            {
                _logger.LogInformation(
                    "BackupCatchUpRun: latest={Latest} — a executar agora antes do próximo schedule",
                    latest);
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
