using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using RepairDesk.Core.Abstractions;
using RepairDesk.API.Backups;
using RepairDesk.DAL.Persistence;

namespace RepairDesk.Tests.Backups;

public class BackupHostedServiceTests
{
    [Fact]
    public void Options_DefaultToDisabledDailyAtThree()
    {
        var options = BackupOptions.FromConfiguration(BuildConfig(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Default"] = "Server=db;Database=RepairDesk;",
        }));

        options.Enabled.Should().BeFalse();
        options.CronSchedule.Should().Be("0 3 * * *");
        options.RetentionDays.Should().Be(30);
        options.LocalPath.Should().Be("/backups");
        options.DatabaseName.Should().Be("RepairDesk");
        options.ValidateValues().Should().BeEmpty();
    }

    [Fact]
    public void Options_UseBackupBucket_AndStorageR2Credentials()
    {
        var options = BackupOptions.FromConfiguration(BuildConfig(new Dictionary<string, string?>
        {
            ["Storage:R2:AccountId"] = "account",
            ["Storage:R2:AccessKey"] = "access",
            ["Storage:R2:Secret"] = "secret",
            ["Storage:R2:Bucket"] = "media-bucket",
            ["Backup:R2:Bucket"] = "backup-bucket",
            ["Backup:R2:Prefix"] = "daily/sql",
        }));

        options.R2.AccountId.Should().Be("account");
        options.R2.AccessKey.Should().Be("access");
        options.R2.Secret.Should().Be("secret");
        options.R2.Bucket.Should().Be("backup-bucket");
        options.R2.BuildKey("repairdesk-20260518-0300.bak")
            .Should().Be("daily/sql/repairdesk-20260518-0300.bak");
    }

    [Theory]
    [InlineData("03:00", 3, 0)]
    [InlineData("0 3 * * *", 3, 0)]
    [InlineData("15 4 * * *", 4, 15)]
    [InlineData("0 30 5 * * *", 5, 30)]
    public void DailySchedule_ParsesSupportedFormats(string value, int hour, int minute)
    {
        var schedule = DailyBackupSchedule.Parse(value);

        schedule.Hour.Should().Be(hour);
        schedule.Minute.Should().Be(minute);
    }

    [Fact]
    public async Task BackupService_CreatesLocalFile_AndAppliesRetention_WithoutR2()
    {
        var temp = Path.Combine(Path.GetTempPath(), $"repairdesk-backup-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temp);
        try
        {
            File.WriteAllText(Path.Combine(temp, "repairdesk-20260401-0300.bak"), "old");
            File.WriteAllText(Path.Combine(temp, "repairdesk-20260517-0300.bak"), "recent");

            var executor = new FakeSqlServerBackupExecutor();
            var remote = new FakeRemoteStorage();
            var services = new ServiceCollection();
            services.AddSingleton<ITenantContext>(new TestTenantContext());
            services.AddDbContext<AppDbContext>(opt => opt.UseInMemoryDatabase($"backup-test-{Guid.NewGuid():N}"));
            var serviceProvider = services.BuildServiceProvider();
            var service = new BackupService(
                BuildConfig(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:Default"] = "Server=db;Database=RepairDesk;",
                    ["Backup:LocalPath"] = temp,
                    ["Backup:RetentionDays"] = "30",
                    ["Backup:DatabaseName"] = "RepairDesk",
                }),
                executor,
                remote,
                new BackupFileSystem(),
                new FrozenTimeProvider(new DateTimeOffset(2026, 5, 18, 3, 0, 0, TimeSpan.Zero)),
                serviceProvider.GetRequiredService<IServiceScopeFactory>(),
                NullLogger<BackupService>.Instance);

            var result = await service.RunBackupAsync(BackupTrigger.Manual);

            result.FileName.Should().Be("repairdesk-20260518-0300.bak");
            result.UploadedToR2.Should().BeFalse();
            result.DeletedLocalFiles.Should().Be(1);
            result.Status.Should().Be("completed_local_only");
            File.Exists(Path.Combine(temp, "repairdesk-20260518-0300.bak")).Should().BeTrue();
            File.Exists(Path.Combine(temp, "repairdesk-20260401-0300.bak")).Should().BeFalse();
            File.Exists(Path.Combine(temp, "repairdesk-20260517-0300.bak")).Should().BeTrue();
            executor.Requests.Should().ContainSingle();
            remote.UploadCalls.Should().Be(0);
        }
        finally
        {
            if (Directory.Exists(temp))
                Directory.Delete(temp, recursive: true);
        }
    }

    private static IConfigurationRoot BuildConfig(Dictionary<string, string?> values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

    private sealed class FakeSqlServerBackupExecutor : ISqlServerBackupExecutor
    {
        public List<BackupExecutionRequest> Requests { get; } = [];

        public Task CreateBackupAsync(BackupExecutionRequest request, CancellationToken ct = default)
        {
            Requests.Add(request);
            Directory.CreateDirectory(Path.GetDirectoryName(request.LocalBackupPath)!);
            File.WriteAllText(request.LocalBackupPath, "fake backup");
            return Task.CompletedTask;
        }

        public Task RestoreBackupAsync(BackupRestoreExecutionRequest request, CancellationToken ct = default)
            => Task.CompletedTask;
    }

    private sealed class FakeRemoteStorage : IBackupRemoteStorage
    {
        public int UploadCalls { get; private set; }
        public bool IsConfigured => false;

        public Task<string> UploadAsync(string localPath, string fileName, BackupR2Options options, CancellationToken ct = default)
        {
            UploadCalls++;
            return Task.FromResult(options.BuildKey(fileName));
        }

        public Task DownloadAsync(string r2Key, string destinationPath, BackupR2Options options, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task<IReadOnlyList<BackupFileDto>> ListAsync(BackupR2Options options, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<BackupFileDto>>([]);
    }

    private sealed class TestTenantContext : ITenantContext
    {
        public Guid? TenantId => null;
        public bool HasTenant => false;
    }

    private sealed class FrozenTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _now;

        public FrozenTimeProvider(DateTimeOffset now)
        {
            _now = now;
        }

        public override DateTimeOffset GetUtcNow() => _now.ToUniversalTime();

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }
}
