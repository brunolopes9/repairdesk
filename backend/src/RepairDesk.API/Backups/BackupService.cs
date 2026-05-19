using Amazon.S3;
using Microsoft.EntityFrameworkCore;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Enums;
using RepairDesk.DAL.Persistence;

namespace RepairDesk.API.Backups;

public interface IBackupService
{
    Task<BackupRunResult> RunBackupAsync(BackupTrigger trigger, CancellationToken ct = default);
    Task<BackupListResult> ListAsync(CancellationToken ct = default);
    Task<BackupRestorePreviewDto> GetRestorePreviewAsync(string backupId, CancellationToken ct = default);
    Task<BackupRestoreResult> RestoreAsync(
        string backupId,
        Guid? tenantId,
        Guid? appUserId,
        CancellationToken ct = default);
    BackupOptions GetOptions();
}

public sealed class BackupService : IBackupService
{
    private const string RestoreConfirmation = "RESTORE";
    private readonly IConfiguration _configuration;
    private readonly ISqlServerBackupExecutor _sql;
    private readonly IBackupRemoteStorage _remote;
    private readonly IBackupFileSystem _files;
    private readonly TimeProvider _clock;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BackupService> _logger;

    public BackupService(
        IConfiguration configuration,
        ISqlServerBackupExecutor sql,
        IBackupRemoteStorage remote,
        IBackupFileSystem files,
        TimeProvider clock,
        IServiceScopeFactory scopeFactory,
        ILogger<BackupService> logger)
    {
        _configuration = configuration;
        _sql = sql;
        _remote = remote;
        _files = files;
        _clock = clock;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public BackupOptions GetOptions() => BackupOptions.FromConfiguration(_configuration);

    public async Task<BackupRunResult> RunBackupAsync(BackupTrigger trigger, CancellationToken ct = default)
    {
        var options = GetOptions();
        options.Validate();

        var connectionString = GetConnectionString();
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

            var snapshot = await GetCurrentSnapshotAsync(ct);
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
            var metadata = new BackupMetadata(fileName, snapshot, started, completed, trigger, uploaded ? "completed" : "completed_local_only");
            _files.WriteMetadata(localPath, metadata);

            if (uploaded)
            {
                var metadataPath = _files.GetMetadataPath(localPath);
                await _remote.UploadAsync(metadataPath, $"{fileName}.json", options.R2, ct);
            }

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
        var items = local.Concat(remote)
            .OrderByDescending(b => b.Timestamp)
            .ToList();

        var latestLocal = local.Count == 0 ? (DateTimeOffset?)null : local.Max(b => b.Timestamp);
        var latest = items.Count == 0 ? (DateTimeOffset?)null : items.Max(b => b.Timestamp);
        var latestAgeHours = latest is null
            ? (double?)null
            : Math.Max(0, (_clock.GetUtcNow() - latest.Value.ToUniversalTime()).TotalHours);
        var health = ResolveHealth(items.FirstOrDefault(), latestAgeHours);
        var localBytesUsed = local.Sum(b => b.SizeBytes);
        var status = !options.Enabled
            ? "disabled"
            : options.R2.IsConfigured ? "ok" : "r2_not_configured";

        return new BackupListResult(
            local,
            remote,
            items,
            latestLocal,
            latest,
            latestAgeHours,
            health,
            localBytesUsed,
            options.RetentionDays,
            options.R2RetentionDays,
            status);
    }

    public async Task<BackupRestorePreviewDto> GetRestorePreviewAsync(string backupId, CancellationToken ct = default)
    {
        var options = GetOptions();
        var backup = await FindBackupAsync(backupId, options, ct);
        var current = await GetCurrentSnapshotAsync(ct);
        var metadata = await TryGetMetadataAsync(backup, options, ct);

        return new BackupRestorePreviewDto(
            backup with { Snapshot = metadata?.Snapshot ?? backup.Snapshot },
            current,
            metadata?.Snapshot ?? backup.Snapshot,
            "Vais substituir a base de dados actual por este backup. Esta accao nao pode ser desfeita.");
    }

    public async Task<BackupRestoreResult> RestoreAsync(
        string backupId,
        Guid? tenantId,
        Guid? appUserId,
        CancellationToken ct = default)
    {
        var options = GetOptions();
        options.Validate();
        var backup = await FindBackupAsync(backupId, options, ct);
        var currentSnapshot = await GetCurrentSnapshotAsync(ct);
        var metadata = await TryGetMetadataAsync(backup, options, ct);

        _logger.LogWarning(
            "BackupRestoreStarted BackupId={BackupId} FileName={FileName} Location={Location}",
            backupId,
            backup.FileName,
            backup.Location);

        try
        {
            var safetyBackup = await RunBackupAsync(BackupTrigger.Manual, ct);
            var restoreLocalPath = await EnsureRestoreSourceAsync(backup, options, ct);
            var restoreSqlPath = backup.Location == BackupLocation.Local
                ? CombineSqlPath(options.LocalPath, backup.FileName)
                : CombineSqlPath(Path.Combine(options.LocalPath, "_restore"), backup.FileName);

            await _sql.RestoreBackupAsync(new BackupRestoreExecutionRequest(
                GetConnectionString(),
                options.DatabaseName,
                restoreLocalPath,
                restoreSqlPath), ct);

            await WriteRestoreAuditAsync(backup, safetyBackup, tenantId, appUserId, ct);

            var restoredAt = _clock.GetUtcNow();
            _logger.LogWarning(
                "BackupRestoreCompleted BackupId={BackupId} FileName={FileName} SafetyBackup={SafetyBackup}",
                backupId,
                backup.FileName,
                safetyBackup.FileName);

            return new BackupRestoreResult(
                backup with { Snapshot = metadata?.Snapshot ?? backup.Snapshot },
                safetyBackup,
                currentSnapshot,
                metadata?.Snapshot ?? backup.Snapshot,
                restoredAt,
                "restored");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "BackupRestoreFailed BackupId={BackupId} FileName={FileName}", backupId, backup.FileName);
            throw;
        }
    }

