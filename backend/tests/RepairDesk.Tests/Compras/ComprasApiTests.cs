using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RepairDesk.Core.Entities;
using RepairDesk.DAL.Persistence;
using RepairDesk.API.Infrastructure;
using RepairDesk.Core.Enums;
using RepairDesk.Services.Clientes;
using RepairDesk.Services.Compras;
using RepairDesk.Services.Compras.Import;
using RepairDesk.Services.Fornecedores;
using RepairDesk.Tests.Auth;

namespace RepairDesk.Tests.Compras;

/// <summary>Compras por lote (Doc 94 Fase 3): CRUD, anti-duplicados, inventário, resumo, simulador e import do Excel.</summary>
public class ComprasApiTests : IClassFixture<RepairDeskApiFactory>
{
    private readonly RepairDeskApiFactory _factory;
    public ComprasApiTests(RepairDeskApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Create_FornecedorUe_UsaTaxaZeroELucrosPorDefeito()
    {
        var client = await AuthedClient();
        var ue = await CriarFornecedor(client, RegimeIvaFornecedor.UeAutoliquidacao);

        var resp = await client.PostAsJsonAsync("/api/compras", Doc(ue, "INV-" + Guid.NewGuid().ToString("N")[..6],
            new CompraLinhaWriteRequest(null, "Display Samsung S23 lavanda", 1, 91.95m, null, null, "Gaveta A1"),
            new CompraLinhaWriteRequest(null, "Vidro temperado Samsung A13 5G", 3, 0.60m, null, null, null)));

        resp.StatusCode.Should().Be(HttpStatusCode.Created);
        var doc = (await resp.Content.ReadFromJsonAsync<CompraDocumentoDto>())!;
        doc.RegimeIva.Should().Be(RegimeIvaFornecedor.UeAutoliquidacao);
        var s23 = doc.Linhas.Single(l => l.Descricao.Contains("S23"));
        s23.TaxaIvaCompra.Should().Be(0m);
        s23.LucroUnitario.Should().Be(5m);
        Math.Round(s23.PrecoFinalComIva, 2).Should().Be(119.25m);
        Math.Round(s23.AutoliquidacaoUe, 2).Should().Be(21.15m);
        doc.Linhas.Single(l => l.Descricao.Contains("Vidro")).LucroUnitario.Should().Be(1m);
        doc.UnidadesEmStock.Should().Be(4);
    }

    [Fact]
    public async Task Create_FaturaRepetida_Bloqueia_SalvoIgnorarDuplicado()
    {
        var client = await AuthedClient();
        var nac = await CriarFornecedor(client, RegimeIvaFornecedor.Nacional);
        var numero = "FCTR2026A/" + Random.Shared.Next(1000, 9999);
        var linha = new CompraLinhaWriteRequest(null, "Bateria Samsung A02s", 1, 8.40m, null, null, null);

        (await client.PostAsJsonAsync("/api/compras", Doc(nac, numero, linha))).StatusCode.Should().Be(HttpStatusCode.Created);

        var dup = await client.PostAsJsonAsync("/api/compras", Doc(nac, numero.ToLowerInvariant(), linha));
        dup.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await dup.Content.ReadAsStringAsync()).Should().Contain("compra_duplicada");

        var forcado = await client.PostAsJsonAsync("/api/compras", Doc(nac, numero, linha) with { IgnorarDuplicado = true });
        forcado.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Create_EncomendaJaUsadaNoutraFatura_Bloqueia()
    {
        var client = await AuthedClient();
        var nac = await CriarFornecedor(client, RegimeIvaFornecedor.Nacional);
        var linha = new CompraLinhaWriteRequest(null, "Touch iPhone 5s", 1, 10.95m, null, null, null);
        var enc = Random.Shared.Next(100000, 999999).ToString();

        var primeiro = Doc(nac, null, linha) with { NumerosEncomenda = [$"#{enc}"] };
        (await client.PostAsJsonAsync("/api/compras", primeiro)).StatusCode.Should().Be(HttpStatusCode.Created);

        // Uma fatura que junta duas encomendas, uma delas já registada.
        var fatura = Doc(nac, "FRE A01/" + enc, linha) with { NumerosEncomenda = [$"{enc} + 999{enc}"] };
        (await client.PostAsJsonAsync("/api/compras", fatura)).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Get_SemNumeroFatura_FicaComoFaturaEmFalta_EReconcilia()
    {
        var client = await AuthedClient();
        var nac = await CriarFornecedor(client, RegimeIvaFornecedor.Nacional);
        var req = Doc(nac, null,
                new CompraLinhaWriteRequest(null, "Ecrã A15", 1, 15.21m, null, null, null),
                new CompraLinhaWriteRequest(null, "Bateria A15", 1, 2.57m, null, null, null))
            with { NumerosEncomenda = ["MWPT 11352"], PortesPagos = 3.92m, PortesIva = 0.73m, TotalDocumento = 21.70m };

        var doc = (await (await client.PostAsJsonAsync("/api/compras", req)).Content.ReadFromJsonAsync<CompraDocumentoDto>())!;

        doc.FaturaEmFalta.Should().BeTrue();
        doc.Diferenca.Should().Be(0.00m);
        doc.TemDiferenca.Should().BeFalse();

        var emFalta = await client.GetFromJsonAsync<PagedResult<CompraDocumentoDto>>($"/api/compras?faturaEmFalta=true&fornecedorId={nac}");
        emFalta!.Items.Should().ContainSingle(d => d.Id == doc.Id);
    }

    [Fact]
    public async Task Update_AtualizaRemoveEAcrescentaLinhas()
    {
        var client = await AuthedClient();
        var nac = await CriarFornecedor(client, RegimeIvaFornecedor.Nacional);
        var doc = (await (await client.PostAsJsonAsync("/api/compras", Doc(nac, "U-" + Guid.NewGuid().ToString("N")[..6],
            new CompraLinhaWriteRequest(null, "Linha A", 1, 5m, null, null, null),
            new CompraLinhaWriteRequest(null, "Linha B", 1, 6m, null, null, null)))).Content.ReadFromJsonAsync<CompraDocumentoDto>())!;
        var a = doc.Linhas.Single(l => l.Descricao == "Linha A");

        var update = Doc(nac, doc.NumeroFatura,
            new CompraLinhaWriteRequest(a.Id, "Linha A editada", 2, 5m, 0.23m, 7m, null),
            new CompraLinhaWriteRequest(null, "Linha C", 1, 1m, null, null, null));
        var resp = await client.PutAsJsonAsync($"/api/compras/{doc.Id}", update);

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var atualizado = (await resp.Content.ReadFromJsonAsync<CompraDocumentoDto>())!;
        atualizado.Linhas.Select(l => l.Descricao).Should().BeEquivalentTo(["Linha A editada", "Linha C"]);
        atualizado.Linhas.Single(l => l.Id == a.Id).Quantidade.Should().Be(2);
    }

    [Fact]
    public async Task Inventario_E_Resumo_RefletemOsLotes()
    {
        var client = await AuthedClient();
        var nac = await CriarFornecedor(client, RegimeIvaFornecedor.Nacional);
        var doc = (await (await client.PostAsJsonAsync("/api/compras", Doc(nac, "R-" + Guid.NewGuid().ToString("N")[..6],
            new CompraLinhaWriteRequest(null, "Touch e lcd iPhone 5s preto", 1, 10.95m, null, null, null)))).Content.ReadFromJsonAsync<CompraDocumentoDto>())!;

        var inventario = await client.GetFromJsonAsync<List<InventarioLinhaDto>>("/api/compras/inventario");
        var linha = inventario!.Single(i => i.DocumentoId == doc.Id);
        Math.Round(linha.PrecoFinalComIva, 2).Should().Be(17.10m);
        Math.Round(linha.IvaAPagarEstado, 2).Should().Be(1.15m);

        var resumo = await client.GetFromJsonAsync<ResumoComprasDto>("/api/compras/resumo");
        resumo!.TaxaIvaVenda.Should().Be(0.23m);
        resumo.EmStock.Unidades.Should().BeGreaterThanOrEqualTo(1);
        resumo.PorFornecedor.Should().Contain(f => f.FornecedorId == nac && f.Documentos == 1);
    }

    [Fact]
    public async Task Simulador_PocoF3VendidoA100()
    {
        var client = await AuthedClient();
        var resp = await client.PostAsJsonAsync("/api/compras/simulador",
            new SimuladorRequest(16.42m, RegimeIvaFornecedor.UeAutoliquidacao, 5m, 100m));
        var r = (await resp.Content.ReadFromJsonAsync<SimuladorResponse>())!;

        Math.Round(r.VendaIvaAPagar!.Value, 2).Should().Be(18.70m);
        Math.Round(r.VendaLucro!.Value, 2).Should().Be(64.88m);
    }

    [Fact]
    public async Task ImportarExcel_CriaFornecedoresEDocumentos_EEIdempotente()
    {
        var client = await AuthedClient();
        var sufixo = Guid.NewGuid().ToString("N")[..5];
        var xlsx = BuildXlsx(sufixo);

        var primeira = await PostExcel(client, xlsx);
        primeira.DocumentosCriados.Should().Be(2);
        primeira.Linhas.Should().Be(3);
        primeira.FornecedoresCriados.Should().Be(2);
        primeira.Avisos.Should().Contain(a => a.Contains("Faturado = Sim"));

        var segunda = await PostExcel(client, xlsx);
        segunda.DocumentosCriados.Should().Be(0);
        segunda.DocumentosJaExistentes.Should().Be(2);

        var fornecedores = await client.GetFromJsonAsync<List<FornecedorDto>>("/api/fornecedores");
        fornecedores!.Single(f => f.Name == $"MobileSentrix {sufixo}").RegimeIva.Should().Be(RegimeIvaFornecedor.UeAutoliquidacao);
        fornecedores.Single(f => f.Name == $"MobileSentrix {sufixo}").Pais.Should().Be("NL");
    }

    [Fact]
    public async Task DeFatura_CriaCompraLigadaEFechaAImportacao()
    {
        var client = await AuthedClient();
        var ue = await CriarFornecedor(client, RegimeIvaFornecedor.UeAutoliquidacao);
        Guid importId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var import = new SupplierInvoiceImport
            {
                TenantId = RepairDeskApiFactory.TenantId,
                FornecedorNameRaw = "Utopya",
                PdfSha256 = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N"),
                PdfRelativePath = "2026/10/utopya/fatura.pdf",
                ParsedTotalCents = 9195,
            };
            db.SupplierInvoiceImports.Add(import);
            await db.SaveChangesAsync();
            importId = import.Id;
        }

        var req = Doc(ue, "UT-" + Guid.NewGuid().ToString("N")[..6],
            new CompraLinhaWriteRequest(null, "Display Samsung S23", 1, 91.95m, null, null, null));
        var resp = await client.PostAsJsonAsync($"/api/compras/de-fatura/{importId}", req);
        resp.StatusCode.Should().Be(HttpStatusCode.Created, await resp.Content.ReadAsStringAsync());
        var doc = (await resp.Content.ReadFromJsonAsync<CompraDocumentoDto>())!;
        doc.SupplierInvoiceImportId.Should().Be(importId);

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var import = await db.SupplierInvoiceImports.IgnoreQueryFilters().SingleAsync(i => i.Id == importId);
            import.Status.Should().Be(SupplierInvoiceImportStatus.Approved);
            import.FornecedorId.Should().Be(ue);
        }

        // Segunda aprovação da mesma fatura → 409.
        var outra = await client.PostAsJsonAsync($"/api/compras/de-fatura/{importId}", req with { IgnorarDuplicado = true });
        outra.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // ---------- helpers ----------

    private static CompraDocumentoWriteRequest Doc(Guid fornecedorId, string? numero, params CompraLinhaWriteRequest[] linhas)
        => new(fornecedorId, new DateTime(2026, 9, 15), numero, null, "MB Way", 0m, null, null, null, linhas);

    private async Task<Guid> CriarFornecedor(HttpClient client, RegimeIvaFornecedor regime)
    {
        var resp = await client.PostAsJsonAsync("/api/fornecedores",
            new FornecedorWriteRequest($"Forn {Guid.NewGuid():N}", null, null, null, null, null, null, true, false, regime, regime == RegimeIvaFornecedor.Nacional ? "PT" : "NL"));
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<FornecedorDto>())!.Id;
    }

