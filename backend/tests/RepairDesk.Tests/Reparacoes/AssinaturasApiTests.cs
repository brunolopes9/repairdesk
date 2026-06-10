using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using RepairDesk.API.Infrastructure;
using RepairDesk.Services.Clientes;
using RepairDesk.Services.Reparacoes;
using RepairDesk.Tests.Auth;

namespace RepairDesk.Tests.Reparacoes;

/// <summary>
/// Sprint 551 (Doc 80 pilar assinaturas / Doc 93 #5): assinatura do cliente (canvas → PNG)
/// na entrada/entrega. API tests de propósito (e não só unit) — apanham DI por registar
/// (lição [[feedback_di_bypass_integration_test]]): POST guarda e GET lista; recapturar
/// substitui (upsert); payload inválido → 422; reparação inexistente → 404.
/// </summary>
public class AssinaturasApiTests : IClassFixture<RepairDeskApiFactory>
{
    private readonly RepairDeskApiFactory _factory;
    public AssinaturasApiTests(RepairDeskApiFactory factory) => _factory = factory;

    // PNG 1×1 válido (magic bytes + IHDR mínimo) — suficiente para passar a validação.
    private const string TinyPngBase64 =
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==";
    private static string PngDataUrl => $"data:image/png;base64,{TinyPngBase64}";

    private sealed record SaveReq(string Tipo, string DataUrl);

    [Fact]
    public async Task Save_Entrada_AparesceNaLista_ERecapturaSubstitui()
    {
        var client = await NewAuthedClientAsync();
        var rep = await CreateReparacaoAsync(client);

        var resp = await client.PostAsJsonAsync($"/api/reparacoes/{rep.Id}/assinaturas", new SaveReq("entrada", PngDataUrl));
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = (await resp.Content.ReadFromJsonAsync<AssinaturaDto>())!;
        dto.Tipo.Should().Be("entrada");

        var list = await client.GetFromJsonAsync<List<AssinaturaDto>>($"/api/reparacoes/{rep.Id}/assinaturas");
        list!.Should().ContainSingle(a => a.Tipo == "entrada");

        // Recaptura: substitui (não duplica) e avança o timestamp.
        var resp2 = await client.PostAsJsonAsync($"/api/reparacoes/{rep.Id}/assinaturas", new SaveReq("entrada", PngDataUrl));
        resp2.StatusCode.Should().Be(HttpStatusCode.OK);
        var list2 = await client.GetFromJsonAsync<List<AssinaturaDto>>($"/api/reparacoes/{rep.Id}/assinaturas");
        list2!.Count(a => a.Tipo == "entrada").Should().Be(1);
        list2.Single(a => a.Tipo == "entrada").AssinadaEm.Should().BeOnOrAfter(dto.AssinadaEm);
    }

    [Fact]
    public async Task Save_EntradaEEntrega_Coexistem()
    {
        var client = await NewAuthedClientAsync();
        var rep = await CreateReparacaoAsync(client);

        (await client.PostAsJsonAsync($"/api/reparacoes/{rep.Id}/assinaturas", new SaveReq("entrada", PngDataUrl))).EnsureSuccessStatusCode();
        (await client.PostAsJsonAsync($"/api/reparacoes/{rep.Id}/assinaturas", new SaveReq("entrega", PngDataUrl))).EnsureSuccessStatusCode();

        var list = await client.GetFromJsonAsync<List<AssinaturaDto>>($"/api/reparacoes/{rep.Id}/assinaturas");
        list!.Select(a => a.Tipo).Should().BeEquivalentTo(new[] { "entrada", "entrega" });
    }

    [Fact]
    public async Task Save_PayloadInvalido_422()
    {
        var client = await NewAuthedClientAsync();
        var rep = await CreateReparacaoAsync(client);

        // Tipo desconhecido.
        var badTipo = await client.PostAsJsonAsync($"/api/reparacoes/{rep.Id}/assinaturas", new SaveReq("levantamento", PngDataUrl));
        badTipo.StatusCode.Should().Be((HttpStatusCode)422);

        // Data-URL que não é PNG.
        var badUrl = await client.PostAsJsonAsync($"/api/reparacoes/{rep.Id}/assinaturas", new SaveReq("entrada", "data:image/jpeg;base64,AAAA"));
        badUrl.StatusCode.Should().Be((HttpStatusCode)422);

        // Base64 com conteúdo que não tem magic bytes PNG.
        var fakePng = $"data:image/png;base64,{Convert.ToBase64String(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 })}";
        var badMagic = await client.PostAsJsonAsync($"/api/reparacoes/{rep.Id}/assinaturas", new SaveReq("entrada", fakePng));
        badMagic.StatusCode.Should().Be((HttpStatusCode)422);
    }

    [Fact]
    public async Task Save_ReparacaoInexistente_404()
    {
        var client = await NewAuthedClientAsync();
        var resp = await client.PostAsJsonAsync($"/api/reparacoes/{Guid.NewGuid()}/assinaturas", new SaveReq("entrada", PngDataUrl));
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task EntradaPdf_ComAssinatura_ContinuaAGerar()
    {
        // Smoke do caminho PDF + assinatura (renderer estampa a imagem em vez da linha vazia).
        var client = await NewAuthedClientAsync();
        var rep = await CreateReparacaoAsync(client);
        (await client.PostAsJsonAsync($"/api/reparacoes/{rep.Id}/assinaturas", new SaveReq("entrada", PngDataUrl))).EnsureSuccessStatusCode();

        var pdf = await client.GetAsync($"/api/reparacoes/{rep.Id}/entrada.pdf");
        pdf.StatusCode.Should().Be(HttpStatusCode.OK);
        var bytes = await pdf.Content.ReadAsByteArrayAsync();
        System.Text.Encoding.ASCII.GetString(bytes, 0, 4).Should().Be("%PDF");
    }

    // ============ helpers ============

    private async Task<HttpClient> NewAuthedClientAsync()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true, AllowAutoRedirect = false });
        var login = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(RepairDeskApiFactory.AdminEmail, RepairDeskApiFactory.AdminPassword));
        login.EnsureSuccessStatusCode();
        var auth = (await login.Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }

    private static async Task<ReparacaoDto> CreateReparacaoAsync(HttpClient client)
    {
        var cli = await client.PostAsJsonAsync("/api/clientes",
            new CreateClienteRequest("Cliente Assin " + Guid.NewGuid().ToString("N")[..6], "912555111", null, null, null));
        cli.EnsureSuccessStatusCode();
        var cliente = (await cli.Content.ReadFromJsonAsync<ClienteDto>())!;

        var resp = await client.PostAsJsonAsync("/api/reparacoes",
            new CreateReparacaoRequest(cliente.Id, "iPhone 13", "Ecrã partido", null, 7000, null));
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<ReparacaoDto>())!;
    }
}
