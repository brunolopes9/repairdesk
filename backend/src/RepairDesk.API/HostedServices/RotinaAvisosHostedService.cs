using RepairDesk.Services.Rotina;

namespace RepairDesk.API.HostedServices;

/// <summary>
/// Sprint 615 (Doc 98): de minuto a minuto envia os avisos da rotina pessoal (antes, à hora, insistência,
/// fim de bloco). A lógica e a idempotência estão em <see cref="RotinaAvisosService"/>. Config: Rotina:Avisos:Enabled.
/// </summary>
public sealed class RotinaAvisosHostedService : BackgroundService
{
    private static readonly TimeSpan Intervalo = TimeSpan.FromMinutes(1);

    private readonly IServiceScopeFactory _scopes;
    private readonly IConfiguration _config;
    private readonly ILogger<RotinaAvisosHostedService> _logger;

    public RotinaAvisosHostedService(IServiceScopeFactory scopes, IConfiguration config, ILogger<RotinaAvisosHostedService> logger)
    {
        _scopes = scopes;
        _config = config;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_config.GetValue("Rotina:Avisos:Enabled", true))
        {
            _logger.LogInformation("Avisos da rotina desativados por config.");
            return;
        }
        try { await Task.Delay(TimeSpan.FromSeconds(40), stoppingToken); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopes.CreateScope();
                var avisos = scope.ServiceProvider.GetRequiredService<IRotinaAvisosService>();
                var n = await avisos.ProcessarAsync(DateTime.UtcNow, stoppingToken);
                if (n > 0) _logger.LogInformation("Rotina: {N} aviso(s) enviados.", n);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { _logger.LogError(ex, "Avisos da rotina falharam — tenta no próximo minuto."); }

            try { await Task.Delay(Intervalo, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }
}
