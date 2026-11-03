using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using FluentAssertions;
using RepairDesk.API.Infrastructure;
using RepairDesk.Core.Enums;
using RepairDesk.Services.Clientes;
using RepairDesk.Services.Despesas;
using RepairDesk.Tests.Auth;

namespace RepairDesk.Tests.Despesas;

/// <summary>Sprint 586: a despesa tem o PDF da fatura para abrir e imprimir, como as compras; anexa-se às criadas à mão.</summary>
public class DespesaPdfTests : IClassFixture<RepairDeskApiFactory>
{
    private readonly RepairDeskApiFactory _factory;
    public DespesaPdfTests(RepairDeskApiFactory factory) => _factory = factory;

    [Fact]
    public async Task AnexarPdf_DespesaPassaATerPdf_QueAbre_ENaoSeAnexaDuasVezes()
    {
        var client = _factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(RepairDeskApiFactory.AdminEmail, RepairDeskApiFactory.AdminPassword));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await login.Content.ReadFromJsonAsync<AuthResponse>())!.AccessToken);

        var r = await client.PostAsJsonAsync("/api/despesas", new CreateDespesaRequest("Material elétrico", DespesaCategoria.Outro, 1683,
            new DateTime(2026, 11, 2), "Loja de eletrónica", "FT V/" + Random.Shared.Next(100000, 999999), null, IvaCents: 315));
        r.EnsureSuccessStatusCode();
        var despesa = (await r.Content.ReadFromJsonAsync<DespesaDto>())!;
        despesa.SupplierInvoiceImportId.Should().BeNull();

        var pdf = Pdf("fatura " + Guid.NewGuid());
        var anexar = await client.PostAsync($"/api/despesas/{despesa.Id}/pdf", Form(pdf));
        anexar.StatusCode.Should().Be(HttpStatusCode.OK, await anexar.Content.ReadAsStringAsync());
        var comPdf = (await anexar.Content.ReadFromJsonAsync<DespesaDto>())!;
        comPdf.SupplierInvoiceImportId.Should().NotBeNull();

        // Aparece na lista (é daí que a tabela abre o PDF) e abre o ficheiro que foi anexado.
        var lista = await client.GetFromJsonAsync<PagedResult<DespesaDto>>($"/api/despesas?q={Uri.EscapeDataString(despesa.NumeroEncomenda!)}");
        lista!.Items.Single(x => x.Id == despesa.Id).SupplierInvoiceImportId.Should().Be(comPdf.SupplierInvoiceImportId);
        var aberto = await client.GetAsync($"/api/supplier-invoices/{comPdf.SupplierInvoiceImportId}/pdf");
        aberto.Content.Headers.ContentType!.MediaType.Should().Be("application/pdf");
        (await aberto.Content.ReadAsByteArrayAsync()).Should().Equal(pdf);

        // Segundo anexo na mesma despesa e o mesmo PDF noutra despesa: recusados.
        (await client.PostAsync($"/api/despesas/{despesa.Id}/pdf", Form(Pdf("outra " + Guid.NewGuid())))).StatusCode.Should().Be(HttpStatusCode.Conflict);
        var outra = (await (await client.PostAsJsonAsync("/api/despesas", new CreateDespesaRequest("Outra", DespesaCategoria.Outro, 100,
            new DateTime(2026, 11, 2), null, null, null))).Content.ReadFromJsonAsync<DespesaDto>())!;
        (await client.PostAsync($"/api/despesas/{outra.Id}/pdf", Form(pdf))).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    private static byte[] Pdf(string texto) => Encoding.ASCII.GetBytes($"%PDF-1.4\n% {texto}\n%%EOF\n");

    private static MultipartFormDataContent Form(byte[] pdf)
    {
        var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(pdf);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        content.Add(file, "file", "fatura.pdf");
        return content;
    }
}
