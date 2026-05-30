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
/// Sprint 455 — testes para S452 (comunicações por reparação) + S453 (vista cliente).
/// Cobre: happy path, validação texto vazio (422), tenant isolation, 404 em reparação
/// inexistente, delete, agregação por cliente entre múltiplas reparações.
/// </summary>
public class ReparacaoComunicacoesApiTests : IClassFixture<RepairDeskApiFactory>
{
    private readonly RepairDeskApiFactory _factory;
    public ReparacaoComunicacoesApiTests(RepairDeskApiFactory factory) => _factory = factory;

    private sealed record CommDto(Guid Id, Guid ReparacaoId, Guid ClienteId, int Tipo, int Direcao, string Texto, Guid CreatedByUserId, DateTime CreatedAt);
    private sealed record CreateCommReq(int Tipo, int Direcao, string Texto);

    [Fact]
    public async Task Create_Valido_AparesceNaListaDaReparacao()
    {
        var client = await NewAuthedClientAsync();
        var (_, rep) = await CreateClienteEReparacaoAsync(client);

        var resp = await client.PostAsJsonAsync(
            $"/api/reparacoes/{rep.Id}/comunicacoes",
            new CreateCommReq(Tipo: 1, Direcao: 0, Texto: "cliente ligou às 10h, perguntou estado")); // Telefone, Inbound
        resp.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = (await resp.Content.ReadFromJsonAsync<CommDto>())!;
        created.ReparacaoId.Should().Be(rep.Id);
        created.Tipo.Should().Be(1);
        created.Direcao.Should().Be(0);

        var list = await client.GetFromJsonAsync<List<CommDto>>($"/api/reparacoes/{rep.Id}/comunicacoes");
        list!.Should().ContainSingle(c => c.Id == created.Id && c.Texto.Contains("perguntou estado"));
    }

    [Fact]
    public async Task Create_TextoVazio_422()
    {
        var client = await NewAuthedClientAsync();
        var (_, rep) = await CreateClienteEReparacaoAsync(client);

        var resp = await client.PostAsJsonAsync(
            $"/api/reparacoes/{rep.Id}/comunicacoes",
            new CreateCommReq(Tipo: 0, Direcao: 2, Texto: "   ")); // só whitespace
        resp.StatusCode.Should().Be((HttpStatusCode)422);
    }

