using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace RepairDesk.API.Infrastructure;

public static class HealthCheckJsonResponseWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    public static async Task WriteAsync(HttpContext context, HealthReport report)
    {
        var env = context.RequestServices.GetRequiredService<IHostEnvironment>();
        var includeErrors = env.IsDevelopment() || env.IsEnvironment("Testing");

        context.Response.ContentType = "application/json";
        context.Response.Headers.CacheControl = "private, max-age=5";

        var payload = new
        {
            status = report.Status.ToString(),
            totalDurationMs = Math.Round(report.TotalDuration.TotalMilliseconds, 2),
            checkedAtUtc = DateTimeOffset.UtcNow,
            entries = report.Entries.ToDictionary(
                entry => entry.Key,
                entry => new
                {
                    status = entry.Value.Status.ToString(),
                    description = entry.Value.Description,
                    durationMs = Math.Round(entry.Value.Duration.TotalMilliseconds, 2),
                    data = entry.Value.Data,
                    error = includeErrors ? entry.Value.Exception?.Message : entry.Value.Exception is null ? null : "check_failed"
                })
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(payload, JsonOptions));
    }

    public static HealthCheckOptions OptionsForTag(string tag, bool forceHealthyStatusCode = false)
    {
        var options = new HealthCheckOptions
        {
            Predicate = registration => registration.Tags.Contains(tag),
            ResponseWriter = WriteAsync
        };

        if (forceHealthyStatusCode)
        {
            options.ResultStatusCodes[HealthStatus.Healthy] = StatusCodes.Status200OK;
            options.ResultStatusCodes[HealthStatus.Degraded] = StatusCodes.Status200OK;
            options.ResultStatusCodes[HealthStatus.Unhealthy] = StatusCodes.Status200OK;
        }

        return options;
    }
}
