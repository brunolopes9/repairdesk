using System.Text;
using System.Text.Json.Serialization;

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

public enum BackupHealthStatus
{
    Green,
    Yellow,
    Red,
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
    string Id,
    string FileName,
    BackupLocation Location,
    DateTimeOffset Timestamp,
    long SizeBytes,
    string Status,
    double AgeHours,
    BackupSnapshotDto? Snapshot,
    [property: JsonIgnore]
    string? Path,
    string? R2Key);

public sealed record BackupListResult(
    IReadOnlyList<BackupFileDto> Local,
    IReadOnlyList<BackupFileDto> R2,
    IReadOnlyList<BackupFileDto> Items,
    DateTimeOffset? LatestLocalBackupAt,
    DateTimeOffset? LatestBackupAt,
    double? LatestBackupAgeHours,
    BackupHealthStatus HealthStatus,
    long LocalBytesUsed,
    int LocalRetentionDays,
    int R2RetentionDays,
    string Status);

public sealed record BackupSnapshotDto(
    int Reparacoes,
    int Clientes,
    int Trabalhos,
    int Vendas,
    int Despesas,
    DateTimeOffset CapturedAt);

public sealed record BackupMetadata(
    string FileName,
    BackupSnapshotDto Snapshot,
    DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt,
    BackupTrigger Trigger,
    string Status);

public sealed record BackupRestorePreviewDto(
    BackupFileDto Backup,
    BackupSnapshotDto CurrentSnapshot,
    BackupSnapshotDto? BackupSnapshot,
    string Warning);

public sealed record BackupRestoreRequest(string ConfirmationText);

public sealed record BackupRestoreResult(
    BackupFileDto RestoredBackup,
    BackupRunResult SafetyBackup,
    BackupSnapshotDto CurrentSnapshotBeforeRestore,
    BackupSnapshotDto? BackupSnapshot,
    DateTimeOffset RestoredAt,
    string Status);

public sealed record BackupExecutionRequest(
    string ConnectionString,
    string DatabaseName,
    string LocalBackupPath,
    string SqlBackupPath);

public sealed record BackupRestoreExecutionRequest(
    string ConnectionString,
    string DatabaseName,
    string LocalBackupPath,
    string SqlBackupPath);

public sealed record BackupRetentionResult(int DeletedFiles);

public static class BackupId
{
    private const char Separator = '|';

    public static string For(BackupLocation location, string value)
    {
        var raw = $"{location}{Separator}{value}";
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(raw))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    public static (BackupLocation Location, string Value) Parse(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Backup id is required.", nameof(id));

        var padded = id.Replace('-', '+').Replace('_', '/');
        padded = padded.PadRight(padded.Length + (4 - padded.Length % 4) % 4, '=');
        var raw = Encoding.UTF8.GetString(Convert.FromBase64String(padded));
        var parts = raw.Split(Separator, 2);

        if (parts.Length != 2 || !Enum.TryParse<BackupLocation>(parts[0], out var location))
            throw new ArgumentException("Backup id is invalid.", nameof(id));

        return (location, parts[1]);
    }
}
