using System.Text.Json;

namespace RepairDesk.API.Backups;

public interface IBackupFileSystem
{
    void EnsureDirectory(string path);
    bool Exists(string path);
    long GetSize(string path);
    IReadOnlyList<BackupFileDto> ListLocalBackups(string localPath);
    void WriteMetadata(string backupPath, BackupMetadata metadata);
    BackupMetadata? TryReadMetadata(string backupPath);
    string GetMetadataPath(string backupPath);
    string GetRestoreWorkspacePath(string localPath);
    BackupRetentionResult ApplyRetention(string localPath, int retentionDays, DateTimeOffset now);
}

public sealed class BackupFileSystem : IBackupFileSystem
{
    public void EnsureDirectory(string path) => Directory.CreateDirectory(path);

    public bool Exists(string path) => File.Exists(path);

    public long GetSize(string path) => new FileInfo(path).Length;

    public IReadOnlyList<BackupFileDto> ListLocalBackups(string localPath)
    {
        if (!Directory.Exists(localPath))
            return [];

        return Directory.EnumerateFiles(localPath, "*.bak", SearchOption.TopDirectoryOnly)
            .Select(ToDto)
            .OrderByDescending(b => b.Timestamp)
            .ToList();
    }

    public void WriteMetadata(string backupPath, BackupMetadata metadata)
    {
        var json = JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(GetMetadataPath(backupPath), json);
    }

    public BackupMetadata? TryReadMetadata(string backupPath)
    {
        var path = GetMetadataPath(backupPath);
        if (!File.Exists(path))
            return null;

        try
        {
            return TryReadMetadataCore(path);
        }
        catch
        {
            return null;
        }
    }

    public string GetRestoreWorkspacePath(string localPath)
    {
        var path = Path.Combine(localPath, "_restore");
        Directory.CreateDirectory(path);
        return path;
    }

    public BackupRetentionResult ApplyRetention(string localPath, int retentionDays, DateTimeOffset now)
    {
        if (!Directory.Exists(localPath))
            return new BackupRetentionResult(0);

        var cutoff = now.AddDays(-retentionDays);
        var deleted = 0;

        foreach (var file in Directory.EnumerateFiles(localPath, "*.bak", SearchOption.TopDirectoryOnly))
        {
            var timestamp = GetBackupTimestamp(file);
            if (timestamp >= cutoff)
                continue;

            File.Delete(file);
            var metadataPath = BuildMetadataPath(file);
            if (File.Exists(metadataPath))
                File.Delete(metadataPath);
            deleted++;
        }

        return new BackupRetentionResult(deleted);
    }

    private static BackupFileDto ToDto(string path)
    {
        var info = new FileInfo(path);
        var metadata = TryReadMetadataCore(BuildMetadataPath(path));
        var timestamp = GetBackupTimestamp(path);
        return new BackupFileDto(
            BackupId.For(BackupLocation.Local, info.Name),
            info.Name,
            BackupLocation.Local,
            timestamp,
            info.Length,
            "OK",
            GetAgeHours(timestamp),
            metadata?.Snapshot,
            path,
            null);
    }

    private static DateTimeOffset GetBackupTimestamp(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        const string prefix = "repairdesk-";
        if (name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            var stamp = name[prefix.Length..];
            if (DateTimeOffset.TryParseExact(
                    stamp,
                    "yyyyMMdd-HHmm",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.AssumeUniversal,
                    out var parsed))
                return parsed.ToUniversalTime();
        }

        return new FileInfo(path).LastWriteTimeUtc;
    }

    public string GetMetadataPath(string backupPath) => BuildMetadataPath(backupPath);

    private static string BuildMetadataPath(string backupPath) => $"{backupPath}.json";

    private static double GetAgeHours(DateTimeOffset timestamp) =>
        Math.Max(0, (DateTimeOffset.UtcNow - timestamp.ToUniversalTime()).TotalHours);

    private static BackupMetadata? TryReadMetadataCore(string path)
    {
        try
        {
            return JsonSerializer.Deserialize<BackupMetadata>(File.ReadAllText(path));
        }
        catch
        {
            return null;
        }
    }
}
