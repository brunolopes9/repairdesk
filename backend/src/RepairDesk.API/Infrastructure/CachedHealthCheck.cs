using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace RepairDesk.API.Infrastructure;

public abstract class CachedHealthCheck : IHealthCheck
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(5);

    private readonly TimeProvider _clock;
    private readonly object _gate = new();
    private CacheEntry? _cached;

    protected CachedHealthCheck(TimeProvider clock)
    {
        _clock = clock;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var now = _clock.GetUtcNow();
        lock (_gate)
        {
            if (_cached is not null && _cached.ExpiresAt > now)
                return _cached.Result;
        }

        HealthCheckResult result;
        try
        {
            result = await CheckUncachedAsync(context, cancellationToken);
        }
        catch (Exception ex)
        {
            result = HealthCheckResult.Unhealthy("Check failed.", ex);
        }

        lock (_gate)
        {
            _cached = new CacheEntry(result, now.Add(CacheTtl));
        }

        return result;
    }

    protected abstract Task<HealthCheckResult> CheckUncachedAsync(HealthCheckContext context, CancellationToken cancellationToken);

    private sealed record CacheEntry(HealthCheckResult Result, DateTimeOffset ExpiresAt);
}
