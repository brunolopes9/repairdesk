using System.Net;
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

/// <summary>Vendas unificadas (Doc 94 Fase 4): estados, consumo de lotes, snapshot e IVA (SPEC §3.3 / §6.3).</summary>
public class VendasApiTests : IClassFixture<RepairDeskApiFactory>
{
    private readonly RepairDeskApiFactory _factory;
    public VendasApiTests(RepairDeskApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Orcamento_NaoMexeNoStock_PassarAPronta_Consome_Cancelar_Devolve()
    {
        var client = await AuthedClient();
        var lote = await CriarLote(client, RegimeIvaFornecedor.Nacional, "Ecrã Samsung A15", 2, 15.21m);
        var cliente = await CriarCliente(client);

        var venda = await Criar(client, new VendaWriteRequest(VendaTipo.Reparacao, cliente, "Samsung A15", "Ecrã partido", null,
            [new VendaLinhaWriteRequest(null, lote, null, 1, 4500), new VendaLinhaWriteRequest(null, null, "Mão de obra", 1, 2000)]));
        venda.Estado.Should().Be(VendaEstado.Orcamento);
        (await Stock(client, lote)).Should().Be(2);

        (await MudarEstado(client, venda.Id, VendaEstado.Pronta)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await Stock(client, lote)).Should().Be(1);

        (await MudarEstado(client, venda.Id, VendaEstado.Cancelada)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await Stock(client, lote)).Should().Be(2);
    }

    [Fact]
    public async Task StockInsuficiente_Bloqueia()
    {
        var client = await AuthedClient();
        var lote = await CriarLote(client, RegimeIvaFornecedor.Nacional, "Bateria A02s", 1, 8.40m);

        var resp = await client.PostAsJsonAsync("/api/vendas", new VendaWriteRequest(VendaTipo.Produto, null, null, null, null,
            [new VendaLinhaWriteRequest(null, lote, null, 2, 1500)], VendaEstado.Entregue));

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await resp.Content.ReadAsStringAsync()).Should().Contain("stock_insuficiente");
        (await Stock(client, lote)).Should().Be(1);
    }

    [Fact]
    public async Task VendaAPrecoManual_PecaUe_ContasDoSpec63()
    {
        var client = await AuthedClient();
        var lote = await CriarLote(client, RegimeIvaFornecedor.UeAutoliquidacao, "LCD Poco F3", 1, 16.42m);

        var venda = await Criar(client, new VendaWriteRequest(VendaTipo.Produto, null, null, null, null,
            [new VendaLinhaWriteRequest(null, lote, null, 1, 10000)], VendaEstado.Entregue, PaymentMethod.MBWay));

        var linha = venda.Items.Single();
        linha.CustoUnitarioPago.Should().Be(16.42m);
        linha.TaxaIvaCompra.Should().Be(0m);
        Math.Round(linha.IvaAPagarEstado, 2).Should().Be(18.70m);
        Math.Round(linha.Lucro, 2).Should().Be(64.88m);
        venda.IvaCents.Should().Be(1870);
        (await Stock(client, lote)).Should().Be(0);
    }

    [Fact]
    public async Task Servico_IvaSobreOValorTodo()
    {
        var client = await AuthedClient();
        var venda = await Criar(client, new VendaWriteRequest(VendaTipo.Servico, null, null, "Website institucional", null,
            [new VendaLinhaWriteRequest(null, null, "Website", 1, 36900)], VendaEstado.Pronta));

        venda.IvaCents.Should().Be(6900);
        Math.Round(venda.IvaAPagarEstado, 2).Should().Be(69.00m);
        Math.Round(venda.Lucro, 2).Should().Be(300.00m);
    }

    [Fact]
    public async Task Entregue_SemFatura_FicaPorRegistar_AteRegistarONumero()
    {
        var client = await AuthedClient();
        var venda = await Criar(client, new VendaWriteRequest(VendaTipo.Produto, null, null, null, null,
            [new VendaLinhaWriteRequest(null, null, "Película", 1, 1000)], VendaEstado.Entregue));
        venda.FaturaPorRegistar.Should().BeTrue();

        var pendentes = await client.GetFromJsonAsync<PagedResult<VendaDto>>("/api/vendas?faturaPorRegistar=true&pageSize=100");
        pendentes!.Items.Should().Contain(v => v.Id == venda.Id);

        var resp = await client.PutAsJsonAsync($"/api/vendas/{venda.Id}/fatura", new RegistarFaturaRequest("FR 2026/123", null));
        var atualizada = (await resp.Content.ReadFromJsonAsync<VendaDto>())!;
        atualizada.FaturaPorRegistar.Should().BeFalse();
        atualizada.InvoiceNumber.Should().Be("FR 2026/123");
    }

