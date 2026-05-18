using Microsoft.Data.SqlClient;

namespace RepairDesk.API.Backups;

public sealed class BackupOptions
{
    public const string SectionName = "Backup";

    public bool Enabled { get; init; }
    public string CronSchedule { get; init; } = "0 3 * * *";
    public int RetentionDays { get; init; } = 30;
    public string LocalPath { get; init; } = "/backups";
    public string DatabaseName { get; init; } = "RepairDesk";
    public BackupR2Options R2 { get; init; } = new();

    public static BackupOptions FromConfiguration(IConfiguration configuration)
    {
        var section = configuration.GetSection(SectionName);
        var connectionString = configuration.GetConnectionString("Default");

        return new BackupOptions
        {
            Enabled = section.GetValue("Enabled", false),
            CronSchedule = section["CronSchedule"] ?? "0 3 * * *",
            RetentionDays = section.GetValue("RetentionDays", 30),
            LocalPath = section["LocalPath"] ?? "/backups",
            DatabaseName = section["DatabaseName"] ?? TryGetDatabaseName(connectionString) ?? "RepairDesk",
            R2 = BackupR2Options.FromConfiguration(configuration),
        };
    }

    public IReadOnlyList<string> ValidateValues()
    {
        var errors = new List<string>();

        if (RetentionDays < 1)
            errors.Add("Backup:RetentionDays must be >= 1.");

        if (string.IsNullOrWhiteSpace(LocalPath))
            errors.Add("Backup:LocalPath is required.");

        if (string.IsNullOrWhiteSpace(DatabaseName))
            errors.Add("Backup:DatabaseName is required.");

        if (DailyBackupSchedule.TryParse(CronSchedule, out _, out var scheduleError) is false)
            errors.Add(scheduleError);

        return errors;
    }

    public void Validate()
    {
        var errors = ValidateValues();
        if (errors.Count > 0)
            throw new InvalidOperationException($"Backup config invalid: {string.Join("; ", errors)}");
    }

    private static string? TryGetDatabaseName(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            return null;

        try
        {
            var builder = new SqlConnectionStringBuilder(connectionString);
            return string.IsNullOrWhiteSpace(builder.InitialCatalog) ? null : builder.InitialCatalog;
        }
        catch
        {
            return null;
        }
    }
}

public sealed class BackupR2Options
{
    public string AccountId { get; init; } = string.Empty;
    public string AccessKey { get; init; } = string.Empty;
    public string Secret { get; init; } = string.Empty;
    public string Bucket { get; init; } = string.Empty;
    public string Prefix { get; init; } = "backups";

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(AccountId) &&
        !string.IsNullOrWhiteSpace(AccessKey) &&
        !string.IsNullOrWhiteSpace(Secret) &&
        !string.IsNullOrWhiteSpace(Bucket);

    public string Endpoint => string.IsNullOrWhiteSpace(AccountId)
        ? string.Empty
        : $"https://{AccountId}.r2.cloudflarestorage.com";

    public static BackupR2Options FromConfiguration(IConfiguration configuration)
    {
        var backup = configuration.GetSection("Backup:R2");
        var storage = configuration.GetSection("Storage:R2");

        return new BackupR2Options
        {
            AccountId = FirstNonEmpty(storage["AccountId"], backup["AccountId"]),
            AccessKey = FirstNonEmpty(storage["AccessKey"], backup["AccessKey"]),
            Secret = FirstNonEmpty(storage["Secret"], backup["Secret"]),
            Bucket = FirstNonEmpty(backup["Bucket"], storage["Bucket"]),
            Prefix = NormalizePrefix(backup["Prefix"] ?? "backups"),
        };
    }

