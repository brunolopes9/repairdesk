using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using RepairDesk.Core.Abstractions;
using RepairDesk.Infrastructure.At;

namespace RepairDesk.Tests.At;

/// <summary>
/// Sprint 364: o VIES é o backend grátis (sem certificado) que resolve empresas.
/// Sem rede: stub do HttpMessageHandler com respostas reais do VIES REST.
/// </summary>
public class ViesNifRemoteClientTests
{
    private static ViesNifRemoteClient Build(HttpStatusCode code, string json)
    {
        var handler = new StubHandler(code, json);
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://ec.europa.eu/taxation_customs/vies/") };
        return new ViesNifRemoteClient(http, TimeProvider.System, NullLogger<ViesNifRemoteClient>.Instance);
    }

    [Fact]
    public async Task EmpresaValida_DevolveNomeEMoradaNumaLinha()
    {
        var client = Build(HttpStatusCode.OK, """
            { "isValid": true, "userError": "VALID", "name": "EDP, S.A.",
              "address": "AV 24 DE JULHO N 12\nLISBOA\n1249-300 LISBOA" }
            """);

        var result = await client.LookupAsync("500697256");

        result.Should().NotBeNull();
        result!.Nome.Should().Be("EDP, S.A.");
        result.Morada.Should().Be("AV 24 DE JULHO N 12, LISBOA, 1249-300 LISBOA");
        result.Status.Should().Be("confirmed");
    }

    [Fact]
    public async Task ParticularOuInvalido_DevolveNull()
    {
        var client = Build(HttpStatusCode.OK, """
            { "isValid": false, "userError": "INVALID", "name": "", "address": "" }
            """);

        var result = await client.LookupAsync("235061921");

        result.Should().BeNull("nenhum serviço devolve o nome de um particular a partir do NIF");
    }

    [Fact]
    public async Task ServicoIndisponivel_LancaUnavailable()
    {
        var client = Build(HttpStatusCode.OK, """
            { "isValid": false, "userError": "MS_UNAVAILABLE", "name": "", "address": "" }
            """);

        var act = () => client.LookupAsync("500697256");

        await act.Should().ThrowAsync<AtNifUnavailableException>();
    }

    [Fact]
    public async Task Http500_LancaUnavailable()
    {
        var client = Build(HttpStatusCode.InternalServerError, "{}");

        var act = () => client.LookupAsync("500697256");

        await act.Should().ThrowAsync<AtNifUnavailableException>();
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _code;
        private readonly string _json;
        public StubHandler(HttpStatusCode code, string json) { _code = code; _json = json; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(_code)
            {
                Content = new StringContent(_json, Encoding.UTF8, "application/json"),
            });
    }
}
