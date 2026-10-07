using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RepairDesk.API.Infrastructure;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;
using RepairDesk.DAL.Persistence;
using RepairDesk.Services.Clientes;
using RepairDesk.Services.Compras;
using RepairDesk.Services.Documents;
using RepairDesk.Services.Fornecedores;
using RepairDesk.Tests.Auth;

namespace RepairDesk.Tests.Compras;

/// <summary>
/// Sprint 560: faturas recebidas ligam-se à compra que já existe (fatura em falta / mesma fatura /
/// encomenda) em vez de criarem compras duplicadas; nº visível em documentos e lotes.
/// </summary>
public class CompraFaturaApiTests : IClassFixture<RepairDeskApiFactory>
{
    private readonly RepairDeskApiFactory _factory;
    public CompraFaturaApiTests(RepairDeskApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Numeros_SaoSequenciaisEmDocumentosELotes()
    {
        var client = await AuthedClient();
        var forn = await CriarFornecedor(client, RegimeIvaFornecedor.Nacional);

        var a = await CriarCompra(client, forn, new DateTime(2026, 7, 1), "FT N/" + Rand(), null, 0m, null, ("Bateria A", 1, 8m), ("Bateria B", 2, 9m));
        var b = await CriarCompra(client, forn, new DateTime(2026, 7, 2), "FT N/" + Rand(), null, 0m, null, ("Ecrã C", 1, 30m));

        b.Numero.Should().Be(a.Numero + 1);
        a.Linhas.Select(l => l.Numero).Should().BeInAscendingOrder().And.OnlyHaveUniqueItems();
        b.Linhas.Single().Numero.Should().Be(a.Linhas.Max(l => l.Numero) + 1);

        // Pesquisar pelo nº encontra a compra.
        var res = await client.GetFromJsonAsync<PagedResult<CompraDocumentoDto>>($"/api/compras?q={b.Numero}");
        res!.Items.Should().Contain(d => d.Id == b.Id);
    }

    [Fact]
    public async Task FaturaEmFalta_QueBateCerto_AssociaSozinha_EIdempotente()
    {
        var client = await AuthedClient();
        var forn = await CriarFornecedor(client, RegimeIvaFornecedor.Nacional);
        var compra = await CriarCompra(client, forn, new DateTime(2026, 7, 6), null, ["164061"], 5.50m, 11.65m, ("Back tampa Samsung A15", 1, 6.15m));
        compra.FaturaEmFalta.Should().BeTrue();

        var importId = await CriarImport(forn, "FRE A01/72199", new DateTime(2026, 7, 6), 1165, ("Back tampa para telemovel", 1, 615), ("Portes", 1, 550));

        var corr = await client.GetFromJsonAsync<CorrespondenciaFaturaDto>($"/api/compras/faturas-recebidas/{importId}/correspondencia");
        corr!.Sugestao.Should().Be("associar");
        corr.CompraSugeridaId.Should().Be(compra.Id);
        corr.Fatura.Portes.Should().Be(5.50m);

        var auto = await PostAuto(client);
        auto.Itens.Should().Contain(i => i.ImportId == importId && i.Resultado == "associada");

        var doc = await client.GetFromJsonAsync<CompraDocumentoDto>($"/api/compras/{compra.Id}");
        doc!.NumeroFatura.Should().Be("FRE A01/72199");
        doc.FaturaEmFalta.Should().BeFalse();
        doc.SupplierInvoiceImportId.Should().Be(importId);
        doc.TemDiferenca.Should().BeFalse();
        Math.Round(doc.PortesIva!.Value, 2).Should().Be(1.03m); // 5,50 − 5,50/1,23
        doc.Linhas.Single().QuantidadeEmStock.Should().Be(1, "associar uma fatura nunca mexe no stock");
        (await StatusImport(importId)).Should().Be(SupplierInvoiceImportStatus.Approved);

        // Repetir: nada muda, nada duplica.
        (await PostAuto(client)).Itens.Should().NotContain(i => i.ImportId == importId);
        var outraVez = await client.PostAsync($"/api/compras/{compra.Id}/associar-fatura/{importId}", null);
        outraVez.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task MesmaFatura_ComTotaisErradosNoExcel_FicaComOsDaFatura()
    {
        // Caso real FRE A01/73083: linhas certas, mas portes 5,50 (eram 5,95) e total 53,10 (era 59,93).
        var client = await AuthedClient();
        var forn = await CriarFornecedor(client, RegimeIvaFornecedor.Nacional);
        var numero = "FRE A01/" + Rand();
        var compra = await CriarCompra(client, forn, new DateTime(2026, 7, 21), numero, ["165152"], 5.50m, 53.10m,
            ("Touch e lcd para iph 11 preto", 1, 18.33m), ("Touch e lcd para iph 11 preto", 1, 24.00m),
            ("Touch e lcd para iph 5s preto", 1, 10.95m), ("VIDRO TEMPERADO NORMAL", 1, 0.70m));
        compra.TemDiferenca.Should().BeTrue();

        var importId = await CriarImport(forn, numero, new DateTime(2026, 7, 21), 5993,
            ("Touch e lcd para iph 11 preto", 1, 1833), ("Touch e lcd para iph 11 preto", 1, 2400),
            ("Touch e lcd para iph 5s preto", 1, 1095), ("VIDRO TEMPERADO NORMAL", 1, 70), ("Portes", 1, 595));

        await PostAuto(client);

        var doc = await client.GetFromJsonAsync<CompraDocumentoDto>($"/api/compras/{compra.Id}");
        doc!.PortesPagos.Should().Be(5.95m);
        doc.TotalDocumento.Should().Be(59.93m);
        doc.TemDiferenca.Should().BeFalse();
        doc.SupplierInvoiceImportId.Should().Be(importId);
    }

    [Fact]
    public async Task FaturaQueNaoBate_FicaParaRever_EAssociacaoManualMostraADiferenca()
    {
        // Caso real FRE A01/72384: a fatura tem uma bateria que a compra (encomenda #164289) não tem.
        var client = await AuthedClient();
        var forn = await CriarFornecedor(client, RegimeIvaFornecedor.Nacional);
        var compra = await CriarCompra(client, forn, new DateTime(2026, 7, 8), null, ["164289"], 5.50m, 24.93m,
            ("Vidro temperado Samsung A13 5G", 3, 0.738m), ("Touch e lcd Realme 12 5G", 1, 17.22m));
        var importId = await CriarImport(forn, "FRE A01/" + Rand(), new DateTime(2026, 7, 8), 3355,
            ("Touch e lcd para realme 12 5g", 1, 1722), ("Bateria 616-0721 para iph 5s", 1, 861),
            ("VIDRO TEMPERADO NORMAL", 3, 222), ("Portes", 1, 550));

        var corr = await client.GetFromJsonAsync<CorrespondenciaFaturaDto>($"/api/compras/faturas-recebidas/{importId}/correspondencia");
        corr!.Sugestao.Should().Be("rever");
        var cand = corr.Candidatos.Single(c => c.CompraId == compra.Id);
        cand.BateCerto.Should().BeFalse();
        Math.Round(cand.DiferencaComFatura, 2).Should().Be(-8.62m);

        (await PostAuto(client)).Itens.Should().Contain(i => i.ImportId == importId && i.Resultado == "rever");
        (await StatusImport(importId)).Should().Be(SupplierInvoiceImportStatus.Pending);

        var resp = await client.PostAsync($"/api/compras/{compra.Id}/associar-fatura/{importId}", null);
        resp.StatusCode.Should().Be(HttpStatusCode.OK, await resp.Content.ReadAsStringAsync());
        var doc = (await resp.Content.ReadFromJsonAsync<CompraDocumentoDto>())!;
        doc.TotalDocumento.Should().Be(33.55m);
        doc.TemDiferenca.Should().BeTrue("as linhas continuam a ser as da compra — a correção é humana");
    }

    [Fact]
    public async Task ResumoDeEncomenda_DocumentaACompra_MasAFaturaContinuaEmFalta()
    {
        var client = await AuthedClient();
        var ue = await CriarFornecedor(client, RegimeIvaFornecedor.UeAutoliquidacao);
        var enc = Random.Shared.Next(600000000, 699999999).ToString();
        var compra = await CriarCompra(client, ue, new DateTime(2026, 7, 3), null, [enc], 6.95m, 23.37m, ("LCD Poco F3", 1, 16.42m));
        var importId = await CriarImport(ue, enc, new DateTime(2026, 7, 3), 2337, ("Montagem de LCD Poco F3", 1, 1642), ("Envio (portes)", 1, 695));

        await PostAuto(client);

        var doc = await client.GetFromJsonAsync<CompraDocumentoDto>($"/api/compras/{compra.Id}");
        doc!.SupplierInvoiceImportId.Should().Be(importId);
        doc.NumeroFatura.Should().BeNull();
        doc.FaturaEmFalta.Should().BeTrue();
        doc.PortesIva.Should().Be(0m);

        // Quando chegar a fatura verdadeira, substitui o resumo.
        var fatura = await CriarImport(ue, "INV-" + Rand(), new DateTime(2026, 7, 4), 2337, ("LCD Poco F3", 1, 1642), ("Shipping", 1, 695));
        (await PostAuto(client)).Itens.Should().Contain(i => i.ImportId == fatura && i.Resultado == "associada");
        doc = await client.GetFromJsonAsync<CompraDocumentoDto>($"/api/compras/{compra.Id}");
        doc!.FaturaEmFalta.Should().BeFalse();
        doc.SupplierInvoiceImportId.Should().Be(fatura);
    }

    [Fact]
    public async Task SemCompra_SugereNova_ESegundaCopiaDaMesmaFatura_EDuplicada()
    {
        var client = await AuthedClient();
        var forn = await CriarFornecedor(client, RegimeIvaFornecedor.Nacional);
        var nova = await CriarImport(forn, "FRE A01/" + Rand(), new DateTime(2026, 7, 20), 1406, ("Back tampa iph 14", 1, 486), ("Portes", 1, 550), ("Vidro", 5, 370));
        (await client.GetFromJsonAsync<CorrespondenciaFaturaDto>($"/api/compras/faturas-recebidas/{nova}/correspondencia"))!
            .Sugestao.Should().Be("nova");

        var numero = "FRE A01/" + Rand();
        await CriarCompra(client, forn, new DateTime(2026, 9, 29), null, ["170197"], 5.50m, 10.36m, ("Back tampa iph 14", 1, 4.8585m));
        var original = await CriarImport(forn, numero, new DateTime(2026, 9, 29), 1036, ("Back tampa para ip 14", 1, 486), ("Portes", 1, 550));
        await PostAuto(client);
        (await StatusImport(original)).Should().Be(SupplierInvoiceImportStatus.Approved);

        var copia = await CriarImport(forn, numero, new DateTime(2026, 9, 29), 1036, ("Back tampa para ip 14", 1, 486), ("Portes", 1, 550));
        (await client.GetFromJsonAsync<CorrespondenciaFaturaDto>($"/api/compras/faturas-recebidas/{copia}/correspondencia"))!
            .Sugestao.Should().Be("duplicada");
        (await PostAuto(client)).Itens.Should().Contain(i => i.ImportId == copia && i.Resultado == "duplicada");
    }

    [Fact]
    public async Task DuasComprasQueBatem_NaoAssociaSozinha()
    {
        var client = await AuthedClient();
        var forn = await CriarFornecedor(client, RegimeIvaFornecedor.Nacional);
        await CriarCompra(client, forn, new DateTime(2026, 8, 1), null, ["1"], 5.50m, null, ("Vidro", 1, 1.25m));
        await CriarCompra(client, forn, new DateTime(2026, 8, 3), null, ["2"], 5.50m, null, ("Película", 1, 1.25m));
        var importId = await CriarImport(forn, "FRE A01/" + Rand(), new DateTime(2026, 8, 2), 675, ("Vidro", 1, 125), ("Portes", 1, 550));

        (await client.GetFromJsonAsync<CorrespondenciaFaturaDto>($"/api/compras/faturas-recebidas/{importId}/correspondencia"))!
            .Sugestao.Should().Be("rever");
    }

    [Fact]
    public async Task OutraEmpresa_NaoVeNemAssociaAFatura()
    {
        var client = await AuthedClient();
        var forn = await CriarFornecedor(client, RegimeIvaFornecedor.Nacional);
        var compra = await CriarCompra(client, forn, new DateTime(2026, 7, 6), null, ["9"], 5.50m, null, ("Back", 1, 6.15m));
        var importId = await CriarImport(forn, "FRE A01/" + Rand(), new DateTime(2026, 7, 6), 1165, ("Back", 1, 615), ("Portes", 1, 550));

        var outra = await AuthedClient(RepairDeskApiFactory.SecondAdminEmail);
        (await outra.GetAsync($"/api/compras/faturas-recebidas/{importId}/correspondencia")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await outra.PostAsync($"/api/compras/{compra.Id}/associar-fatura/{importId}", null)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await StatusImport(importId)).Should().Be(SupplierInvoiceImportStatus.Pending);
    }

    // ---------- helpers ----------

    private static string Rand() => Random.Shared.Next(10000, 99999).ToString();

    private static async Task<AssociacaoAutomaticaDto> PostAuto(HttpClient client)
    {
        var resp = await client.PostAsync("/api/compras/faturas-recebidas/associar-automaticamente", null);
        resp.StatusCode.Should().Be(HttpStatusCode.OK, await resp.Content.ReadAsStringAsync());
        return (await resp.Content.ReadFromJsonAsync<AssociacaoAutomaticaDto>())!;
    }

    private static async Task<CompraDocumentoDto> CriarCompra(HttpClient client, Guid fornecedorId, DateTime data, string? numero,
        string[]? encomendas, decimal portes, decimal? total, params (string Desc, int Qtd, decimal Preco)[] linhas)
    {
        var req = new CompraDocumentoWriteRequest(fornecedorId, data, numero, encomendas, null, portes, null, total, null,
            linhas.Select(l => new CompraLinhaWriteRequest(null, l.Desc, l.Qtd, l.Preco, null, null, null)).ToList());
        var resp = await client.PostAsJsonAsync("/api/compras", req);
        resp.StatusCode.Should().Be(HttpStatusCode.Created, await resp.Content.ReadAsStringAsync());
        return (await resp.Content.ReadFromJsonAsync<CompraDocumentoDto>())!;
    }

    private async Task<Guid> CriarImport(Guid fornecedorId, string numero, DateTime data, int totalCents, params (string Desc, int Qtd, int Cents)[] itens)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var import = new SupplierInvoiceImport
        {
            TenantId = RepairDeskApiFactory.TenantId,
            FornecedorId = fornecedorId,
            PdfSha256 = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N"),
            PdfRelativePath = $"2026/07/teste/{Guid.NewGuid():N}.pdf",
            ParsedDocumentNumber = numero,
            ParsedDocumentDate = data,
            ParsedTotalCents = totalCents,
            ParsedItemsJson = JsonSerializer.Serialize(itens.Select(i => new SupplierPdfItem(i.Desc, i.Qtd, i.Cents)).ToArray()),
            ParseConfidence = "High",
            Status = SupplierInvoiceImportStatus.Pending,
        };
        db.SupplierInvoiceImports.Add(import);
        await db.SaveChangesAsync();
        return import.Id;
    }

    private async Task<SupplierInvoiceImportStatus> StatusImport(Guid id)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return (await db.SupplierInvoiceImports.IgnoreQueryFilters().AsNoTracking().SingleAsync(i => i.Id == id)).Status;
    }

    private static async Task<Guid> CriarFornecedor(HttpClient client, RegimeIvaFornecedor regime)
    {
        var resp = await client.PostAsJsonAsync("/api/fornecedores",
            new FornecedorWriteRequest($"Forn {Guid.NewGuid():N}", null, null, null, null, null, null, true, false, regime, regime == RegimeIvaFornecedor.Nacional ? "PT" : "NL"));
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<FornecedorDto>())!.Id;
    }

    private async Task<HttpClient> AuthedClient(string email = RepairDeskApiFactory.AdminEmail)
    {
        var client = _factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, RepairDeskApiFactory.AdminPassword));
        login.EnsureSuccessStatusCode();
        var auth = (await login.Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }
}
