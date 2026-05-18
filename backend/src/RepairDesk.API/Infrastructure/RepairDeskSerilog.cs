using Serilog;
using Serilog.Events;
using Serilog.Formatting.Json;

namespace RepairDesk.API.Infrastructure;

public static class RepairDeskSerilog
{
    private const string DevTemplate =
        "[{Timestamp:HH:mm:ss} {Level:u3}] [{CorrelationId}] {Message:lj} {Properties:j}{NewLine}{Exception}";

    private const string FileTemplate =
        "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] [{MachineName}/{ThreadId}] [{CorrelationId}] {Message:lj} {Properties:j}{NewLine}{Exception}";

    public static Serilog.ILogger CreateBootstrapLogger()
    {
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        var production = string.Equals(environment, Environments.Production, StringComparison.OrdinalIgnoreCase);

        var config = new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.WithMachineName()
            .Enrich.WithThreadId();

        if (production)
        {
            config.WriteTo.Console(new JsonFormatter(renderMessage: true));
        }
        else
        {
            config.WriteTo.Console(outputTemplate: DevTemplate);
        }

        return config.CreateLogger();
    }

    public static void Configure(HostBuilderContext context, IServiceProvider services, LoggerConfiguration config)
    {
        var production = context.HostingEnvironment.IsProduction();
        var httpContextAccessor = services.GetService<IHttpContextAccessor>();

        config
            .ReadFrom.Configuration(context.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .Enrich.WithMachineName()
            .Enrich.WithThreadId()
            .Enrich.With(new TenantIdLogEventEnricher(httpContextAccessor));

        if (production)
        {
            config.WriteTo.Console(new JsonFormatter(renderMessage: true));
        }
        else
        {
            config.WriteTo.Console(outputTemplate: DevTemplate);
            config.WriteTo.File(
                Environment.GetEnvironmentVariable("REPAIRDESK_LOG_FILE") ?? "logs/repairdesk-.log",
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14,
                outputTemplate: FileTemplate);
        }
    }
}
