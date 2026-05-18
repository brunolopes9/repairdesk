using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RepairDesk.Common.Helpers;
using RepairDesk.Core.Abstractions;

namespace RepairDesk.Infrastructure.At;

public sealed class AtNifLookupService : IAtNifLookupService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IDistributedCache _cache;
    private readonly IAtNifRemoteClient _remoteClient;
    private readonly ITenantContext _tenant;
    private readonly TimeProvider _clock;
    private readonly AtNifLookupOptions _options;
    private readonly ILogger<AtNifLookupService> _logger;

    public AtNifLookupService(
        IDistributedCache cache,
        IAtNifRemoteClient remoteClient,
        ITenantContext tenant,
        TimeProvider clock,
        IOptions<AtNifLookupOptions> options,
        ILogger<AtNifLookupService> logger)
    {
        _cache = cache;
        _remoteClient = remoteClient;
        _tenant = tenant;
        _clock = clock;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<AtNifLookupResult?> LookupAsync(string nif, CancellationToken ct = default)
    {
        var clean = NifValidator.Normalize(nif);
        if (!NifValidator.IsValid(clean))
            return null;

        var cacheKey = CacheKey(clean);
        var cached = await _cache.GetStringAsync(cacheKey, ct);
        if (!string.IsNullOrWhiteSpace(cached))
        {
            _logger.LogInformation("AT NIF lookup cache hit for {MaskedNif}", MaskNif(clean));
            return JsonSerializer.Deserialize<AtNifLookupResult>(cached, JsonOptions);
        }

        await EnforceRateLimitAsync(clean, ct);

        _logger.LogInformation("AT NIF lookup cache miss for {MaskedNif}", MaskNif(clean));
        var result = await _remoteClient.LookupAsync(clean, ct);
        if (result is null)
            return null;

        var ttl = TimeSpan.FromDays(Math.Clamp(_options.CacheTtlDays, 1, 365));
        await _cache.SetStringAsync(
            cacheKey,
            JsonSerializer.Serialize(result, JsonOptions),
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = ttl },
            ct);

        return result;
    }

    private async Task EnforceRateLimitAsync(string nif, CancellationToken ct)
    {
        var limit = Math.Max(1, _options.MaxDailyCallsPerTenant);
        var key = RateKey();
        var raw = await _cache.GetStringAsync(key, ct);
        var current = int.TryParse(raw, out var parsed) ? parsed : 0;
        if (current >= limit)
        {
            _logger.LogWarning(
                "AT NIF lookup rate limit exceeded for tenant {TenantId} after checking {MaskedNif}",
                _tenant.TenantId,
                MaskNif(nif));
            throw new AtNifRateLimitExceededException(limit);
        }

        var expiresAt = NextUtcMidnight();
        await _cache.SetStringAsync(
            key,
            (current + 1).ToString(System.Globalization.CultureInfo.InvariantCulture),
            new DistributedCacheEntryOptions { AbsoluteExpiration = expiresAt },
            ct);
    }

    private DateTimeOffset NextUtcMidnight()
    {
        var now = _clock.GetUtcNow();
        return new DateTimeOffset(now.UtcDateTime.Date.AddDays(1), TimeSpan.Zero);
    }

    private string RateKey()
    {
        var tenant = _tenant.TenantId?.ToString("N") ?? "no-tenant";
        var day = _clock.GetUtcNow().ToString("yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture);
        return $"at:nif:quota:{tenant}:{day}";
    }

    public static string CacheKey(string nif) => $"at:nif:{nif}";

    public static string MaskNif(string nif)
    {
        var clean = NifValidator.Normalize(nif);
        return clean.Length <= 4 ? "****" : new string('*', clean.Length - 4) + clean[^4..];
    }
}