    [Fact]
    public async Task Create_ReparacaoInexistente_404()
    {
        var client = await NewAuthedClientAsync();
        var resp = await client.PostAsJsonAsync(
            $"/api/reparacoes/{Guid.NewGuid()}/comunicacoes",
            new CreateCommReq(Tipo: 0, Direcao: 2, Texto: "nota qualquer"));
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_RemoveDaLista()
    {
        var client = await NewAuthedClientAsync();
        var (_, rep) = await CreateClienteEReparacaoAsync(client);

        var post = await client.PostAsJsonAsync(
            $"/api/reparacoes/{rep.Id}/comunicacoes",
            new CreateCommReq(Tipo: 2, Direcao: 1, Texto: "enviei WhatsApp com link de pagamento"));
        var created = (await post.Content.ReadFromJsonAsync<CommDto>())!;

        var del = await client.DeleteAsync($"/api/reparacoes/{rep.Id}/comunicacoes/{created.Id}");
        del.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var list = await client.GetFromJsonAsync<List<CommDto>>($"/api/reparacoes/{rep.Id}/comunicacoes");
        list!.Should().NotContain(c => c.Id == created.Id);
    }

    [Fact]
    public async Task ListByCliente_AgregaEntreVariasReparacoes()
    {
        // Sprint 453: vista cliente — soma comunicações de várias reparações do mesmo cliente.
        var client = await NewAuthedClientAsync();
        var cliente = await CreateClienteAsync(client);
        var rep1 = await CreateReparacaoAsync(client, cliente.Id, "iPhone 12");
        var rep2 = await CreateReparacaoAsync(client, cliente.Id, "iPad Pro");

        var marker1 = Guid.NewGuid().ToString("N")[..6];
        var marker2 = Guid.NewGuid().ToString("N")[..6];
        await client.PostAsJsonAsync($"/api/reparacoes/{rep1.Id}/comunicacoes",
            new CreateCommReq(Tipo: 1, Direcao: 0, Texto: $"chamada {marker1}"));
        await client.PostAsJsonAsync($"/api/reparacoes/{rep2.Id}/comunicacoes",
            new CreateCommReq(Tipo: 3, Direcao: 1, Texto: $"email {marker2}"));

        var byCliente = await client.GetFromJsonAsync<List<CommDto>>($"/api/clientes/{cliente.Id}/comunicacoes");
        byCliente!.Should().Contain(c => c.Texto.Contains(marker1));
        byCliente!.Should().Contain(c => c.Texto.Contains(marker2));
    }

    [Fact]
    public async Task TenantIsolation_ComunicacaoTenantA_NaoApareceEmTenantB()
    {
        var adminA = await NewAuthedClientAsync(RepairDeskApiFactory.AdminEmail);
        var adminB = await NewAuthedClientAsync(RepairDeskApiFactory.SecondAdminEmail);

        var (clienteA, repA) = await CreateClienteEReparacaoAsync(adminA);
        var marker = Guid.NewGuid().ToString("N")[..8];
        await adminA.PostAsJsonAsync($"/api/reparacoes/{repA.Id}/comunicacoes",
            new CreateCommReq(Tipo: 0, Direcao: 2, Texto: $"iso {marker}"));

        // Tenant B não consegue ler a reparação de A → 404.
        var resp = await adminB.GetAsync($"/api/reparacoes/{repA.Id}/comunicacoes");
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Tenant B também não consegue ver as comunicações do cliente A.
        var byCli = await adminB.GetAsync($"/api/clientes/{clienteA.Id}/comunicacoes");
        // Vista por cliente: o repo filtra por TenantId; mesmo que o Id do cliente A seja conhecido,
        // o resultado tem que estar vazio (não vazar).
        if (byCli.StatusCode == HttpStatusCode.OK)
        {
            var items = await byCli.Content.ReadFromJsonAsync<List<CommDto>>();
            items!.Should().NotContain(c => c.Texto.Contains(marker), "comunicações de tenant A não podem vazar para tenant B");
        }
        else
        {
            byCli.StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.OK);
        }
    }

    // ============ helpers ============

    private async Task<HttpClient> NewAuthedClientAsync(string? email = null)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true, AllowAutoRedirect = false });
        var login = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(email ?? RepairDeskApiFactory.AdminEmail, RepairDeskApiFactory.AdminPassword));
        login.EnsureSuccessStatusCode();
        var auth = (await login.Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }

    private static async Task<ClienteDto> CreateClienteAsync(HttpClient client)
    {
        var resp = await client.PostAsJsonAsync("/api/clientes",
            new CreateClienteRequest("Cliente Comm " + Guid.NewGuid().ToString("N")[..6], "912555000", null, null, null));
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<ClienteDto>())!;
    }

    private static async Task<ReparacaoDto> CreateReparacaoAsync(HttpClient client, Guid clienteId, string equipamento = "iPhone 13")
    {
        var resp = await client.PostAsJsonAsync("/api/reparacoes",
            new CreateReparacaoRequest(clienteId, equipamento, "Avaria genérica", null, 7000, null));
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<ReparacaoDto>())!;
    }

    private async Task<(ClienteDto Cliente, ReparacaoDto Reparacao)> CreateClienteEReparacaoAsync(HttpClient client)
    {
        var cliente = await CreateClienteAsync(client);
        var rep = await CreateReparacaoAsync(client, cliente.Id);
        return (cliente, rep);
    }
}