    public IReadOnlyList<string> ValidateValues()
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(AccountId))
            errors.Add("Backup:R2:AccountId or Storage:R2:AccountId is required for R2 upload.");
        else if (AccountId.Contains("://", StringComparison.Ordinal) ||
                 AccountId.Contains('/', StringComparison.Ordinal) ||
                 AccountId.Contains('\\', StringComparison.Ordinal) ||
                 AccountId.Contains(' ', StringComparison.Ordinal))
            errors.Add("R2 account id must not be a URL or path.");

        if (string.IsNullOrWhiteSpace(AccessKey))
            errors.Add("Backup:R2:AccessKey or Storage:R2:AccessKey is required for R2 upload.");

        if (string.IsNullOrWhiteSpace(Secret))
            errors.Add("Backup:R2:Secret or Storage:R2:Secret is required for R2 upload.");

        if (string.IsNullOrWhiteSpace(Bucket))
            errors.Add("Backup:R2:Bucket is required for R2 upload.");
        else if (Bucket.Contains('/', StringComparison.Ordinal) ||
                 Bucket.Contains('\\', StringComparison.Ordinal) ||
                 Bucket.Contains(' ', StringComparison.Ordinal))
            errors.Add("Backup:R2:Bucket must be a bucket name, not a path.");

        if (Prefix.Contains("..", StringComparison.Ordinal) || Prefix.Contains('\\', StringComparison.Ordinal))
            errors.Add("Backup:R2:Prefix is invalid.");

        return errors;
    }

    public string BuildKey(string fileName) =>
        string.IsNullOrWhiteSpace(Prefix) ? fileName : $"{Prefix}/{fileName}";

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim() ?? string.Empty;

    private static string NormalizePrefix(string prefix) => prefix.Trim().Trim('/');
}

public sealed record DailyBackupSchedule(int Hour, int Minute)
{
    public static DailyBackupSchedule Parse(string? value)
    {
        if (!TryParse(value, out var schedule, out var error))
            throw new InvalidOperationException(error);

        return schedule;
    }

    public static bool TryParse(string? value, out DailyBackupSchedule schedule, out string error)
    {
        schedule = new DailyBackupSchedule(3, 0);
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(value))
            return true;

        if (TimeSpan.TryParse(value, out var time))
        {
            if (time.TotalDays >= 1)
            {
                error = "Backup:CronSchedule time must be within one day.";
                return false;
            }

            schedule = new DailyBackupSchedule(time.Hours, time.Minutes);
            return true;
        }

        var parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 5)
            return TryParseCronParts(parts[1], parts[0], parts[2], parts[3], parts[4], out schedule, out error);

        if (parts.Length == 6 && parts[0] == "0")
            return TryParseCronParts(parts[2], parts[1], parts[3], parts[4], parts[5], out schedule, out error);

        error = "Backup:CronSchedule must be HH:mm, 'm H * * *', or '0 m H * * *'.";
        return false;
    }

    public DateTimeOffset GetNext(DateTimeOffset now)
    {
        var next = new DateTimeOffset(now.Year, now.Month, now.Day, Hour, Minute, 0, now.Offset);
        return next <= now ? next.AddDays(1) : next;
    }

    private static bool TryParseCronParts(
        string hourPart,
        string minutePart,
        string dayOfMonth,
        string month,
        string dayOfWeek,
        out DailyBackupSchedule schedule,
        out string error)
    {
        schedule = new DailyBackupSchedule(3, 0);
        error = string.Empty;

        if (dayOfMonth != "*" || month != "*" || dayOfWeek != "*")
        {
            error = "Backup:CronSchedule only supports daily schedules with '* * *' for day/month/week.";
            return false;
        }

        if (!int.TryParse(hourPart, out var hour) || hour is < 0 or > 23)
        {
            error = "Backup:CronSchedule hour must be between 0 and 23.";
            return false;
        }

        if (!int.TryParse(minutePart, out var minute) || minute is < 0 or > 59)
        {
            error = "Backup:CronSchedule minute must be between 0 and 59.";
            return false;
        }

        schedule = new DailyBackupSchedule(hour, minute);
        return true;
    }
}
