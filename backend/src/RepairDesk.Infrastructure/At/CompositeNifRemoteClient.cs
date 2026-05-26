using Microsoft.Extensions.Logging;
using RepairDesk.Core.Abstractions;

namespace RepairDesk.Infrastructure.At;

/// <summary>
/// Sprint 364: encadeia os dois backends de lookup de NIF.
///
/// 1) AT dadosTOI (SOAP, exige certificado do Portal das Finanças) — só quando o
///    certificado está configurado; caso contrário lança AtNifUnavailableException.
/// 2) VIES (REST da Comissão Europeia, grátis, sem certificado) — resolve EMPRESAS
///    registadas em IVA.
///
/// Estratégia: tenta a AT; se devolver dados, usa-os. Se a AT estiver indisponível
/// (sem certificado, típico em localhost) OU não tiver dados, cai para o VIES. Assim
/// o auto-preenchimento de empresas funciona SEM qualquer configuração, e o
/// certificado da AT — quando existir — continua a ter prioridade.
/// </summary>
public sealed class CompositeNifRemoteClient : IAtNifRemoteClient
{
    private readonly AtDadosToiSoapClient _at;
    private readonly ViesNifRemoteClient _vies;
    private readonly ILogger<CompositeNifRemoteClient> _logger;

    public CompositeNifRemoteClient(
        AtDadosToiSoapClient at,
        ViesNifRemoteClient vies,
        ILogger<CompositeNifRemoteClient> logger)
    {
        _at = at;
        _vies = vies;
        _logger = logger;
    }

    public async Task<AtNifLookupResult?> LookupAsync(string nif, CancellationToken ct = default)
    {
        var atUnavailable = false;
        try
        {
            var atResult = await _at.LookupAsync(nif, ct);
            if (atResult is not null)
                return atResult;
        }
        catch (AtNifUnavailableException)
        {
            // Certificado AT ausente/indisponível — esperado em localhost. Cai para o VIES.
            atUnavailable = true;
        }

        try
        {
            return await _vies.LookupAsync(nif, ct);
        }
        catch (AtNifUnavailableException) when (atUnavailable)
        {
            // Ambos indisponíveis: propaga (controller → 503).
            _logger.LogWarning("Lookup de NIF indisponível: AT e VIES falharam.");
            throw;
        }
    }
}
