using System.Net.Http.Json;
using RepairDesk.Core.Enums;
using RepairDesk.Services.Clientes;
using RepairDesk.Services.Vendas;

namespace RepairDesk.Tests.Support;

/// <summary>Doc 94 Fase 4c: uma "reparação" é uma Venda do tipo Reparação — helper para os testes de API.</summary>
public static class TestReparacoes
{
    public static async Task<VendaDto> CriarAsync(
        HttpClient client,
        string equipamento = "iPhone 13",
        string problema = "Ecrã partido",
        int valorCents = 7000,
        Guid? clienteId = null,
        VendaEstado estado = VendaEstado.Orcamento)
    {
        if (clienteId is null)
        {
            var cli = await client.PostAsJsonAsync("/api/clientes",
                new CreateClienteRequest("Cliente Rep " + Guid.NewGuid().ToString("N")[..6], "9" + Random.Shared.Next(10000000, 99999999), null, null, null));
            cli.EnsureSuccessStatusCode();
            clienteId = (await cli.Content.ReadFromJsonAsync<ClienteDto>())!.Id;
        }

        var resp = await client.PostAsJsonAsync("/api/vendas", new VendaWriteRequest(
            VendaTipo.Reparacao, clienteId, equipamento, problema, null,
            valorCents > 0 ? [new VendaLinhaWriteRequest(null, null, "Mão de obra", 1, valorCents)] : [],
            estado));
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<VendaDto>())!;
    }
}
