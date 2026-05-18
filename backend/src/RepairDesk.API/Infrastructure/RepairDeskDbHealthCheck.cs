using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using RepairDesk.DAL.Persistence;

namespace RepairDesk.API.Infrastructure;

public sealed class RepairDeskDbHealthCheck : CachedHealthCheck
{
    private readonly IServiceScopeFactory _scopeFactory;

    public RepairDeskDbHealthCheck(IServiceScopeFactory scopeFactory, TimeProvider clock)
        : base(clock)
    {
        _scopeFactory = scopeFactory;
    }

    protected override async Task<HealthCheckResult> CheckUncachedAsync(HealthCheckContext context, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var provider = db.Database.ProviderName ?? "unknown";

        if (db.Database.IsRelational())
        {
            await db.Database.ExecuteSqlRawAsync("SELECT 1", cancellationToken);
        }
        else if (!await db.Database.CanConnectAsync(cancellationToken))
        {
            return HealthCheckResult.Unhealthy("Database is not reachable.", data: Data(provider));
        }

        return HealthCheckResult.Healthy("Database is reachable.", Data(provider));
    }

    private static IReadOnlyDictionary<string, object> Data(string provider) =>
        new Dictionary<string, object>
        {
            ["provider"] = provider
        };
}
