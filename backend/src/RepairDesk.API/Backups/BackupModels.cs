namespace RepairDesk.API.Backups;

public enum BackupTrigger
{
    Scheduled,
    Manual,
}

public enum BackupLocation
{
    Local,
    R2,
}

public sealed record BackupRunResult(
    string FileName,
    string LocalPath,
    string? R2Key,
    long SizeBytes,
    DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt,
    BackupTrigger Trigger,
    bool UploadedToR2,
    int DeletedLocalFiles,
    string Status);

public sealed record BackupFileDto(
    string FileName,
    BackupLocation Location,
    DateTimeOffset Timestamp,
    long SizeBytes,
    string Status,
    string? Path,
    string? R2Key);

public sealed record BackupListResult(
    IReadOnlyList<BackupFileDto> Local,
    IReadOnlyList<BackupFileDto> R2,
    DateTimeOffset? LatestLocalBackupAt,
    string Status);

public sealed record BackupExecutionRequest(
    string ConnectionString,
    string DatabaseName,
    string LocalBackupPath,
    string SqlBackupPath);

public sealed record BackupRetentionResult(int DeletedFiles);
