using RepairDesk.Core.Abstractions;

namespace RepairDesk.API.HostedServices;

/// <summary>
/// 23/11: retenção das visitas do site — eventos com mais de 13 meses apagam-se uma vez por dia (RGPD: minimização;
/// 13 meses deixa comparar o mesmo mês do ano anterior).
/// </summary>
public sealed class VisitasSiteLimpezaHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly IConfiguration _config;
    private readonly ILogger<VisitasSiteLimpezaHostedService> _log;

    public VisitasSiteLimpezaHostedService(IServiceScopeFactory scopes, IConfiguration config, ILogger<VisitasSiteLimpezaHostedService> log)
    {
        _scopes = scopes;
        _config = config;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_config.GetValue("E2E:Enabled", false)) return;
        try { await Task.Delay(TimeSpan.FromMinutes(15), stoppingToken); }
        catch (OperationCanceledException) { return; }
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopes.CreateScope();
                var repo = scope.ServiceProvider.GetRequiredService<IVisitasSiteRepository>();
                var n = await repo.ApagarAntesDeAsync(DateTime.UtcNow.AddMonths(-13), stoppingToken);
                if (n > 0) _log.LogInformation("Visitas do site: {N} evento(s) com mais de 13 meses apagados.", n);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { _log.LogError(ex, "Limpeza das visitas do site falhou — tenta amanhã."); }
            try { await Task.Delay(TimeSpan.FromHours(24), stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }
}
