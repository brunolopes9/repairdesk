using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using QuestPDF.Fluent;
using RepairDesk.API.Infrastructure;
using RepairDesk.Core.Enums;
using RepairDesk.Services.Despesas;
using RepairDesk.Tests.Auth;
using UglyToad.PdfPig;

namespace RepairDesk.Tests.Impressao;

/// <summary>Sprint 593: imprimir uma lista inteira num só PDF — a lista com totais e, a seguir, as faturas.</summary>
[Collection("Pdf")]
public class ImpressaoTests : IClassFixture<RepairDeskApiFactory>
{
    private readonly RepairDeskApiFactory _factory;
    public ImpressaoTests(RepairDeskApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Despesas_ListaEFaturasNumSoPdf_ComAsSemPdfAssinaladas()
    {
        var client = await LoginAsync();
        var marca = "Impressao" + Guid.NewGuid().ToString("N")[..8];

        var comPdf = await CriarAsync(client, $"{marca} renda", 12300);
        (await client.PostAsync($"/api/despesas/{comPdf.Id}/pdf", Form(FaturaPdf("Fatura da renda")))).EnsureSuccessStatusCode();
        await CriarAsync(client, $"{marca} café", 150);

        var tudo = await client.GetAsync($"/api/imprimir/despesas?q={marca}&from=2026-11-01&to=2026-11-30");
        tudo.StatusCode.Should().Be(HttpStatusCode.OK, await tudo.Content.ReadAsStringAsync());
        tudo.Content.Headers.ContentType!.MediaType.Should().Be("application/pdf");
        using (var pdf = PdfDocument.Open(await tudo.Content.ReadAsByteArrayAsync()))
        {
            pdf.NumberOfPages.Should().Be(2, "a lista + a fatura da renda (o café não tem PDF)");
            var lista = pdf.GetPage(1).Text;
            lista.Should().Contain("Despesas").And.Contain("2 documento(s)").And.Contain("sem PDF").And.Contain("124,50");
            pdf.GetPage(2).Text.Should().Contain("Fatura da renda");
        }

        var soLista = await client.GetAsync($"/api/imprimir/despesas?q={marca}&conteudo=lista");
        using (var pdf = PdfDocument.Open(await soLista.Content.ReadAsByteArrayAsync())) pdf.NumberOfPages.Should().Be(1);

        var soFaturas = await client.GetAsync($"/api/imprimir/despesas?q={marca}&conteudo=faturas");
        using (var pdf = PdfDocument.Open(await soFaturas.Content.ReadAsByteArrayAsync())) pdf.GetPage(1).Text.Should().Contain("Fatura da renda");

        var nada = await client.GetAsync($"/api/imprimir/despesas?q=naoexiste{marca}");
        nada.IsSuccessStatusCode.Should().BeFalse();
        (await nada.Content.ReadAsStringAsync()).Should().Contain("sem_documentos");
    }

    private async Task<HttpClient> LoginAsync()
    {
        var client = _factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(RepairDeskApiFactory.AdminEmail, RepairDeskApiFactory.AdminPassword));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await login.Content.ReadFromJsonAsync<AuthResponse>())!.AccessToken);
        return client;
    }

    private static async Task<DespesaDto> CriarAsync(HttpClient client, string descricao, int cents)
    {
        var r = await client.PostAsJsonAsync("/api/despesas", new CreateDespesaRequest(descricao, DespesaCategoria.Outro, cents,
            new DateTime(2026, 11, 4), "Fornecedor teste", null, null, IvaCents: 0));
        r.EnsureSuccessStatusCode();
        return (await r.Content.ReadFromJsonAsync<DespesaDto>())!;
    }

    private static byte[] FaturaPdf(string texto)
        => Document.Create(c => c.Page(p => p.Content().Text($"{texto} {Guid.NewGuid()}"))).GeneratePdf();

    private static MultipartFormDataContent Form(byte[] pdf)
    {
        var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(pdf);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        content.Add(file, "file", "fatura.pdf");
        return content;
    }
}
