using Microsoft.Extensions.Diagnostics.HealthChecks;
using RepairDesk.Core.Abstractions;

namespace RepairDesk.API.Infrastructure;

public sealed class PhotoStorageHealthCheck : CachedHealthCheck
{
    private readonly IPhotoStorage _storage;
    private readonly IConfiguration _configuration;

    public PhotoStorageHealthCheck(IPhotoStorage storage, IConfiguration configuration, TimeProvider clock)
        : base(clock)
    {
        _storage = storage;
        _configuration = configuration;
    }

    protected override async Task<HealthCheckResult> CheckUncachedAsync(HealthCheckContext context, CancellationToken cancellationToken)
    {
        var provider = _configuration["Storage:Provider"]?.Trim().ToLowerInvariant() ?? "local";
        var key = $"health/{Guid.NewGuid():N}.probe";

        try
        {
            await using var content = new MemoryStream(new byte[] { 1 });
            await _storage.UploadAsync(key, content, "application/octet-stream", cancellationToken);

            if (!await _storage.ExistsAsync(key, cancellationToken))
                return HealthCheckResult.Unhealthy("Storage write succeeded but probe was not found.", data: Data(provider));

            return HealthCheckResult.Healthy("Storage upload/delete probe succeeded.", Data(provider));
        }
        finally
        {
            await _storage.DeleteAsync(key, cancellationToken);
        }
    }

    private static IReadOnlyDictionary<string, object> Data(string provider) =>
        new Dictionary<string, object>
        {
            ["provider"] = provider
        };
}
