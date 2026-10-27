using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using RepairDesk.API.Infrastructure;
using RepairDesk.Core.Enums;
using RepairDesk.Services.Artigos;
using RepairDesk.Services.Clientes;
using RepairDesk.Services.Compras;
using RepairDesk.Services.Fornecedores;
using RepairDesk.Services.Moloni;
using RepairDesk.Services.Vendas;
using RepairDesk.Tests.Auth;

namespace RepairDesk.Tests.Moloni;

/// <summary>Sprint 567: documentos fiscais emitidos pelo Mender no Moloni ON (simulado).</summary>
public class MoloniApiTests : IClassFixture<MoloniApiFactory>
{
    private readonly MoloniApiFactory _factory;
    public MoloniApiTests(MoloniApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Fatura_DepoisRecibo_DepoisAnular_NaOrdemCerta()
    {
        var client = await ClienteMoloniAsync();
        var venda = await VendaServicoAsync(client, comCliente: true, precoCents: 36900, iva: 23m);

        var fatura = await Post<VendaDto>(client, $"/api/vendas/{venda.Id}/moloni/emitir", new MoloniEmitirRequest(MoloniDocumento.Fatura, VencimentoDias: 30));
        fatura.InvoiceNumber.Should().Be("FT SOFTWARE/7");
        fatura.MoloniDocumentoTipo.Should().Be("invoice");
        fatura.Paga.Should().BeFalse("fatura fica por pagar até ao recibo");
        fatura.TemFaturaPdf.Should().BeTrue();
        fatura.FaturaPorRegistar.Should().BeFalse();
        (await client.GetFromJsonAsync<PagedResult<VendaDto>>("/api/vendas?porPagar=true&pageSize=100"))!.Items.Should().Contain(v => v.Id == venda.Id);

        // Rascunho (status 0) com o preço SEM IVA, cliente criado pelo NIF; só depois finalizado (status 1).
        var criar = _factory.Moloni.Chamadas.Last(c => c.Operacao == "invoiceCreate").Variaveis!["d"]!;
        criar["status"]!.GetValue<int>().Should().Be(0);
        criar["products"]![0]!["price"]!.GetValue<decimal>().Should().Be(300m);
        _factory.Moloni.Chamadas.Last(c => c.Operacao == "invoiceUpdate").Variaveis!["d"]!["status"]!.GetValue<int>().Should().Be(1);
        _factory.Moloni.Operacoes.Should().Contain("customerCreate");

        var paga = await Post<VendaDto>(client, $"/api/vendas/{venda.Id}/moloni/recibo", new MoloniReciboRequest(PaymentMethod.TransferenciaBancaria));
        paga.Paga.Should().BeTrue();
        paga.MoloniReciboNumero.Should().Be("RC SOFTWARE/5");
        (await client.GetFromJsonAsync<PagedResult<VendaDto>>("/api/vendas?porPagar=true&pageSize=100"))!.Items.Should().NotContain(v => v.Id == venda.Id);
        (await client.PostAsJsonAsync($"/api/vendas/{venda.Id}/moloni/recibo", new MoloniReciboRequest()))
            .StatusCode.Should().Be(HttpStatusCode.Conflict, "não se paga duas vezes");

        var antes = _factory.Moloni.Chamadas.Count;
        var anulada = await Post<VendaDto>(client, $"/api/vendas/{venda.Id}/moloni/anular", new MoloniAnularRequest("Emitida com o NIF errado"));
        _factory.Moloni.Operacoes.Skip(antes).Where(o => o.EndsWith("Nullify")).Should().Equal("receiptNullify", "invoiceNullify");
        anulada.InvoiceNumber.Should().BeNull();
        anulada.FaturaPorRegistar.Should().BeTrue("volta a poder emitir-se");
        anulada.MoloniDocumentoTipo.Should().BeNull();
    }

    [Fact]
    public async Task TotalDoMoloniDiferente_ApagaORascunho_ENaoEmiteNada()
    {
        var client = await ClienteMoloniAsync();
        var venda = await VendaServicoAsync(client, comCliente: false, precoCents: 12300, iva: 23m);
        _factory.Moloni.TotalForcado = 99.99m;
        try
        {
            var resp = await client.PostAsJsonAsync($"/api/vendas/{venda.Id}/moloni/emitir", new MoloniEmitirRequest(MoloniDocumento.FaturaRecibo));
            resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
            (await resp.Content.ReadAsStringAsync()).Should().Contain("total_diferente");
        }
        finally { _factory.Moloni.TotalForcado = null; }
        _factory.Moloni.Operacoes.Last().Should().Be("invoiceReceiptDelete");
        (await client.GetFromJsonAsync<VendaDto>($"/api/vendas/{venda.Id}"))!.InvoiceNumber.Should().BeNull();
    }

    [Fact]
    public async Task LinhaIsenta_SemMotivo_Recusa_ComMotivo_EnviaExemptionReason()
    {
        var client = await ClienteMoloniAsync();
        var venda = await VendaServicoAsync(client, comCliente: false, precoCents: 23240, iva: 0m);
        var sem = await client.PostAsJsonAsync($"/api/vendas/{venda.Id}/moloni/emitir", new MoloniEmitirRequest(MoloniDocumento.FaturaRecibo));
        sem.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await sem.Content.ReadAsStringAsync()).Should().Contain("motivo_isencao_obrigatorio");

        var ok = await Post<VendaDto>(client, $"/api/vendas/{venda.Id}/moloni/emitir",
            new MoloniEmitirRequest(MoloniDocumento.FaturaRecibo, PaymentMethod: PaymentMethod.TransferenciaBancaria, MotivoIsencao: "m40"));
        ok.InvoiceNumber.Should().Be("FR SOFTWARE/7");
        ok.Paga.Should().BeTrue();
        var linha = _factory.Moloni.Chamadas.Last(c => c.Operacao == "invoiceReceiptCreate").Variaveis!["d"]!["products"]![0]!;
        linha["exemptionReason"]!.GetValue<string>().Should().Be("M40");
        linha["taxes"]!.AsArray().Should().BeEmpty();
        _factory.Moloni.Chamadas.Last(c => c.Operacao == "invoiceReceiptCreate").Variaveis!["d"]!["customerId"]!.GetValue<int>()
            .Should().Be(63270, "consumidor final = cliente predefinido do Moloni");
    }

