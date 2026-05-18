namespace RepairDesk.API.Backups;

public interface IBackupService
{
    Task<BackupRunResult> RunBackupAsync(BackupTrigger trigger, CancellationToken ct = default);
    Task<BackupListResult> ListAsync(CancellationToken ct = default);
    BackupOptions GetOptions();
}

public sealed class BackupService : IBackupService
{
    private readonly IConfiguration _configuration;
    private readonly ISqlServerBackupExecutor _sql;
    private readonly IBackupRemoteStorage _remote;
    private readonly IBackupFileSystem _files;
    private readonly TimeProvider _clock;
    private readonly ILogger<BackupService> _logger;

    public BackupService(
        IConfiguration configuration,
        ISqlServerBackupExecutor sql,
        IBackupRemoteStorage remote,
        IBackupFileSystem files,
        TimeProvider clock,
        ILogger<BackupService> logger)
    {
        _configuration = configuration;
        _sql = sql;
        _remote = remote;
        _files = files;
        _clock = clock;
        _logger = logger;
    }

    public BackupOptions GetOptions() => BackupOptions.FromConfiguration(_configuration);

    public async Task<BackupRunResult> RunBackupAsync(BackupTrigger trigger, CancellationToken ct = default)
    {
        var options = GetOptions();
        options.Validate();

        var connectionString = _configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("ConnectionStrings:Default not configured.");

        var started = _clock.GetUtcNow();
        var stamp = started.ToString("yyyyMMdd-HHmm");
        var fileName = $"repairdesk-{stamp}.bak";
        var localPath = Path.Combine(options.LocalPath, fileName);
        var sqlPath = CombineSqlPath(options.LocalPath, fileName);

        _files.EnsureDirectory(options.LocalPath);

        _logger.LogInformation(
            "BackupStarted Trigger={Trigger} Database={DatabaseName} FileName={FileName}",
            trigger,
            options.DatabaseName,
            fileName);

        try
        {
            await _sql.CreateBackupAsync(new BackupExecutionRequest(
                connectionString,
                options.DatabaseName,
                localPath,
                sqlPath), ct);

            if (!_files.Exists(localPath))
                throw new FileNotFoundException($"SQL Server backup command completed but file was not found: {localPath}", localPath);

            var size = _files.GetSize(localPath);
            string? r2Key = null;
            var uploaded = false;

            if (options.R2.IsConfigured)
            {
                var r2Errors = options.R2.ValidateValues();
                if (r2Errors.Count > 0)
                    throw new InvalidOperationException($"Backup R2 config invalid: {string.Join("; ", r2Errors)}");

                r2Key = await _remote.UploadAsync(localPath, fileName, options.R2, ct);
                uploaded = true;
            }
            else
            {
                _logger.LogWarning("BackupUploaded Skipped=true Reason=R2NotConfigured FileName={FileName}", fileName);
            }

            var retention = _files.ApplyRetention(options.LocalPath, options.RetentionDays, _clock.GetUtcNow());
            _logger.LogInformation(
                "BackupRetentionApplied RetentionDays={RetentionDays} DeletedFiles={DeletedFiles}",
                options.RetentionDays,
                retention.DeletedFiles);

            var completed = _clock.GetUtcNow();
            _logger.LogInformation(
                "BackupCompleted Trigger={Trigger} FileName={FileName} SizeBytes={SizeBytes} UploadedToR2={UploadedToR2}",
                trigger,
                fileName,
                size,
                uploaded);

            return new BackupRunResult(
                fileName,
                localPath,
                r2Key,
                size,
                started,
                completed,
                trigger,
                uploaded,
                retention.DeletedFiles,
                uploaded ? "completed" : "completed_local_only");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "BackupFailed Trigger={Trigger} FileName={FileName}", trigger, fileName);
            throw;
        }
    }

    public async Task<BackupListResult> ListAsync(CancellationToken ct = default)
    {
        var options = GetOptions();
        var local = _files.ListLocalBackups(options.LocalPath);
        var remote = await _remote.ListAsync(options.R2, ct);
        DateTimeOffset? latest = local.Count == 0 ? null : local.Max(b => b.Timestamp);
        var status = options.R2.IsConfigured ? "ok" : "r2_not_configured";

        return new BackupListResult(local, remote, latest, status);
    }

    private static string CombineSqlPath(string localPath, string fileName) =>
        $"{localPath.TrimEnd('/', '\\')}/{fileName}";
}
