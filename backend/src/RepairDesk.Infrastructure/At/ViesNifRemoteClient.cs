using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using RepairDesk.Common.Helpers;
using RepairDesk.Core.Abstractions;

namespace RepairDesk.Infrastructure.At;

/// <summary>
/// Sprint 364: lookup de NIF via VIES (VAT Information Exchange System da Comissão
/// Europeia). É grátis, público e NÃO exige certificado — ao contrário do webservice
/// dadosTOI da AT. Devolve nome + morada SÓ para entidades registadas em IVA (empresas,
/// NIF começado por 5/6/9); para pessoas singulares devolve isValid=false → null, o que
/// o controller traduz em 404 ("NIF válido localmente, sem confirmação"). Isto é o
/// melhor possível: nenhum serviço devolve o nome de um particular a partir do NIF.
/// </summary>
public sealed class ViesNifRemoteClient : IAtNifRemoteClient
{
    private readonly HttpClient _http;
    private readonly TimeProvider _clock;
    private readonly ILogger<ViesNifRemoteClient> _logger;

    public ViesNifRemoteClient(HttpClient http, TimeProvider clock, ILogger<ViesNifRemoteClient> logger)
    {
        _http = http;
        _clock = clock;
        _logger = logger;
    }

    private sealed record ViesResponse(
        [property: JsonPropertyName("isValid")] bool IsValid,
        [property: JsonPropertyName("userError")] string? UserError,
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("address")] string? Address);

    // Erros do VIES que significam "serviço indisponível" (não "NIF inexistente").
    private static readonly HashSet<string> UnavailableErrors = new(StringComparer.OrdinalIgnoreCase)
    {
        "MS_UNAVAILABLE", "MS_MAX_CONCURRENT_REQ", "GLOBAL_MAX_CONCURRENT_REQ",
        "SERVICE_UNAVAILABLE", "TIMEOUT", "SERVER_BUSY",
    };

    public async Task<AtNifLookupResult?> LookupAsync(string nif, CancellationToken ct = default)
    {
        var clean = NifValidator.Normalize(nif);

        ViesResponse? body;
        try
        {
            // VIES exige o código de país (PT) separado do número.
            using var resp = await _http.GetAsync($"rest-api/ms/PT/vat/{clean}", ct);
            if (resp.StatusCode is HttpStatusCode.InternalServerError or HttpStatusCode.ServiceUnavailable
                or HttpStatusCode.BadGateway or HttpStatusCode.GatewayTimeout)
            {
                throw new AtNifUnavailableException($"VIES respondeu {(int)resp.StatusCode}.");
            }
            resp.EnsureSuccessStatusCode();
            body = await resp.Content.ReadFromJsonAsync<ViesResponse>(ct);
        }
        catch (AtNifUnavailableException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new AtNifUnavailableException("VIES NIF lookup indisponível.", ex);
        }

        if (body is null)
            throw new AtNifUnavailableException("VIES devolveu uma resposta vazia.");

        if (!body.IsValid)
        {
            if (!string.IsNullOrWhiteSpace(body.UserError) && UnavailableErrors.Contains(body.UserError))
                throw new AtNifUnavailableException($"VIES indisponível ({body.UserError}).");

            // INVALID → particular ou NIF não-IVA. Não é erro: simplesmente não há dados.
            _logger.LogInformation("VIES sem dados para {MaskedNif} ({Error})", AtNifLookupService.MaskNif(clean), body.UserError);
            return null;
        }

        if (string.IsNullOrWhiteSpace(body.Name))
            return null;

        return new AtNifLookupResult(
            clean,
            body.Name.Trim(),
            NormalizeAddress(body.Address),
            "confirmed",
            _clock.GetUtcNow());
    }

    private static string? NormalizeAddress(string? address)
    {
        if (string.IsNullOrWhiteSpace(address)) return null;
        // VIES separa linhas com \n; juntar numa linha legível.
        var parts = address.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length == 0 ? null : string.Join(", ", parts);
    }
}
