using RepairDesk.Services.Appointments;

namespace RepairDesk.API.HostedServices;

/// <summary>
/// Sprint 611: de minuto a minuto envia os avisos da agenda (lembrete 1 h antes, resumo do dia
/// às 08:00 e da semana à segunda). A lógica e a idempotência estão em <see cref="AgendaAvisosService"/>.
/// Config: Agenda:Avisos:Enabled (default true).
/// </summary>
public sealed class AgendaAvisosHostedService : BackgroundService
{
    private static readonly TimeSpan Intervalo = TimeSpan.FromMinutes(1);

    private readonly IServiceScopeFactory _scopes;
    private readonly IConfiguration _config;
    private readonly ILogger<AgendaAvisosHostedService> _logger;

    public AgendaAvisosHostedService(IServiceScopeFactory scopes, IConfiguration config, ILogger<AgendaAvisosHostedService> logger)
    {
        _scopes = scopes;
        _config = config;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_config.GetValue("Agenda:Avisos:Enabled", true))
        {
            _logger.LogInformation("Avisos da agenda desativados por config.");
            return;
        }
        try { await Task.Delay(TimeSpan.FromSeconds(50), stoppingToken); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopes.CreateScope();
                var avisos = scope.ServiceProvider.GetRequiredService<IAgendaAvisosService>();
                var r = await avisos.ProcessarAsync(DateTime.UtcNow, stoppingToken);
                if (r.Lembretes + r.ResumosDia + r.ResumosSemana > 0)
                    _logger.LogInformation("Agenda: {L} lembrete(s), {D} resumo(s) do dia, {S} da semana.", r.Lembretes, r.ResumosDia, r.ResumosSemana);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { _logger.LogError(ex, "Avisos da agenda falharam — tenta no próximo minuto."); }

            try { await Task.Delay(Intervalo, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }
}
