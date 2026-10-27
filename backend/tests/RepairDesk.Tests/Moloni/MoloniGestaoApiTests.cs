using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using RepairDesk.API.Infrastructure;
using RepairDesk.Core.Enums;
using RepairDesk.Services.Artigos;
using RepairDesk.Services.Clientes;
using RepairDesk.Services.Moloni;
using RepairDesk.Services.Vendas;
using RepairDesk.Tests.Auth;

namespace RepairDesk.Tests.Moloni;

/// <summary>Sprint 568: documentos, artigos/serviços e clientes do Moloni ON geridos no Mender (Moloni simulado).</summary>
public class MoloniGestaoApiTests : IClassFixture<MoloniApiFactory>
{
    private readonly MoloniApiFactory _factory;
    public MoloniGestaoApiTests(MoloniApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Emitir_ComOJsonDoEcra_TipoEmTexto_Funciona()
    {
        // Bug apanhado na auditoria: o ecrã envia "FaturaRecibo" em texto e a API só aceitava números.
        var client = await LigadoAsync();
        var venda = await Post<VendaDto>(client, "/api/vendas", new VendaWriteRequest(VendaTipo.Servico, null, null, null, null,
            [new VendaLinhaWriteRequest(null, null, "Formatação", 1, 3000, 0, 23m)], VendaEstado.Entregue, PaymentMethod.Dinheiro));
        var json = """{"tipo":"FaturaRecibo","serieId":null,"paymentMethod":0,"vencimentoDias":0,"motivoIsencao":null}""";
        var resp = await client.PostAsync($"/api/vendas/{venda.Id}/moloni/emitir", new StringContent(json, Encoding.UTF8, "application/json"));
        resp.StatusCode.Should().Be(HttpStatusCode.OK, await resp.Content.ReadAsStringAsync());
        (await resp.Content.ReadFromJsonAsync<VendaDto>())!.InvoiceNumber.Should().StartWith("FR ");
    }

    [Fact]
    public async Task ListaDeFaturas_MostraVendaLigada_EPagamentoDeFaturaExterna()
    {
        var client = await LigadoAsync();
        _factory.Moloni.Faturas.Clear();
        _factory.Moloni.Faturas.Add(new { documentId = 86676, number = 1, documentSetName = "SOFTWARE", date = "2026-06-19T09:10:15.000Z", totalValue = 492m, status = 1, nullified = false, reconciliationPercentage = 0m, entityName = "Beira Plus", entityVat = "513679081" });
        var pagina = await client.GetFromJsonAsync<MoloniPagina<MoloniDocumentoDto>>("/api/moloni/documentos?tipo=Fatura");
        var f = pagina!.Items.Single();
        f.Numero.Should().Be("FT SOFTWARE/1");
        f.PagoPercentagem.Should().Be(0);
        f.VendaId.Should().BeNull("emitida fora do Mender");

        (await client.PostAsJsonAsync("/api/moloni/documentos/Fatura/86676/recibo", new MoloniReciboDocumentoRequest(PaymentMethod.TransferenciaBancaria)))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        var recibo = _factory.Moloni.Chamadas.Last(c => c.Operacao == "receiptCreate").Variaveis!["d"]!;
        recibo["relatedWith"]![0]!["relatedDocumentId"]!.GetValue<int>().Should().Be(86676);
        recibo["status"]!.GetValue<int>().Should().Be(1);
    }

    [Fact]
    public async Task ArtigosDoMoloni_Crud_ComPrecoComIva_EIsencaoObrigatoria()
    {
        var client = await LigadoAsync();
        var criado = await Post<MoloniProdutoDto>(client, "/api/moloni/artigos", new MoloniProdutoRequest("Manutenção website", "MANUT-TESTE", 2, 36.90m, 23m));
        var enviado = _factory.Moloni.Chamadas.Last(c => c.Operacao == "productCreate").Variaveis!["d"]!;
        enviado["price"]!.GetValue<decimal>().Should().Be(30m, "o Moloni guarda o preço sem IVA");
        enviado["taxes"]![0]!["taxId"]!.GetValue<int>().Should().Be(12733);

        (await client.PutAsJsonAsync($"/api/moloni/artigos/{criado.ProductId}", new MoloniProdutoRequest("Reward trading", "REWARD-T", 2, 100m, 0m)))
            .StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, "a 0% exige motivo de isenção");
        (await client.PutAsJsonAsync($"/api/moloni/artigos/{criado.ProductId}", new MoloniProdutoRequest("Reward trading", "REWARD-T", 2, 100m, 0m, "m40")))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        _factory.Moloni.Chamadas.Last(c => c.Operacao == "productUpdate").Variaveis!["d"]!["exemptionReason"]!.GetValue<string>().Should().Be("M40");

        _factory.Moloni.ArtigosEmUso.Add(777);
        (await client.DeleteAsync("/api/moloni/artigos/777")).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await client.DeleteAsync($"/api/moloni/artigos/{criado.ProductId}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task ImportarClientes_LigaPeloNif_ECriaOsQueFaltam()
    {
        var client = await LigadoAsync();
        var nifExistente = NifValido();
        var c = await client.PostAsJsonAsync("/api/clientes", new CreateClienteRequest("Já no Mender", null, null, nifExistente, null));
        c.EnsureSuccessStatusCode();
        var nifNovo = NifValido();
        _factory.Moloni.Clientes.Clear();
        _factory.Moloni.Clientes.Add(new { customerId = 63270, name = "Consumidor final", vat = (string?)null, isDefault = true });
        _factory.Moloni.Clientes.Add(new { customerId = 4001, name = "Já no Mender (Moloni)", vat = nifExistente, isDefault = false });
        _factory.Moloni.Clientes.Add(new { customerId = 4002, name = "Só no Moloni", vat = nifNovo, email = "x@y.pt", city = "Viseu", isDefault = false });

        var r = await Post<MoloniImportacaoClientesDto>(client, "/api/moloni/clientes/importar", new { });
        r.Should().BeEquivalentTo(new MoloniImportacaoClientesDto(Ligados: 1, Criados: 1, JaLigados: 0, Total: 2));
        var outra = await Post<MoloniImportacaoClientesDto>(client, "/api/moloni/clientes/importar", new { });
        outra.JaLigados.Should().Be(2, "importar outra vez não duplica");

        var lista = await client.GetFromJsonAsync<PagedResult<ClienteDto>>($"/api/clientes?q={nifNovo}");
        lista!.Items.Should().ContainSingle(x => x.Nome == "Só no Moloni");
    }

    [Fact]
    public async Task ArtigoDaLoja_LigadoAoMoloni_AlterarNomeAtualizaLa()
    {
        var client = await LigadoAsync();
        var artigo = (await Post<ArtigoDetalheDto>(client, "/api/artigos", new ArtigoWriteRequest("Cabo X", null, 500, null, null, null, null))).Artigo;
        // Ligar como se já tivesse sido faturado.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RepairDesk.DAL.Persistence.AppDbContext>();
            db.Artigos.Single(a => a.Id == artigo.Id).MoloniProductId = 5555;
            await db.SaveChangesAsync();
        }
        (await client.PutAsJsonAsync($"/api/artigos/{artigo.Id}", new ArtigoWriteRequest("Cabo X 2 m", null, 615, null, null, null, null)))
            .EnsureSuccessStatusCode();
        var upd = _factory.Moloni.Chamadas.Last(c => c.Operacao == "productUpdate").Variaveis!["d"]!;
        upd["productId"]!.GetValue<int>().Should().Be(5555);
        upd["name"]!.GetValue<string>().Should().Be("Cabo X 2 m");
        upd["price"]!.GetValue<decimal>().Should().Be(5m, "6,15 € com IVA = 5,00 € sem IVA");
    }

    [Fact]
    public async Task LigarVendas_AntigasImportadas_FicamLigadasAFaturaEAoRecibo()
    {
        var client = await LigadoAsync();
        var venda = await Post<VendaDto>(client, "/api/vendas", new VendaWriteRequest(VendaTipo.Servico, null, null, null, null,
            [new VendaLinhaWriteRequest(null, null, "Site", 1, 49200, 0, 23m)], VendaEstado.Entregue, PaymentMethod.TransferenciaBancaria));
        (await client.PutAsJsonAsync($"/api/vendas/{venda.Id}/fatura", new RegistarFaturaRequest("FT SOFTWARE/41", new DateTime(2026, 6, 19)))).EnsureSuccessStatusCode();
        _factory.Moloni.Faturas.Clear();
        _factory.Moloni.Recibos.Clear();
        _factory.Moloni.Faturas.Add(new { documentId = 86641, number = 41, documentSetName = "SOFTWARE", status = 1, nullified = false });
        _factory.Moloni.Recibos.Add(new { documentId = 89641, number = 41, documentSetName = "SOFTWARE", status = 1, nullified = false,
            relatedWith = new[] { new { relatedDocument = new { documentId = 86641 } } } });

        var r = await Post<MoloniLigacaoVendasDto>(client, "/api/moloni/vendas/ligar", new { });
        r.Ligadas.Should().BeGreaterThanOrEqualTo(1);
        var v = await client.GetFromJsonAsync<VendaDto>($"/api/vendas/{venda.Id}");
        v!.MoloniDocumentoTipo.Should().Be("invoice");
        v.Paga.Should().BeTrue();
        v.MoloniReciboNumero.Should().Be("RC SOFTWARE/41");
    }

    // ---------- ajudas ----------

    private async Task<HttpClient> LigadoAsync()
    {
        var client = await AuthedClient();
        (await client.PutAsJsonAsync("/api/moloni", new MoloniConfigurarRequest("apik:teste:segredo"))).EnsureSuccessStatusCode();
        return client;
    }

    private static string NifValido()
    {
        var oito = "5" + Random.Shared.Next(1_000_000, 9_999_999);
        var soma = 0;
        for (var i = 0; i < 8; i++) soma += (oito[i] - '0') * (9 - i);
        var resto = soma % 11;
        return oito + (resto < 2 ? 0 : 11 - resto);
    }

    private static async Task<T> Post<T>(HttpClient client, string url, object body)
    {
        var resp = await client.PostAsJsonAsync(url, body);
        if (!resp.IsSuccessStatusCode) throw new Exception($"{(int)resp.StatusCode}: {await resp.Content.ReadAsStringAsync()}");
        return (await resp.Content.ReadFromJsonAsync<T>())!;
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