    [Fact]
    public async Task Balcao_ComMoloniLigado_SaiFaturaReciboLogo()
    {
        var client = await ClienteMoloniAsync();
        var lote = await CriarLoteAsync(client, "Película teste balcão", 3, 1.00m);
        var artigo = (await Post<ArtigoDetalheDto>(client, "/api/artigos",
            new ArtigoWriteRequest("Película teste balcão", null, 999, null, null, null, null, true, [lote]))).Artigo;

        var venda = await Post<VendaDto>(client, "/api/artigos/balcao/vender", new BalcaoVendaRequest([new BalcaoLinhaRequest(artigo.Id, 1)], PaymentMethod.MBWay));

        venda.InvoiceNumber.Should().Be("FR SOFTWARE/7");
        venda.Paga.Should().BeTrue();
        venda.FaturaPorRegistar.Should().BeFalse();
        _factory.Moloni.Operacoes.Should().Contain("paymentMethodCreate", "MB Way não existia no Moloni");
        _factory.Moloni.Chamadas.Last(c => c.Operacao == "productCreate").Variaveis!["d"]!["reference"]!.GetValue<string>()
            .Should().Be(artigo.Sku, "o artigo da loja passa a existir no Moloni com o SKU");
    }

    [Fact]
    public async Task SemMoloniLigado_EmitirDizComoLigar()
    {
        var client = await AuthedClient();
        (await client.DeleteAsync("/api/moloni")).EnsureSuccessStatusCode();
        var venda = await VendaServicoAsync(client, comCliente: false, precoCents: 1000, iva: 23m);
        var resp = await client.PostAsJsonAsync($"/api/vendas/{venda.Id}/moloni/emitir", new MoloniEmitirRequest(MoloniDocumento.FaturaRecibo));
        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await resp.Content.ReadAsStringAsync()).Should().Contain("moloni_nao_configurado");
    }

    // ---------- Sprint 571: sem NIF / com NIF ----------

    [Fact]
    public async Task ClienteSemNif_NaoECriadoNoMoloni_EAFaturaSaiParaConsumidorFinal()
    {
        var client = await ClienteMoloniAsync();
        var criadosAntes = _factory.Moloni.Operacoes.Count(o => o == "customerCreate");
        var cliente = await Post<ClienteDto>(client, "/api/clientes", new CreateClienteRequest("André de Lamaçais", null, null, null, null));
        _factory.Moloni.Operacoes.Count(o => o == "customerCreate").Should().Be(criadosAntes, "sem NIF o nome é só interno");

        var venda = await VendaComClienteAsync(client, cliente.Id);
        await Post<VendaDto>(client, $"/api/vendas/{venda.Id}/moloni/emitir", new MoloniEmitirRequest(MoloniDocumento.FaturaRecibo));

        UltimoCliente("invoiceReceiptCreate").Should().Be(63270);
        _factory.Moloni.Operacoes.Count(o => o == "customerCreate").Should().Be(criadosAntes);
    }

    [Fact]
    public async Task ComNif_GuardaNomeFiscalSemMudarONomeInterno_ECriaNoMoloni()
    {
        var client = await ClienteMoloniAsync();
        var cliente = await Post<ClienteDto>(client, "/api/clientes", new CreateClienteRequest("André de Lamaçais", null, null, null, null));
        var venda = await VendaComClienteAsync(client, cliente.Id);
        var nif = NifValido();

        var emitida = await Post<VendaDto>(client, $"/api/vendas/{venda.Id}/moloni/emitir", new MoloniEmitirRequest(MoloniDocumento.Fatura,
            ComNif: new MoloniDadosFiscais(nif, "André Silva", "Rua da Igreja, 42", "3500-001", "Viseu")));

        UltimoCliente("invoiceCreate").Should().NotBe(63270);
        var criado = _factory.Moloni.Chamadas.Last(c => c.Operacao == "customerCreate").Variaveis!["d"]!;
        criado["vat"]!.GetValue<string>().Should().Be(nif);
        criado["name"]!.GetValue<string>().Should().Be("André Silva");
        criado["zipCode"]!.GetValue<string>().Should().Be("3500-001");
        emitida.Cliente!.Nome.Should().Be("André de Lamaçais", "o nome interno não muda");
        emitida.Cliente.NomeFaturacao.Should().Be("André Silva");

        var guardado = (await client.GetFromJsonAsync<ClienteDto>($"/api/clientes/{cliente.Id}"))!;
        guardado.Nif.Should().Be(nif);
        guardado.Morada.Should().Be("Rua da Igreja, 42");
        guardado.Localidade.Should().Be("Viseu");
    }

    [Fact]
    public async Task SemNif_ComClienteQueTemNif_SaiParaConsumidorFinal()
    {
        var client = await ClienteMoloniAsync();
        var venda = await VendaServicoAsync(client, comCliente: true, precoCents: 1500, iva: 23m);
        await Post<VendaDto>(client, $"/api/vendas/{venda.Id}/moloni/emitir", new MoloniEmitirRequest(MoloniDocumento.FaturaRecibo, SemNif: true));
        UltimoCliente("invoiceReceiptCreate").Should().Be(63270);
    }

    [Fact]
    public async Task ComNif_VendaSemCliente_CriaOClienteELigaAVenda()
    {
        var client = await ClienteMoloniAsync();
        var venda = await VendaServicoAsync(client, comCliente: false, precoCents: 1500, iva: 23m);
        var nif = NifValido();
        var emitida = await Post<VendaDto>(client, $"/api/vendas/{venda.Id}/moloni/emitir", new MoloniEmitirRequest(MoloniDocumento.FaturaRecibo,
            ComNif: new MoloniDadosFiscais(nif, "Maria Costa", "Av. Central 1", "3000-100", "Coimbra")));
        emitida.Cliente.Should().NotBeNull();
        emitida.Cliente!.Nome.Should().Be("Maria Costa");
        emitida.Cliente.Nif.Should().Be(nif);
        UltimoCliente("invoiceReceiptCreate").Should().NotBe(63270);
    }

    [Fact]
    public async Task ComNif_DadosIncompletos_RecusaSemTocarNoMoloni()
    {
        var client = await ClienteMoloniAsync();
        var venda = await VendaServicoAsync(client, comCliente: false, precoCents: 1500, iva: 23m);
        var antes = _factory.Moloni.Operacoes.Count(o => o.EndsWith("Create"));
        var resp = await client.PostAsJsonAsync($"/api/vendas/{venda.Id}/moloni/emitir", new MoloniEmitirRequest(MoloniDocumento.FaturaRecibo,
            ComNif: new MoloniDadosFiscais(NifValido(), "Maria Costa", "Av. Central 1", "3000", "Coimbra")));
        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await resp.Content.ReadAsStringAsync()).Should().Contain("codigo_postal_invalido");
        _factory.Moloni.Operacoes.Count(o => o.EndsWith("Create")).Should().Be(antes);

        (await client.PostAsJsonAsync($"/api/vendas/{venda.Id}/moloni/emitir", new MoloniEmitirRequest(MoloniDocumento.FaturaRecibo,
            ComNif: new MoloniDadosFiscais("123456780", "Maria Costa", "Av. Central 1", "3000-100", "Coimbra"))))
            .StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, "NIF com check-digit errado");
    }

    private int UltimoCliente(string operacao)
        => _factory.Moloni.Chamadas.Last(c => c.Operacao == operacao).Variaveis!["d"]!["customerId"]!.GetValue<int>();

    private static Task<VendaDto> VendaComClienteAsync(HttpClient client, Guid clienteId)
        => Post<VendaDto>(client, "/api/vendas", new VendaWriteRequest(VendaTipo.Servico, clienteId, null, null, null,
            [new VendaLinhaWriteRequest(null, null, "Reparação ecrã", 1, 8000, 0, 23m)], VendaEstado.Entregue, PaymentMethod.Dinheiro));

    // ---------- ajudas ----------

    private async Task<HttpClient> ClienteMoloniAsync()
    {
        var client = await AuthedClient();
        var estado = await Put<MoloniEstadoDto>(client, "/api/moloni", new MoloniConfigurarRequest("apik:teste:segredo"));
        estado.Configurado.Should().BeTrue();
        estado.SerieServico.Should().Be(3216);
        estado.SerieReparacao.Should().Be(3413);
        return client;
    }

    private static async Task<VendaDto> VendaServicoAsync(HttpClient client, bool comCliente, int precoCents, decimal iva)
    {
        Guid? clienteId = null;
        if (comCliente)
        {
            var c = await client.PostAsJsonAsync("/api/clientes", new CreateClienteRequest($"Associação {Guid.NewGuid():N}"[..20], null, null, NifValido(), null));
            c.EnsureSuccessStatusCode();
            clienteId = (await c.Content.ReadFromJsonAsync<ClienteDto>())!.Id;
        }
        return await Post<VendaDto>(client, "/api/vendas", new VendaWriteRequest(VendaTipo.Servico, clienteId, null, null, null,
            [new VendaLinhaWriteRequest(null, null, "Website", 1, precoCents, 0, iva)], VendaEstado.Entregue, PaymentMethod.Dinheiro));
    }

    /// <summary>NIF português válido (mod 11), diferente em cada teste.</summary>
    private static string NifValido()
    {
        while (true)
        {
            var oito = "5" + Random.Shared.Next(1_000_000, 9_999_999).ToString();
            var soma = 0;
            for (var i = 0; i < 8; i++) soma += (oito[i] - '0') * (9 - i);
            var resto = soma % 11;
            var dig = resto < 2 ? 0 : 11 - resto;
            return oito + dig;
        }
    }

    private static async Task<Guid> CriarLoteAsync(HttpClient client, string descricao, int qtd, decimal preco)
    {
        var forn = await client.PostAsJsonAsync("/api/fornecedores",
            new FornecedorWriteRequest($"Forn {Guid.NewGuid():N}", null, null, null, null, null, null, true, false, RegimeIvaFornecedor.Nacional, null));
        var fornecedorId = (await forn.Content.ReadFromJsonAsync<FornecedorDto>())!.Id;
        var resp = await client.PostAsJsonAsync("/api/compras", new CompraDocumentoWriteRequest(fornecedorId, new DateTime(2026, 9, 1),
            "F-" + Guid.NewGuid().ToString("N")[..8], null, null, 0m, null, null, null,
            [new CompraLinhaWriteRequest(null, descricao, qtd, preco, null, null, null)]));
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<CompraDocumentoDto>())!.Linhas.Single().Id;
    }

    private static async Task<T> Post<T>(HttpClient client, string url, object body)
    {
        var resp = await client.PostAsJsonAsync(url, body);
        if (!resp.IsSuccessStatusCode) throw new Exception($"{(int)resp.StatusCode}: {await resp.Content.ReadAsStringAsync()}");
        return (await resp.Content.ReadFromJsonAsync<T>())!;
    }

    private static async Task<T> Put<T>(HttpClient client, string url, object body)
    {
        var resp = await client.PutAsJsonAsync(url, body);
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