    private async Task<HttpClient> AuthedClient()
    {
        var client = _factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(RepairDeskApiFactory.AdminEmail, RepairDeskApiFactory.AdminPassword));
        var auth = (await login.Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }

    private static async Task<ImportComprasResultado> PostExcel(HttpClient client, byte[] xlsx)
    {
        using var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(xlsx);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        content.Add(file, "ficheiro", "compras.xlsx");
        var resp = await client.PostAsync("/api/compras/importar-excel", content);
        resp.StatusCode.Should().Be(HttpStatusCode.OK, await resp.Content.ReadAsStringAsync());
        return (await resp.Content.ReadFromJsonAsync<ImportComprasResultado>())!;
    }

    /// <summary>xlsx mínimo com o formato do Excel do Bruno (strings inline, datas em nº de série).</summary>
    private static byte[] BuildXlsx(string sufixo)
    {
        var ms = $"MobileSentrix {sufixo}";
        var mw = $"Microwire {sufixo}";
        const int data = 46206; // 2026-07-01
        var sheets = new (string Name, string[][] Rows)[]
        {
            ("Fornecedores", [
                ["Fornecedor", "País", "Regime", "IVA na compra"],
                [ms, "Países Baixos", "UE – autoliquidação", "0"],
                [mw, "Portugal", "Nacional", "0.23"],
            ]),
            ("Faturas", [
                ["Fornecedor", "Data", "Nº Fatura", "Nº Encomenda", "Pagamento", "Regime", "Artigos", "Portes", "IVA dos portes", "Total calc", "Total documento", "Dif", "Estado", "Notas"],
                [ms, $"{data}", $"Enc. 6002{sufixo.Length}0227", $"6002{sufixo.Length}0227", "PayPal", "UE", "", "6.95", "0", "", "23.37", "", "Pago", "Resumo de encomenda"],
                [mw, $"{data}", $"FCTR-{sufixo}", "MWPT 1", "Pronto pagamento", "Nacional", "", "3.92", "0.73", "", "", "", "", ""],
            ]),
            ("Compras", [
                ["", "", "", "", "", "", "Preço Compra C/IVA"],
                ["Fornecedor", "Data compra", "Nº Fatura", "Descrição", "Qtd", "IVA compra", "C/IVA Unitário", "", "", "", "Faturado", "", "Lucro"],
                [ms, $"{data}", $"Enc. 6002{sufixo.Length}0227", "Touch e lcd Xiaomi Poco F3", "1", "0", "16.42", "", "", "", "sim", "", "5"],
                [mw, $"{data}", $"FCTR-{sufixo}", "Bateria Samsung A02s", "1", "0.23", "8.4", "", "", "", "Não", "", "5"],
                [mw, $"{data}", $"FCTR-{sufixo}", "Vidro temperado A13", "3", "0.23", "0.738", "", "", "", "Não", "", "1"],
            ]),
        };

        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            void Add(string path, string xml)
            {
                using var w = new StreamWriter(zip.CreateEntry(path).Open(), new UTF8Encoding(false));
                w.Write(xml);
            }

            Add("[Content_Types].xml", "<?xml version=\"1.0\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"/>");
            var wb = new StringBuilder("<?xml version=\"1.0\"?><workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets>");
            var rels = new StringBuilder("<?xml version=\"1.0\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">");
            for (var i = 0; i < sheets.Length; i++)
            {
                wb.Append($"<sheet name=\"{sheets[i].Name}\" sheetId=\"{i + 1}\" r:id=\"rId{i + 1}\"/>");
                rels.Append($"<Relationship Id=\"rId{i + 1}\" Type=\"worksheet\" Target=\"worksheets/sheet{i + 1}.xml\"/>");
                var sd = new StringBuilder("<?xml version=\"1.0\"?><worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><sheetData>");
                for (var r = 0; r < sheets[i].Rows.Length; r++)
                {
                    sd.Append($"<row r=\"{r + 1}\">");
                    for (var c = 0; c < sheets[i].Rows[r].Length; c++)
                    {
                        var v = sheets[i].Rows[r][c];
                        if (v.Length == 0) continue;
                        var col = (char)('A' + c);
                        sd.Append(decimal.TryParse(v, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out _)
                            ? $"<c r=\"{col}{r + 1}\"><v>{v}</v></c>"
                            : $"<c r=\"{col}{r + 1}\" t=\"inlineStr\"><is><t>{System.Security.SecurityElement.Escape(v)}</t></is></c>");
                    }
                    sd.Append("</row>");
                }
                sd.Append("</sheetData></worksheet>");
                Add($"xl/worksheets/sheet{i + 1}.xml", sd.ToString());
            }
            Add("xl/workbook.xml", wb.Append("</sheets></workbook>").ToString());
            Add("xl/_rels/workbook.xml.rels", rels.Append("</Relationships>").ToString());
        }
        return buffer.ToArray();
    }
}