    [Fact]
    public async Task VendaEntregue_NaoPodeSerEditada()
    {
        var client = await AuthedClient();
        var req = new VendaWriteRequest(VendaTipo.Produto, null, null, null, null,
            [new VendaLinhaWriteRequest(null, null, "Capa", 1, 900)], VendaEstado.Entregue);
        var venda = await Criar(client, req);

        var resp = await client.PutAsJsonAsync($"/api/vendas/{venda.Id}", req);
        resp.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Reparacao_SemCliente_E422()
    {
        var client = await AuthedClient();
        var resp = await client.PostAsJsonAsync("/api/vendas", new VendaWriteRequest(VendaTipo.Reparacao, null, "iPhone 11", null, null, []));
        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task EditarQuantidade_EmPronta_AjustaOStock()
    {
        var client = await AuthedClient();
        var lote = await CriarLote(client, RegimeIvaFornecedor.Nacional, "Vidro temperado A13", 5, 0.74m);
        var venda = await Criar(client, new VendaWriteRequest(VendaTipo.Produto, null, null, null, null,
            [new VendaLinhaWriteRequest(null, lote, null, 2, 500)], VendaEstado.Pronta));
        (await Stock(client, lote)).Should().Be(3);

        var linhaId = venda.Items.Single().Id;
        var resp = await client.PutAsJsonAsync($"/api/vendas/{venda.Id}", new VendaWriteRequest(VendaTipo.Produto, null, null, null, null,
            [new VendaLinhaWriteRequest(linhaId, lote, null, 4, 500)]));
        resp.StatusCode.Should().Be(HttpStatusCode.OK, await resp.Content.ReadAsStringAsync());
        (await Stock(client, lote)).Should().Be(1);
    }

    // ---------- helpers ----------

    private static async Task<VendaDto> Criar(HttpClient client, VendaWriteRequest req)
    {
        var resp = await client.PostAsJsonAsync("/api/vendas", req);
        resp.StatusCode.Should().Be(HttpStatusCode.Created, await resp.Content.ReadAsStringAsync());
        return (await resp.Content.ReadFromJsonAsync<VendaDto>())!;
    }

    private static Task<HttpResponseMessage> MudarEstado(HttpClient client, Guid id, VendaEstado estado)
        => client.PostAsJsonAsync($"/api/vendas/{id}/estado", new MudarEstadoVendaRequest(estado));

    private static async Task<int> Stock(HttpClient client, Guid loteId)
    {
        var inv = await client.GetFromJsonAsync<List<InventarioLinhaDto>>("/api/compras/inventario");
        return inv!.SingleOrDefault(i => i.LinhaId == loteId)?.QuantidadeEmStock ?? 0;
    }

    private static async Task<Guid> CriarLote(HttpClient client, RegimeIvaFornecedor regime, string descricao, int qtd, decimal preco)
    {
        var forn = await client.PostAsJsonAsync("/api/fornecedores",
            new FornecedorWriteRequest($"Forn {Guid.NewGuid():N}", null, null, null, null, null, null, true, false, regime, null));
        var fornecedorId = (await forn.Content.ReadFromJsonAsync<FornecedorDto>())!.Id;
        var resp = await client.PostAsJsonAsync("/api/compras", new CompraDocumentoWriteRequest(fornecedorId, new DateTime(2026, 9, 1),
            "F-" + Guid.NewGuid().ToString("N")[..8], null, null, 0m, null, null, null,
            [new CompraLinhaWriteRequest(null, descricao, qtd, preco, null, null, null)]));
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<CompraDocumentoDto>())!.Linhas.Single().Id;
    }

    private static async Task<Guid> CriarCliente(HttpClient client)
    {
        var resp = await client.PostAsJsonAsync("/api/clientes", new CreateClienteRequest("Cliente Vendas", "912345678", null, null, null));
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<ClienteDto>())!.Id;
    }

    private async Task<HttpClient> AuthedClient()
    {
        var client = _factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(RepairDeskApiFactory.AdminEmail, RepairDeskApiFactory.AdminPassword));
        var auth = (await login.Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }
}
