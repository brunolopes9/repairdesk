using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using RepairDesk.API.Infrastructure;
using RepairDesk.Core.Enums;
using RepairDesk.Services.Compras;
using RepairDesk.Services.Despesas;
using RepairDesk.Services.Fornecedores;
using RepairDesk.Tests.Auth;

namespace RepairDesk.Tests.Compras;

/// <summary>Sprint 688: fatura mista — linhas da compra passam a despesa ligada, com a parte proporcional dos portes.</summary>
public class FaturaMistaTests : IClassFixture<RepairDeskApiFactory>
{
    private readonly RepairDeskApiFactory _factory;
    public FaturaMistaTests(RepairDeskApiFactory factory) => _factory = factory;

    [Fact]
    public async Task PassarLinhas_ParaDespesa_DivideOsPortes_ELigaACompra()
    {
        var client = await AuthedClient();
        var forn = await Post<FornecedorDto>(client, "/api/fornecedores",
            new FornecedorWriteRequest($"Forn {Guid.NewGuid():N}", null, null, null, null, null, null, true, false, RegimeIvaFornecedor.Nacional, null));
        // Nacional: preços com IVA a 23%. Linhas 80 € (stock) + 20 € (cola, despesa); portes 5 € com 0,93 € de IVA.
        var compra = await Post<CompraDocumentoDto>(client, "/api/compras", new CompraDocumentoWriteRequest(forn.Id, new DateTime(2026, 11, 6),
            "FT-" + Guid.NewGuid().ToString("N")[..8], null, null, 5m, 0.93m, 105m, null,
            [new CompraLinhaWriteRequest(null, "Ecrã Samsung A52", 2, 40m, null, null, null),
             new CompraLinhaWriteRequest(null, "Cola B7000", 1, 20m, null, null, null)]));
        var cola = compra.Linhas.Single(l => l.Descricao == "Cola B7000").Id;

        var r = await Post<PassarParaDespesaResultado>(client, $"/api/compras/{compra.Id}/passar-a-despesa",
            new PassarParaDespesaRequest([cola], null, DespesaCategoria.Material, DespesaNatureza.Gasto));
        r.PortesDaDespesa.Should().Be(1.00m);      // 20 % das linhas → 20 % dos 5 € de portes
        r.Despesa.Valor.Should().Be(21.00m);
        r.Despesa.Iva.Should().Be(3.93m);          // 20 × 0,23/1,23 = 3,74 + 0,19 dos portes
        r.TotalQueFicaNaCompra.Should().Be(84m);

        var depois = (await client.GetFromJsonAsync<CompraDocumentoDto>($"/api/compras/{compra.Id}"))!;
        depois.Linhas.Should().ContainSingle(l => l.Descricao == "Ecrã Samsung A52");
        depois.PortesPagos.Should().Be(4m);
        depois.TemDiferenca.Should().BeFalse("o total que fica na compra bate com as linhas + portes");

        var ligadas = (await client.GetFromJsonAsync<List<DespesaLigadaDto>>($"/api/compras/{compra.Id}/despesas"))!;
        ligadas.Should().ContainSingle(d => d.Id == r.Despesa.Id);
        var despesa = (await client.GetFromJsonAsync<DespesaDto>($"/api/despesas/{r.Despesa.Id}"))!;
        despesa.CompraNumero.Should().Be(compra.Numero);
        despesa.CompraDocumentoId.Should().Be(compra.Id);

        // Desligar e voltar a ligar pelo nº visível.
        (await Put<DespesaDto>(client, $"/api/despesas/{despesa.Id}/compra", new LigarCompraRequest(null))).CompraNumero.Should().BeNull();
        (await Put<DespesaDto>(client, $"/api/despesas/{despesa.Id}/compra", new LigarCompraRequest(compra.Numero))).CompraNumero.Should().Be(compra.Numero);

        // Todas as linhas → recusa (a fatura seria toda despesa).
        var todas = await client.PostAsJsonAsync($"/api/compras/{compra.Id}/passar-a-despesa",
            new PassarParaDespesaRequest(depois.Linhas.Select(l => l.Id).ToList()));
        todas.StatusCode.Should().Be(HttpStatusCode.Conflict);
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
