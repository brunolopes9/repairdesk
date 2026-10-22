using System.IO.Compression;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RepairDesk.API.Infrastructure;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;
using RepairDesk.DAL.Persistence;
using RepairDesk.Services.Documents;
using RepairDesk.Tests.Auth;

namespace RepairDesk.Tests.Documents;

/// <summary>
/// Sprint 564: faturas que o leitor não conhece (Leroy, Ikea, Amazon…) ficam com a data/nº/fornecedor
/// confirmados na aprovação, e o ZIP para o contabilista organiza-se por esses dados (e filtra por fornecedor).
/// </summary>
public class FaturaExportApiTests : IClassFixture<RepairDeskApiFactory>
{
    private readonly RepairDeskApiFactory _factory;
    public FaturaExportApiTests(RepairDeskApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Aprovar_FaturaIlegivel_GuardaDadosConfirmados_EExportOrganizaPorEles()
    {
        var client = await AuthedClient();
        var nomeForn = $"Leroy Teste {Guid.NewGuid():N}"[..20];
        var ilegivel = await CriarImportSemLeitura();
        var outra = await CriarImportSemLeitura();

        var resp = await client.PostAsJsonAsync($"/api/supplier-invoices/{ilegivel}/approve", new ApproveSupplierInvoiceRequest(
            699, "Fita montagem", DespesaCategoria.Material, new DateTime(2026, 9, 12), nomeForn, "FT 20260431101/006004", null));
        resp.EnsureSuccessStatusCode();
        (await client.PostAsJsonAsync($"/api/supplier-invoices/{outra}/approve", new ApproveSupplierInvoiceRequest(
            1000, "Outra", DespesaCategoria.Material, new DateTime(2026, 9, 13), "Outro Forn Export", "X1", null))).EnsureSuccessStatusCode();

        Guid fornecedorId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var imp = await db.SupplierInvoiceImports.IgnoreQueryFilters().Include(i => i.Fornecedor).AsNoTracking().SingleAsync(i => i.Id == ilegivel);
            imp.ParsedDocumentDate.Should().Be(new DateTime(2026, 9, 12));
            imp.ParsedDocumentNumber.Should().Be("FT 20260431101/006004");
            imp.ParsedTotalCents.Should().Be(699);
            imp.Fornecedor!.Name.Should().Be(nomeForn);
            fornecedorId = imp.FornecedorId!.Value;
        }

        var zipResp = await client.GetAsync($"/api/supplier-invoices/export?from=2026-09-01&to=2026-09-30&fornecedorId={fornecedorId}");
        zipResp.EnsureSuccessStatusCode();
        using var zip = new ZipArchive(await zipResp.Content.ReadAsStreamAsync());
        var esperado = $"2026-09/{nomeForn}/2026-09-12 FT 20260431101-006004.pdf";
        zip.Entries.Select(e => e.FullName).Should().BeEquivalentTo(esperado, "indice.csv");
        using var indice = new StreamReader(zip.GetEntry("indice.csv")!.Open());
        var linhas = (await indice.ReadToEndAsync()).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        linhas.Should().HaveCount(2, "só a fatura deste fornecedor");
        linhas[1].Should().StartWith(esperado + ";" + nomeForn + ";FT 20260431101/006004;2026-09-12;6,99;Despesa");

        // Só compras: esta despesa fica de fora; só despesas: entra.
        (await Entradas(client, $"fornecedorId={fornecedorId}&tipo=Compras")).Should().Equal("indice.csv");
        (await Entradas(client, $"fornecedorId={fornecedorId}&tipo=despesas")).Should().Contain(esperado);
    }

    private static async Task<List<string>> Entradas(HttpClient client, string filtro)
    {
        var resp = await client.GetAsync($"/api/supplier-invoices/export?from=2026-09-01&to=2026-09-30&{filtro}");
        resp.EnsureSuccessStatusCode();
        using var zip = new ZipArchive(await resp.Content.ReadAsStreamAsync());
        return zip.Entries.Select(e => e.FullName).ToList();
    }

    /// <summary>Simula um PDF que o leitor não conhece: guardado no storage, sem data/nº/total/fornecedor.</summary>
    private async Task<Guid> CriarImportSemLeitura()
    {
        using var scope = _factory.Services.CreateScope();
        var storage = scope.ServiceProvider.GetRequiredService<ISupplierInvoiceStorage>();
        var path = await storage.SaveAsync(RepairDeskApiFactory.TenantId, "desconhecido", new DateTime(2026, 10, 22),
            $"{Guid.NewGuid():N}.pdf", "%PDF-1.4 teste"u8.ToArray());
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var import = new SupplierInvoiceImport
        {
            TenantId = RepairDeskApiFactory.TenantId,
            PdfSha256 = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N"),
            PdfRelativePath = path,
            ParseConfidence = "None",
            Status = SupplierInvoiceImportStatus.Failed,
        };
        db.SupplierInvoiceImports.Add(import);
        await db.SaveChangesAsync();
        return import.Id;
    }

    private async Task<HttpClient> AuthedClient()
    {
        var client = _factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(RepairDeskApiFactory.AdminEmail, RepairDeskApiFactory.AdminPassword));
        login.EnsureSuccessStatusCode();
        var auth = (await login.Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }
}
