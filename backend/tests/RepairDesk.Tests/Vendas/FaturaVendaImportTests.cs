using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using RepairDesk.API.Infrastructure;
using RepairDesk.Core.Enums;
using RepairDesk.Services.Clientes;
using RepairDesk.Services.Documents;
using RepairDesk.Services.Vendas;
using RepairDesk.Tests.Auth;

namespace RepairDesk.Tests.Vendas;

/// <summary>
/// Sprint 565: faturas de venda emitidas no Moloni entram pelo PDF — criam a venda entregue com a fatura,
/// ou ficam anexadas à venda que já tem esse nº. Textos sintéticos com o layout do Moloni (sem dados reais).
/// </summary>
public class FaturaVendaImportTests : IClassFixture<RepairDeskApiFactory>
{
    private readonly RepairDeskApiFactory _factory;
    public FaturaVendaImportTests(RepairDeskApiFactory factory) => _factory = factory;

    private static string Fatura(string numero, string data, string[] cliente, string nifLinha, string[] linhas, string totalIliquido, string totalPagar, string? resumo = null)
    {
        var via = string.Join('\n', new[]
        {
            "Powered by Moloni ON | www.molonion.pt", "Original", $"Fatura {numero}", $"Data de emissão: {data}",
            "Loja Teste", "Rua do Teste 1", "1000-001 LISBOA", "Portugal", "Internet: www.exemplo.pt", "NIF: 500000000",
        }.Concat(cliente).Concat(new[]
        {
            "ATCUD: XXXX-1", "abcd - Processado por programa certificado Nº 3075/AT", "NIF Código Moeda Data vto. Página", nifLinha,
            "Ref. Designação Qtd. Unidade Preço Imposto Total ilíquido",
        }).Concat(linhas).Concat(new[] { $"Total ilíquido {totalIliquido}", $"Total a pagar {totalPagar}", resumo ?? "Resumo dos impostos", "1/1" }));
        return via + "\nDuplicado\n" + via; // o PDF do Moloni repete tudo no "Duplicado"
    }

    private static readonly string Manutencao = Fatura("FT SOFTWARE/11", "11/10/2026",
        ["Associação de", "Desenvolvimento Local", "Teste Plus", "Rua Aquilino Ribeiro", "3620-330 Moimenta da Beira", "Portugal"],
        "513000000 2 EUR 12/10/2026",
        Enumerable.Repeat("MANUT-SRV Alojamento, manutenção e suporte técnico — website e aplicação 1 un 29,50€ 23% 29,50€", 12).ToArray(),
        "354,00€", "435,42€");

    [Fact]
    public void Parser_LeSoOOriginal_ClienteEmVariasLinhas_EJuntaOTotal()
    {
        var f = MoloniFaturaParser.Parse(Manutencao)!;
        f.Numero.Should().Be("FT SOFTWARE/11");
        f.Data.Should().Be(new DateTime(2026, 10, 11));
        f.ClienteNome.Should().Be("Associação de Desenvolvimento Local Teste Plus");
        f.ClienteNif.Should().Be("513000000");
        f.ClienteMorada.Should().Be("Rua Aquilino Ribeiro, 3620-330 Moimenta da Beira");
        f.Linhas.Should().HaveCount(12, "o Duplicado não conta");
        f.TotalCents.Should().Be(43542);
    }

    [Fact]
    public void Parser_Isento_DescricaoEmVariasLinhas_EMotivo()
    {
        var texto = Fatura("FT TRADING/7", "22/10/2026", ["EMPRESA ESTRANGEIRA LTD", "168 Praed Street", "W2 1RH London", "Reino Unido"],
            "12553363 8 EUR 22/10/2026",
            ["REWARD3", "Reward – conta 123 – payout 1.544,00 USD (BCE", "1,1195)", "1 un 1.379,19€ Isento (1) 1.379,19€"],
            "1.379,19€", "1.379,19€Resumo dos impostos", "(1) M40 - IVA - Autoliquidação - 1.379,19€ 0,00€");
        var f = MoloniFaturaParser.Parse(texto)!;
        f.ClienteNome.Should().Be("EMPRESA ESTRANGEIRA LTD");
        f.Linhas.Single().Should().BeEquivalentTo(new FaturaVendaLinha("REWARD3 Reward – conta 123 – payout 1.544,00 USD (BCE 1,1195)", 1, 137919, 0));
        f.MotivoIsencao.Should().Be("M40 - IVA - Autoliquidação");
        f.TotalCents.Should().Be(137919);
    }

    [Fact]
    public void Parser_ConsumidorFinal_SemNif()
    {
        var f = MoloniFaturaParser.Parse(Fatura("FT REPARACAO/9", "28/09/2026", ["Consumidor final", "Portugal"], "Consumidor final 1 EUR 28/09/2026",
            ["IP 13 PM Substituição Bateria 1 un 45,00€ 23% 45,00€"], "45,00€", "55,35€"))!;
        f.ConsumidorFinal.Should().BeTrue();
        f.ClienteNif.Should().BeNull();
    }

