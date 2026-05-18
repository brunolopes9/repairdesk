using Microsoft.Extensions.Diagnostics.HealthChecks;
using RepairDesk.API.Backups;

namespace RepairDesk.API.Infrastructure;

public sealed class BackupHealthCheck : CachedHealthCheck
{
    private static readonly TimeSpan MaxAge = TimeSpan.FromHours(26);
    private static readonly DateTimeOffset ProcessStartedAt = DateTimeOffset.UtcNow;
    private readonly IBackupService _backup;
    private readonly IBackupFileSystem _files;
    private readonly TimeProvider _clock;

    public BackupHealthCheck(IBackupService backup, IBackupFileSystem files, TimeProvider clock)
        : base(clock)
    {
        _backup = backup;
        _files = files;
        _clock = clock;
    }

    protected override Task<HealthCheckResult> CheckUncachedAsync(HealthCheckContext context, CancellationToken cancellationToken)
    {
        var options = _backup.GetOptions();
        if (!options.Enabled)
            return Task.FromResult(HealthCheckResult.Healthy("Backup scheduler is disabled.", Data("disabled", null, null)));

        var local = _files.ListLocalBackups(options.LocalPath);
        var latest = local.Count == 0 ? (DateTimeOffset?)null : local.Max(b => b.Timestamp);
        if (latest is null)
        {
            var uptime = _clock.GetUtcNow() - ProcessStartedAt;
            if (uptime <= MaxAge)
                return Task.FromResult(HealthCheckResult.Healthy("No local backup found yet; initial 26h window is still open.", Data("pending_initial_backup", null, uptime)));

            return Task.FromResult(HealthCheckResult.Unhealthy("No local backup found.", data: Data("missing", null, null)));
        }

        var age = _clock.GetUtcNow() - latest.Value.ToUniversalTime();
        if (age > MaxAge)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy(
                "Latest local backup is older than 26 hours.",
                data: Data("stale", latest, age)));
        }

        return Task.FromResult(HealthCheckResult.Healthy(
            "Latest local backup is recent.",
            Data("ok", latest, age)));
    }

    private static IReadOnlyDictionary<string, object> Data(string status, DateTimeOffset? latest, TimeSpan? age) =>
        new Dictionary<string, object>
        {
            ["status"] = status,
            ["latestLocalBackupAt"] = latest?.ToString("O") ?? string.Empty,
            ["ageHours"] = age?.TotalHours ?? 0,
            ["maxAgeHours"] = MaxAge.TotalHours,
        };
}
