namespace RepairDesk.API.Backups;

public interface IBackupFileSystem
{
    void EnsureDirectory(string path);
    bool Exists(string path);
    long GetSize(string path);
    IReadOnlyList<BackupFileDto> ListLocalBackups(string localPath);
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
            deleted++;
        }

        return new BackupRetentionResult(deleted);
    }

    private static BackupFileDto ToDto(string path)
    {
        var info = new FileInfo(path);
        return new BackupFileDto(
            info.Name,
            BackupLocation.Local,
            GetBackupTimestamp(path),
            info.Length,
            "available",
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
}
