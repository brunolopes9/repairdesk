using RepairDesk.Services.Conteudo.Redes;

namespace RepairDesk.API.HostedServices;

/// <summary>
/// Sprint 599 (Doc 95 Fase B): de minuto a minuto publica o conteúdo aprovado que chegou à hora
/// (só tenants com «Publicação automática» ligada) e, uma vez por dia, renova os tokens do Threads.
/// Config: Conteudo:Publicador:Enabled (default true).
/// </summary>
public sealed class ConteudoPublicadorHostedService : BackgroundService
{
    private static readonly TimeSpan Intervalo = TimeSpan.FromMinutes(1);

    private readonly IServiceScopeFactory _scopes;
    private readonly IConfiguration _config;
    private readonly ILogger<ConteudoPublicadorHostedService> _logger;
    private DateTime _ultimaRenovacao = DateTime.MinValue;

    public ConteudoPublicadorHostedService(IServiceScopeFactory scopes, IConfiguration config, ILogger<ConteudoPublicadorHostedService> logger)
    {
        _scopes = scopes;
        _config = config;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_config.GetValue("Conteudo:Publicador:Enabled", true))
        {
            _logger.LogInformation("Publicador de conteúdo desativado por config.");
            return;
        }
        try { await Task.Delay(TimeSpan.FromSeconds(45), stoppingToken); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopes.CreateScope();
                var publicador = scope.ServiceProvider.GetRequiredService<IConteudoPublicadorService>();
                await publicador.PublicarPendentesAsync(stoppingToken);
                if (DateTime.UtcNow - _ultimaRenovacao > TimeSpan.FromHours(24))
                {
                    await publicador.RenovarTokensAsync(stoppingToken);
                    _ultimaRenovacao = DateTime.UtcNow;
                }
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { _logger.LogError(ex, "Publicador de conteúdo falhou — tenta no próximo minuto."); }

            try { await Task.Delay(Intervalo, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }
}
