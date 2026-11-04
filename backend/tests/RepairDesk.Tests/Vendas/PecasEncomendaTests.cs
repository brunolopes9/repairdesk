using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using RepairDesk.API.Infrastructure;
using RepairDesk.Core.Enums;
using RepairDesk.Services.Clientes;
using RepairDesk.Services.Compras;
using RepairDesk.Services.Fornecedores;
using RepairDesk.Services.Vendas;
using RepairDesk.Tests.Auth;

namespace RepairDesk.Tests.Vendas;

/// <summary>
/// Sprint 594: a fila de reparações — o iPad da cliente fica registado com a peça que falta (custo estimado e preço),
/// aparece em "Peças a encomendar" e, quando a peça chega e entra no stock, a linha passa a usar o lote.
/// </summary>
public class PecasEncomendaTests : IClassFixture<RepairDeskApiFactory>
{
    private readonly RepairDeskApiFactory _factory;
    public PecasEncomendaTests(RepairDeskApiFactory factory) => _factory = factory;

    [Fact]
    public async Task ReparacaoComPecaPorEncomendar_MostraLucroEstimado_EntraNaLista_EPassaAoStockQuandoChega()
    {
        var client = await AuthedClient();
        var cliente = (await (await client.PostAsJsonAsync("/api/clientes", new CreateClienteRequest("Cliente iPad", "912345671", null, null, null)))
            .Content.ReadFromJsonAsync<ClienteDto>())!.Id;
        var ecra = $"Ecrã iPad 9 {Guid.NewGuid():N}"[..20];

        var criada = await client.PostAsJsonAsync("/api/vendas", new VendaWriteRequest(VendaTipo.Reparacao, cliente, "iPad 9", "Ecrã partido", null,
            [new VendaLinhaWriteRequest(null, null, ecra, 1, 6000, Encomenda: PecaEncomendaEstado.PorEncomendar, CustoEstimadoCents: 3000, EncomendaNota: "LC Phones")],
            VendaEstado.AEsperaPeca, null));
        criada.EnsureSuccessStatusCode();
        var venda = (await criada.Content.ReadFromJsonAsync<VendaDto>())!;
        var linha = venda.Items.Single();
        linha.Encomenda.Should().Be(PecaEncomendaEstado.PorEncomendar);
        // 60 € c/ IVA → 48,78 € s/ IVA; a peça ~30 € c/ IVA → 24,39 € s/ IVA. Lucro ≈ 24,39 €.
        venda.Lucro.Should().BeApproximately(24.39m, 0.01m);

        var pecas = (await client.GetFromJsonAsync<List<PecaEncomendaDto>>("/api/vendas/pecas-encomenda"))!;
        var peca = pecas.Single(p => p.ItemId == linha.Id);
        peca.Cliente.Should().Be("Cliente iPad");
        peca.CustoEstimado.Should().Be(30m);
        peca.Nota.Should().Be("LC Phones");

        (await client.PostAsJsonAsync($"/api/vendas/{venda.Id}/itens/{linha.Id}/encomenda", new MarcarEncomendaRequest(PecaEncomendaEstado.Encomendada)))
            .EnsureSuccessStatusCode();
        (await client.GetFromJsonAsync<List<PecaEncomendaDto>>("/api/vendas/pecas-encomenda"))!
            .Single(p => p.ItemId == linha.Id).Encomenda.Should().Be(PecaEncomendaEstado.Encomendada);

        // A peça chegou: entra na compra e a linha da reparação passa a usar esse lote (custo real, sai do stock ao entregar).
        var forn = await CriarFornecedor(client);
        var doc = (await (await client.PostAsJsonAsync("/api/compras", new CompraDocumentoWriteRequest(forn, new DateTime(2026, 11, 5), "FT " + Guid.NewGuid().ToString("N")[..6],
            null, "MB Way", 0m, null, null, null, [new CompraLinhaWriteRequest(null, "LCD iPad 9", 1, 28.29m, null, null, null)])))
            .Content.ReadFromJsonAsync<CompraDocumentoDto>())!;
        var lote = doc.Linhas.Single();

        var atualizada = await client.PutAsJsonAsync($"/api/vendas/{venda.Id}", new VendaWriteRequest(VendaTipo.Reparacao, cliente, "iPad 9", "Ecrã partido", null,
            [new VendaLinhaWriteRequest(linha.Id, lote.Id, ecra, 1, 6000)], null, null));
        atualizada.EnsureSuccessStatusCode();
        var comLote = (await atualizada.Content.ReadFromJsonAsync<VendaDto>())!.Items.Single();
        comLote.Encomenda.Should().BeNull();
        comLote.CompraLinhaId.Should().Be(lote.Id);
        comLote.CustoUnitarioPago.Should().Be(28.29m);
        (await client.GetFromJsonAsync<List<PecaEncomendaDto>>("/api/vendas/pecas-encomenda"))!.Should().NotContain(p => p.ItemId == linha.Id);
    }

    private static async Task<Guid> CriarFornecedor(HttpClient client)
    {
        var resp = await client.PostAsJsonAsync("/api/fornecedores",
            new FornecedorWriteRequest($"Forn {Guid.NewGuid():N}", null, null, null, null, null, null, true, false, RegimeIvaFornecedor.Nacional, "PT"));
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<FornecedorDto>())!.Id;
    }

    private async Task<HttpClient> AuthedClient()
    {
        var client = _factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(RepairDeskApiFactory.AdminEmail, RepairDeskApiFactory.AdminPassword));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await login.Content.ReadFromJsonAsync<AuthResponse>())!.AccessToken);
        return client;
    }
}
