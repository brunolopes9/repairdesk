using System.Diagnostics;
using System.Net;
using System.Text;
using Microsoft.Extensions.Options;

namespace RepairDesk.API.Infrastructure;

public static class MetricsEndpoint
{
    public static void Map(WebApplication app)
    {
        if (!app.Configuration.GetValue("Metrics:Enabled", false))
            return;

        app.MapGet("/api/metrics", HandleAsync)
            .AllowAnonymous()
            .WithName("PrometheusMetrics");
    }

    private static async Task<IResult> HandleAsync(HttpContext context, IOptions<MetricsOptions> options)
    {
        if (!IsAuthorized(context, options.Value))
            return Results.Unauthorized();

        var process = Process.GetCurrentProcess();
        var uptimeSeconds = (DateTimeOffset.UtcNow - Process.GetCurrentProcess().StartTime.ToUniversalTime()).TotalSeconds;
        var content = new StringBuilder();

        content.AppendLine("# HELP repairdesk_process_uptime_seconds Process uptime in seconds.");
        content.AppendLine("# TYPE repairdesk_process_uptime_seconds gauge");
        content.AppendLine($"repairdesk_process_uptime_seconds {uptimeSeconds:F0}");
        content.AppendLine("# HELP repairdesk_process_private_memory_bytes Private memory used by the API process.");
        content.AppendLine("# TYPE repairdesk_process_private_memory_bytes gauge");
        content.AppendLine($"repairdesk_process_private_memory_bytes {process.PrivateMemorySize64}");
        content.AppendLine("# HELP repairdesk_process_thread_count Current process thread count.");
        content.AppendLine("# TYPE repairdesk_process_thread_count gauge");
        content.AppendLine($"repairdesk_process_thread_count {process.Threads.Count}");

        await Task.CompletedTask;
        return Results.Text(content.ToString(), "text/plain; version=0.0.4; charset=utf-8");
    }

    private static bool IsAuthorized(HttpContext context, MetricsOptions options)
    {
        if (IsIpAllowed(context.Connection.RemoteIpAddress, options.AllowedIps))
            return true;

        if (string.IsNullOrWhiteSpace(options.BasicAuthUsername) ||
            string.IsNullOrWhiteSpace(options.BasicAuthPassword))
            return false;

        var header = context.Request.Headers.Authorization.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(header) || !header.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
            return false;

        try
        {
            var raw = Encoding.UTF8.GetString(Convert.FromBase64String(header["Basic ".Length..].Trim()));
            var separator = raw.IndexOf(':', StringComparison.Ordinal);
            if (separator <= 0)
                return false;

            var username = raw[..separator];
            var password = raw[(separator + 1)..];
            return FixedTimeEquals(username, options.BasicAuthUsername) &&
                   FixedTimeEquals(password, options.BasicAuthPassword);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static bool IsIpAllowed(IPAddress? remoteIp, string[] allowedIps)
    {
        if (remoteIp is null || allowedIps.Length == 0)
            return false;

        return allowedIps.Any(ip => string.Equals(ip, remoteIp.ToString(), StringComparison.OrdinalIgnoreCase));
    }

    private static bool FixedTimeEquals(string left, string right)
    {
        var leftBytes = Encoding.UTF8.GetBytes(left);
        var rightBytes = Encoding.UTF8.GetBytes(right);
        return System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
    }
}
