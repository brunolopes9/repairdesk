using RepairDesk.Core.Abstractions;

namespace RepairDesk.API.HostedServices;

/// <summary>
/// Sprint 598 (Doc 95 §6): no arranque, garante a regra CORS do bucket R2 que deixa o browser
/// enviar e ver vídeos diretamente (URLs pré-assinados). Usa as mesmas origens do CORS da API.
/// Se as credenciais não tiverem permissão, regista o aviso — o resto da app continua normal.
/// </summary>
public sealed class ConteudoStorageCorsHostedService : BackgroundService
{
    private readonly IEnumerable<IDirectAccessStorage> _storage;
    private readonly IReadOnlyCollection<string> _origins;
    private readonly ILogger<ConteudoStorageCorsHostedService> _logger;

    public ConteudoStorageCorsHostedService(
        IEnumerable<IDirectAccessStorage> storage,
        ConteudoCorsOrigins origins,
        ILogger<ConteudoStorageCorsHostedService> logger)
    {
        _storage = storage;
        _origins = origins.Values;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var storage = _storage.FirstOrDefault();
        if (storage is null) return; // dev local: a API faz de proxy
        try
        {
            await storage.EnsureBrowserCorsAsync(_origins, stoppingToken);
            _logger.LogInformation("CORS do storage de conteúdo garantido para {Origins}.", string.Join(", ", _origins));
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Não foi possível configurar o CORS do bucket R2 — uploads diretos de vídeo vão falhar no browser até se definir a regra no painel da Cloudflare.");
        }
    }
}

/// <summary>Origens do frontend (as mesmas do CORS da API).</summary>
public sealed record ConteudoCorsOrigins(IReadOnlyCollection<string> Values);
