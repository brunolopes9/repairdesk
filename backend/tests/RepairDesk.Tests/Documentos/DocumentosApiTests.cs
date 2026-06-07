using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using RepairDesk.API.Infrastructure;
using RepairDesk.Services.Documentos;
using RepairDesk.Tests.Auth;

namespace RepairDesk.Tests.Documentos;

/// <summary>
/// Sprint 518: guarda de regressão para o separador "Vendas · Faturas".
/// O bug original: o <see cref="DocumentoService"/> injecta IMemoryCache mas o AddMemoryCache nunca
/// foi registado → a activação do controller falhava no DI → 500 em cada GET → a lista aparecia
/// sempre vazia. O teste unitário não apanhou porque construía o serviço à mão (bypassa o DI).
/// Estes testes batem no endpoint REAL através do contentor de DI verdadeiro — se faltar uma
/// dependência, falham com 500 em vez de deixarem passar para produção.
/// </summary>
public class DocumentosApiTests : IClassFixture<RepairDeskApiFactory>
{
    private readonly RepairDeskApiFactory _factory;
    public DocumentosApiTests(RepairDeskApiFactory factory) => _factory = factory;

    [Fact]
    public async Task ListVendas_ResolvesService_Returns200()
    {
        var client = await NewAuthedClient(RepairDeskApiFactory.AdminEmail);

        var resp = await client.GetAsync("/api/documentos/vendas?from=2026-01-01T00:00:00Z&to=2026-12-31T23:59:59Z");

        // O ponto deste teste: NÃO pode ser 500. Antes do fix do IMemoryCache rebentava aqui.
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await resp.Content.ReadFromJsonAsync<DocumentosListDto>();
        dto.Should().NotBeNull();
        dto!.Items.Should().NotBeNull();
    }

    [Fact]
    public async Task ListVendas_RequiresAuth()
    {
        var anon = _factory.CreateClient();
        var resp = await anon.GetAsync("/api/documentos/vendas");
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ExportCsv_ResolvesService_Returns200Csv()
    {
        var client = await NewAuthedClient(RepairDeskApiFactory.AdminEmail);

        var resp = await client.GetAsync("/api/documentos/vendas/export.csv");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        resp.Content.Headers.ContentType!.MediaType.Should().Be("text/csv");
    }

    private async Task<HttpClient> NewAuthedClient(string email)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            HandleCookies = true,
            AllowAutoRedirect = false
        });
        var login = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(email, RepairDeskApiFactory.AdminPassword));
        login.EnsureSuccessStatusCode();
        var auth = (await login.Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }
}
