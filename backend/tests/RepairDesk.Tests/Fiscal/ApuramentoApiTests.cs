using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using RepairDesk.API.Infrastructure;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;
using RepairDesk.DAL.Persistence;
using RepairDesk.Services.Compras;
using RepairDesk.Services.Despesas;
using RepairDesk.Services.Documents;
using RepairDesk.Services.Fiscal;
using RepairDesk.Services.Fornecedores;
using RepairDesk.Services.Vendas;
using RepairDesk.Tests.Auth;

namespace RepairDesk.Tests.Fiscal;

/// <summary>
/// Sprint 573: apuramento trimestral do IVA e resultado. Usa o 2.º trimestre de 2031 para não se cruzar
/// com dados de outros testes; os valores esperados estão contados à mão nos comentários.
/// </summary>
public class ApuramentoApiTests : IClassFixture<RepairDeskApiFactory>
{
    private readonly RepairDeskApiFactory _factory;
    public ApuramentoApiTests(RepairDeskApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Trimestre_JuntaVendasComprasDespesas_ESinalizaOQueFalta()
    {
        var client = await AuthedClient();
        var dia = new DateTime(2031, 5, 10);

        // Compras: nacional com fatura 10 × 12,30 (23%) → base 100 + IVA 23; portes 4,92 (IVA 0,92).
        var nacional = await Fornecedor(client, RegimeIvaFornecedor.Nacional);
        await Compra(client, nacional, dia, "FT N/1", 4.92m, 0.92m, (10, 12.30m));
        // Sem fatura: o IVA não se deduz e fica pendente.
        await Compra(client, nacional, dia, null, 0m, null, (1, 24.60m));
        // UE (autoliquidação): base 100 → IVA autoliquidado 23 (liquida e deduz).
        var ue = await Fornecedor(client, RegimeIvaFornecedor.UeAutoliquidacao);
        await Compra(client, ue, dia, "INV-UE-1", 0m, null, (5, 20m));

        // Despesas: 123 (IVA 23, 100%), gasóleo 61,50 (IVA 11,50, 50%), uma sem IVA indicado, serviço UE 10 (autoliquidação).
        await Despesa(client, "Renda", 12300, dia, 2300, 100);
        await Despesa(client, "Gasóleo", 6150, dia, 1150, 50);
        await Despesa(client, "Fatura sem IVA indicado", 3000, dia, null, 100);
        await Despesa(client, "Software UE", 1000, dia, null, 100, reverseCharge: true);

        // Vendas faturadas no trimestre: 123 a 23% (base 100) e 50 a 0%.
        await VendaFaturada(client, 12300, 23m, "FT T/1", dia.AddDays(10));
        await VendaFaturada(client, 5000, 0m, "FT T/2", dia.AddDays(11));

        var r = (await client.GetFromJsonAsync<ApuramentoDto>("/api/apuramento?ano=2031&trimestre=2"))!;

        r.De.Should().Be(new DateTime(2031, 4, 1));
        r.Ate.Should().Be(new DateTime(2031, 6, 30));
        r.Faturas.Should().Be(2);
        r.VendasPorTaxa.Should().ContainEquivalentOf(new ApuramentoTaxaDto(23m, 100m, 23m, 1));
        r.VendasPorTaxa.Should().ContainEquivalentOf(new ApuramentoTaxaDto(0m, 50m, 0m, 1));

        r.Compras.BaseNacional.Should().Be(104m);
        r.Compras.IvaNacional.Should().Be(23m);
        r.Compras.IvaPortes.Should().Be(0.92m);
        r.Compras.BaseUe.Should().Be(100m);
        r.Compras.IvaAutoliquidadoUe.Should().Be(23m);

        r.Despesas.Base.Should().Be(190m);
        r.Despesas.IvaDedutivel.Should().Be(28.75m);
        r.Despesas.IvaNaoDedutivel.Should().Be(5.75m);
        r.Despesas.IvaAutoliquidado.Should().Be(2.30m);

        r.IvaAutoliquidado.Should().Be(25.30m);
        r.IvaLiquidado.Should().Be(48.30m);             // 23 vendas + 25,30 autoliquidado
        r.IvaDedutivel.Should().Be(77.97m);             // 23 + 0,92 + 28,75 + 25,30
        r.Saldo.Should().Be(-29.67m, "este trimestre há IVA a recuperar");

        r.Resultado.Vendas.Should().Be(150m);
        r.Resultado.Gastos.Should().Be(195.75m);        // 190 + 5,75 de IVA não dedutível
        r.Resultado.Resultado.Should().Be(-45.75m);
        r.Resultado.ComprasDeStock.Should().Be(204m, "comprar stock não é gasto do trimestre");

        r.Campos.Single(c => c.Campo == "3").Valor.Should().Be(100m);
        r.Campos.Single(c => c.Campo == "4").Valor.Should().Be(23m);
        r.Campos.Single(c => c.Campo == "24").Valor.Should().Be(28.75m);
        r.Pendentes.Select(p => p.Tipo).Should().Contain(["compra_sem_fatura", "despesa_sem_iva"]);
    }

    [Fact]
    public async Task VendaEntregueSemFatura_FicaPendente_ENaoContaNoIva()
    {
        var client = await AuthedClient();
        var venda = await Post<VendaDto>(client, "/api/vendas", new VendaWriteRequest(VendaTipo.Servico, null, null, null, null,
            [new VendaLinhaWriteRequest(null, null, "Reparação", 1, 4000, 0, 23m)], VendaEstado.Entregue, PaymentMethod.Dinheiro));
        var agora = DateTime.UtcNow;
        var r = (await client.GetFromJsonAsync<ApuramentoDto>($"/api/apuramento?ano={agora.Year}&trimestre={(agora.Month - 1) / 3 + 1}"))!;
        r.Pendentes.Should().Contain(p => p.Tipo == "venda_sem_fatura" && p.Id == venda.Id);
    }

    [Fact]
    public async Task AprovarFaturaDeDespesa_GuardaOIvaLidoDoPdf_EIntraUeFicaEmAutoliquidacao()
    {
        var client = await AuthedClient();
        var lida = await Import(totalCents: 2460, subtotalCents: 2000);
        (await client.PostAsJsonAsync($"/api/supplier-invoices/{lida}/approve", new ApproveSupplierInvoiceRequest(
            2460, "Material", DespesaCategoria.Material, new DateTime(2031, 8, 1), "Loja PT Teste", "FT 1", null))).EnsureSuccessStatusCode();

        var ue = await Import(totalCents: 1500, subtotalCents: 1500);
        (await client.PostAsJsonAsync($"/api/supplier-invoices/{ue}/approve", new ApproveSupplierInvoiceRequest(
            1500, "Peças UE", DespesaCategoria.Material, new DateTime(2031, 8, 2), "Kaquu", "W1", null))).EnsureSuccessStatusCode();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var despesas = db.Despesas.Where(d => d.Data >= new DateTime(2031, 8, 1) && d.Data < new DateTime(2031, 8, 3)).ToList();
        despesas.Single(d => d.Descricao == "Material").IvaCents.Should().Be(460);
        var intraUe = despesas.Single(d => d.Descricao == "Peças UE");
        intraUe.ReverseCharge.Should().BeTrue("Kaquu é fornecedor intra-UE conhecido");
        intraUe.IvaCents.Should().Be(0);
    }

    [Fact]
    public async Task TrimestreInvalido_Recusa()
    {
        var client = await AuthedClient();
        (await client.GetAsync("/api/apuramento?ano=2031&trimestre=5")).StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    // ---------- ajudas ----------

    private static async Task<Guid> Fornecedor(HttpClient client, RegimeIvaFornecedor regime)
        => (await Post<FornecedorDto>(client, "/api/fornecedores", new FornecedorWriteRequest($"Forn {Guid.NewGuid():N}"[..20], null, null, null, null, null, null, true,
            regime == RegimeIvaFornecedor.UeAutoliquidacao, regime, null))).Id;

    private static Task<CompraDocumentoDto> Compra(HttpClient client, Guid fornecedor, DateTime data, string? fatura, decimal portes, decimal? portesIva,
        params (int Qtd, decimal Preco)[] linhas)
        => Post<CompraDocumentoDto>(client, "/api/compras", new CompraDocumentoWriteRequest(fornecedor, data, fatura,
            fatura is null ? [$"ENC-{Guid.NewGuid():N}"[..12]] : null, null, portes, portesIva, null, null,
            linhas.Select(l => new CompraLinhaWriteRequest(null, $"Peça {Guid.NewGuid():N}"[..14], l.Qtd, l.Preco, null, null, null)).ToList()));

    private static Task<DespesaDto> Despesa(HttpClient client, string descricao, int valorCents, DateTime data, int? ivaCents, int deducao, bool reverseCharge = false)
        => Post<DespesaDto>(client, "/api/despesas", new CreateDespesaRequest(descricao, DespesaCategoria.Outro, valorCents, data, null, null, null,
            ReverseCharge: reverseCharge, IvaCents: ivaCents, DeducaoPercent: deducao));

    private static async Task VendaFaturada(HttpClient client, int cents, decimal iva, string numero, DateTime emitida)
    {
        var v = await Post<VendaDto>(client, "/api/vendas", new VendaWriteRequest(VendaTipo.Servico, null, null, null, null,
            [new VendaLinhaWriteRequest(null, null, "Serviço", 1, cents, 0, iva)], VendaEstado.Entregue, PaymentMethod.Dinheiro));
        var resp = await client.PutAsJsonAsync($"/api/vendas/{v.Id}/fatura", new RegistarFaturaRequest($"{numero}-{Guid.NewGuid():N}"[..20], emitida));
        if (!resp.IsSuccessStatusCode) throw new Exception($"{(int)resp.StatusCode}: {await resp.Content.ReadAsStringAsync()}");
    }

    private async Task<Guid> Import(int totalCents, int subtotalCents)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var import = new SupplierInvoiceImport
        {
            TenantId = RepairDeskApiFactory.TenantId,
            PdfSha256 = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N"),
            PdfRelativePath = $"supplier-invoices/teste/{Guid.NewGuid():N}.pdf",
            ParseConfidence = "High",
            ParsedTotalCents = totalCents,
            ParsedSubtotalCents = subtotalCents,
            Status = SupplierInvoiceImportStatus.Pending,
        };
        db.SupplierInvoiceImports.Add(import);
        await db.SaveChangesAsync();
        return import.Id;
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
