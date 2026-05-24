using Microsoft.Extensions.Options;
using RepairDesk.Core.Abstractions;

namespace RepairDesk.API.HostedServices;

public sealed class RefreshTokenCleanupOptions
{
    public const string SectionName = "Auth";
    public int RefreshTokenIdleDays { get; set; } = 30;
}

public sealed class RefreshTokenCleanupHostedService : BackgroundService
{
    private static readonly TimeSpan DailyRunUtc = new(4, 0, 0);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<RefreshTokenCleanupOptions> _options;
    private readonly TimeProvider _clock;
    private readonly ILogger<RefreshTokenCleanupHostedService> _logger;

    public RefreshTokenCleanupHostedService(
        IServiceScopeFactory scopeFactory,
        IOptions<RefreshTokenCleanupOptions> options,
        TimeProvider clock,
        ILogger<RefreshTokenCleanupHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _clock = clock;
        _logger = logger;
    }

    public async Task<int> RunOnceAsync(CancellationToken ct = default)
    {
        var idleDays = Math.Max(1, _options.Value.RefreshTokenIdleDays);
        var cutoffUtc = _clock.GetUtcNow().UtcDateTime.AddDays(-idleDays);

        using var scope = _scopeFactory.CreateScope();
        var refresh = scope.ServiceProvider.GetRequiredService<IRefreshTokenService>();
        var revoked = await refresh.RevokeIdleAsync(cutoffUtc, "idle-timeout", ct);

        if (revoked > 0)
            _logger.LogInformation("Revoked {Count} idle refresh tokens older than {CutoffUtc}", revoked, cutoffUtc);

        return revoked;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = DelayUntilNextRun(_clock.GetUtcNow());
            try
            {
                await Task.Delay(delay, stoppingToken);
                await RunOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Refresh token idle cleanup failed");
            }
        }
    }

    internal static TimeSpan DelayUntilNextRun(DateTimeOffset nowUtc)
    {
        var next = nowUtc.Date.Add(DailyRunUtc);
        if (next <= nowUtc.UtcDateTime)
            next = next.AddDays(1);

        return next - nowUtc.UtcDateTime;
    }
}