    public static void ValidateRestoreConfirmation(BackupRestoreRequest request)
    {
        if (!string.Equals(request.ConfirmationText?.Trim(), RestoreConfirmation, StringComparison.Ordinal))
            throw new InvalidOperationException("restore_confirmation_required");
    }

    private async Task<BackupFileDto> FindBackupAsync(string backupId, BackupOptions options, CancellationToken ct)
    {
        try
        {
            _ = BackupId.Parse(backupId);
        }
        catch (Exception ex)
        {
            throw new KeyNotFoundException("Backup not found.", ex);
        }

        var list = await ListAsync(ct);
        var backup = list.Items.FirstOrDefault(b => b.Id == backupId);
        if (backup is null)
            throw new KeyNotFoundException("Backup not found.");

        if (backup.Location == BackupLocation.Local)
        {
            var path = backup.Path ?? Path.Combine(options.LocalPath, backup.FileName);
            if (!_files.Exists(path))
                throw new FileNotFoundException("Local backup file not found.", path);
        }

        return backup;
    }

    private async Task<string> EnsureRestoreSourceAsync(BackupFileDto backup, BackupOptions options, CancellationToken ct)
    {
        if (backup.Location == BackupLocation.Local)
            return backup.Path ?? Path.Combine(options.LocalPath, backup.FileName);

        if (string.IsNullOrWhiteSpace(backup.R2Key))
            throw new InvalidOperationException("R2 backup key is missing.");

        var restoreWorkspace = _files.GetRestoreWorkspacePath(options.LocalPath);
        var destinationPath = Path.Combine(restoreWorkspace, backup.FileName);
        await _remote.DownloadAsync(backup.R2Key, destinationPath, options.R2, ct);
        return destinationPath;
    }

    private async Task<BackupMetadata?> TryGetMetadataAsync(BackupFileDto backup, BackupOptions options, CancellationToken ct)
    {
        if (backup.Location == BackupLocation.Local)
            return backup.Path is null ? null : _files.TryReadMetadata(backup.Path);

        if (string.IsNullOrWhiteSpace(backup.R2Key) || !options.R2.IsConfigured)
            return null;

        try
        {
            var restoreWorkspace = _files.GetRestoreWorkspacePath(options.LocalPath);
            var metadataPath = Path.Combine(restoreWorkspace, $"{backup.FileName}.json");
            await _remote.DownloadAsync($"{backup.R2Key}.json", metadataPath, options.R2, ct);
            return _files.TryReadMetadata(metadataPath[..^5]);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
        catch (FileNotFoundException)
        {
            return null;
        }
    }

    private async Task<BackupSnapshotDto> GetCurrentSnapshotAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var capturedAt = _clock.GetUtcNow();

        var reparacoes = await db.Reparacoes.CountAsync(ct);
        var clientes = await db.Clientes.CountAsync(ct);
        var trabalhos = await db.Trabalhos.CountAsync(ct);
        var vendas = await db.Vendas.CountAsync(ct);
        var despesas = await db.Despesas.CountAsync(ct);

        return new BackupSnapshotDto(reparacoes, clientes, trabalhos, vendas, despesas, capturedAt);
    }

    private async Task WriteRestoreAuditAsync(
        BackupFileDto backup,
        BackupRunResult safetyBackup,
        Guid? tenantId,
        Guid? appUserId,
        CancellationToken ct)
    {
        if (tenantId is null)
            return;

        using var scope = _scopeFactory.CreateScope();
        var audit = scope.ServiceProvider.GetService<IAuditLogger>();
        if (audit is null)
            return;

        await audit.LogAsync(
            AuditAction.Restore,
            "Backup",
            null,
            new
            {
                backup.Id,
                backup.FileName,
                backup.Location,
                safetyBackup = safetyBackup.FileName,
            },
            tenantId,
            appUserId,
            ct);
    }

    private string GetConnectionString() =>
        _configuration.GetConnectionString("Default")
        ?? throw new InvalidOperationException("ConnectionStrings:Default not configured.");

    private static BackupHealthStatus ResolveHealth(BackupFileDto? latest, double? latestAgeHours)
    {
        if (latest is null || latest.Status.Equals("failed", StringComparison.OrdinalIgnoreCase))
            return BackupHealthStatus.Red;

        if (latestAgeHours is <= 26)
            return BackupHealthStatus.Green;

        if (latestAgeHours is <= 48)
            return BackupHealthStatus.Yellow;

        return BackupHealthStatus.Red;
    }

    private static string CombineSqlPath(string localPath, string fileName) =>
        $"{localPath.TrimEnd('/', '\\')}/{fileName}";
}