    [Fact]
    public async Task Importar_CriaVendaEntregueComFatura_IdempotenteEExportavel()
    {
        var client = await AuthedClient();
        var numero = $"FT SOFTWARE/{Random.Shared.Next(1000, 9999)}";
        var texto = Fatura(numero, "11/10/2026",
            ["Associação de", "Desenvolvimento Local", "Teste Plus", "Rua Aquilino Ribeiro", "3620-330 Moimenta da Beira", "Portugal"],
            $"5{Random.Shared.Next(10000000, 99999999)} 2 EUR 12/10/2026",
            Enumerable.Repeat("MANUT-SRV Alojamento, manutenção e suporte técnico — website e aplicação 1 un 29,50€ 23% 29,50€", 12).ToArray(),
            "354,00€", "435,42€");
        var pdf = Pdf(texto);
        MoloniFaturaParser.Parse(PdfTextExtractor.Extract(new MemoryStream(pdf), "t.pdf").Text!)
            .Should().NotBeNull("o PDF gerado tem de ler como uma fatura do Moloni");

        var r = await Importar(client, pdf);
        r.Resultado.Should().Be("criada");
        r.Aviso.Should().BeNull();
        var venda = await client.GetFromJsonAsync<VendaDto>($"/api/vendas/{r.VendaId}");
        venda!.Estado.Should().Be(VendaEstado.Entregue);
        venda.InvoiceNumber.Should().Be(numero);
        venda.Data.Date.Should().Be(new DateTime(2026, 10, 11));
        venda.TotalCents.Should().Be(43542);
        venda.IvaCents.Should().Be(8142, "o IVA bate com a fatura");
        venda.Cliente!.Nome.Should().Be("Associação de Desenvolvimento Local Teste Plus");
        venda.TemFaturaPdf.Should().BeTrue();
        venda.FaturaPorRegistar.Should().BeFalse();

        // Mesma fatura outra vez: não duplica.
        (await Importar(client, pdf)).Should().BeEquivalentTo(new { r.VendaId, Resultado = "repetida" });

        (await client.GetAsync($"/api/vendas/{r.VendaId}/fatura.pdf")).StatusCode.Should().Be(HttpStatusCode.OK);
        var zipResp = await client.GetAsync("/api/vendas/faturas/export?from=2026-10-01&to=2026-10-31");
        zipResp.EnsureSuccessStatusCode();
        using var zip = new ZipArchive(await zipResp.Content.ReadAsStreamAsync());
        zip.Entries.Select(e => e.FullName).Should().Contain(
            $"2026-10/Associação de Desenvolvimento Local Teste Plus/2026-10-11 {numero.Replace('/', '-')}.pdf");
    }

    [Fact]
    public async Task Importar_VendaJaRegistadaComEsseNumero_SoAnexaOPdf()
    {
        var client = await AuthedClient();
        var numero = $"FT REPARACAO/{Random.Shared.Next(1000, 9999)}";
        var resp = await client.PostAsJsonAsync("/api/vendas", new VendaWriteRequest(VendaTipo.Servico, null, null, null, null,
            [new VendaLinhaWriteRequest(null, null, "Substituição bateria", 1, 5535, 0, 23m)], VendaEstado.Entregue, PaymentMethod.MBWay));
        resp.EnsureSuccessStatusCode();
        var manual = (await resp.Content.ReadFromJsonAsync<VendaDto>())!;
        (await client.PutAsJsonAsync($"/api/vendas/{manual.Id}/fatura", new RegistarFaturaRequest(numero, new DateTime(2026, 9, 28)))).EnsureSuccessStatusCode();

        var r = await Importar(client, Pdf(Fatura(numero, "28/09/2026", ["Consumidor final", "Portugal"], "Consumidor final 1 EUR 28/09/2026",
            ["IP Substituição Bateria 1 un 45,00€ 23% 45,00€"], "45,00€", "55,35€")));

        r.Should().BeEquivalentTo(new { VendaId = manual.Id, Resultado = "ligada", Aviso = (string?)null });
        (await client.GetFromJsonAsync<VendaDto>($"/api/vendas/{manual.Id}"))!.TemFaturaPdf.Should().BeTrue();
    }

    [Fact]
    public async Task Importar_PdfQueNaoEFaturaMoloni_DaErroClaro()
    {
        var client = await AuthedClient();
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(Pdf("Fatura de outra coisa\nTotal 10,00€"));
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(file, "file", "x.pdf");
        var resp = await client.PostAsync("/api/vendas/importar-fatura", form);
        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await resp.Content.ReadAsStringAsync()).Should().Contain("nao_e_fatura_moloni");
    }

    private static async Task<VendaFaturaImportResult> Importar(HttpClient client, byte[] pdf)
    {
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(pdf);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(file, "file", "fatura.pdf");
        var resp = await client.PostAsync("/api/vendas/importar-fatura", form);
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<VendaFaturaImportResult>())!;
    }

    private static byte[] Pdf(string texto)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        return Document.Create(c => c.Page(p =>
        {
            p.Margin(20);
            p.DefaultTextStyle(t => t.FontSize(7));
            p.Content().Column(col =>
            {
                foreach (var l in texto.Split('\n')) col.Item().Text(l);
            });
        })).GeneratePdf();
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
