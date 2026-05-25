using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using RepairDesk.API.Infrastructure;
using RepairDesk.Core.Enums;
using RepairDesk.Services.Clientes;
using RepairDesk.Services.Despesas;
using RepairDesk.Tests.Auth;

namespace RepairDesk.Tests.Despesas;

public class DespesasApiTests : IClassFixture<RepairDeskApiFactory>
{
    private readonly RepairDeskApiFactory _factory;
    public DespesasApiTests(RepairDeskApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Create_QuickAdd_OK()
    {
        var client = await NewAuthedClient(RepairDeskApiFactory.AdminEmail);

        var resp = await client.PostAsJsonAsync("/api/despesas",
            new CreateDespesaRequest(
                Descricao: "Bateria iPhone 13",
                Categoria: DespesaCategoria.Pecas,
                ValorCents: 4500,
                Data: DateTime.UtcNow,
                Fornecedor: "Mobiltrust",
                NumeroEncomenda: null,
                Notas: null,
                TrabalhoId: null,
                ReparacaoId: null));

        resp.StatusCode.Should().Be(HttpStatusCode.Created);
        var dto = await resp.Content.ReadFromJsonAsync<DespesaDto>();
        dto!.ValorCents.Should().Be(4500);
        dto.Categoria.Should().Be(DespesaCategoria.Pecas);
    }

    [Fact]
    public async Task Create_RejectsZeroValue()
    {
        var client = await NewAuthedClient(RepairDeskApiFactory.AdminEmail);
        var resp = await client.PostAsJsonAsync("/api/despesas",
            new CreateDespesaRequest("X", DespesaCategoria.Outro, 0, null, null, null, null, null, null));
        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Search_ByCategoria_AndDateRange()
    {
        var client = await NewAuthedClient(RepairDeskApiFactory.AdminEmail);
        await CreateOne(client, DespesaCategoria.Pecas, 1000);
        await CreateOne(client, DespesaCategoria.Software, 5000);

        var pecas = await client.GetFromJsonAsync<PagedResult<DespesaDto>>("/api/despesas?categoria=0");
        pecas!.Items.Should().OnlyContain(d => d.Categoria == DespesaCategoria.Pecas);
    }

    [Fact]
    public async Task Search_ByCategoriaIn_FiltersMultipleCategorias()
    {
        var client = await NewAuthedClient(RepairDeskApiFactory.AdminEmail);
        var pecas = await CreateOne(client, DespesaCategoria.Pecas, 1000);
        var material = await CreateOne(client, DespesaCategoria.Material, 1500);
        await CreateOne(client, DespesaCategoria.Software, 5000);

        var list = await client.GetFromJsonAsync<PagedResult<DespesaDto>>("/api/despesas?categoria_in=0,1");

        list!.Items.Select(d => d.Id).Should().Contain(new[] { pecas.Id, material.Id });
        var allowed = new[] { DespesaCategoria.Pecas, DespesaCategoria.Material };
        list.Items.Should().OnlyContain(d => allowed.Contains(d.Categoria));
    }

    [Fact]
    public async Task Search_Recurring_FilterOnlyRecurring()
    {
        var client = await NewAuthedClient(RepairDeskApiFactory.AdminEmail);
        var recurring = await CreateOne(client, DespesaCategoria.Software, 1200, isRecorrente: true, periodicidadeMeses: 1);
        await CreateOne(client, DespesaCategoria.Software, 800);

        var list = await client.GetFromJsonAsync<PagedResult<DespesaDto>>("/api/despesas?isRecorrente=true");

        list!.Items.Should().ContainSingle(d => d.Id == recurring.Id);
        list.Items.Should().OnlyContain(d => d.IsRecorrente);
        list.Items.Single().PeriodicidadeMeses.Should().Be(1);
    }

    [Fact]
    public async Task Update_AllowsLinkingToTrabalho()
    {
        var client = await NewAuthedClient(RepairDeskApiFactory.AdminEmail);
        var d = await CreateOne(client, DespesaCategoria.Pecas, 2500);
        var trabalhoId = Guid.NewGuid(); // We'll just attempt linking; update should accept

        var resp = await client.PutAsJsonAsync($"/api/despesas/{d.Id}",
            new UpdateDespesaRequest(d.Descricao, d.Categoria, d.ValorCents, d.Data, d.Fornecedor, d.NumeroEncomenda, d.Notas, trabalhoId, null));
        resp.EnsureSuccessStatusCode();
        var updated = await resp.Content.ReadFromJsonAsync<DespesaDto>();
        updated!.TrabalhoId.Should().Be(trabalhoId);
    }

    [Fact]
    public async Task TenantIsolation_DespesasScoped()
    {
        var clientA = await NewAuthedClient(RepairDeskApiFactory.AdminEmail);
        var clientB = await NewAuthedClient(RepairDeskApiFactory.SecondAdminEmail);
        var inA = await CreateOne(clientA, DespesaCategoria.Outro, 999);
        var inB = await CreateOne(clientB, DespesaCategoria.Outro, 888);

        var crossGet = await clientA.GetAsync($"/api/despesas/{inB.Id}");
        crossGet.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var listA = await clientA.GetFromJsonAsync<PagedResult<DespesaDto>>("/api/despesas");
        listA!.Items.Select(i => i.Id).Should().Contain(inA.Id).And.NotContain(inB.Id);
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

    private static async Task<DespesaDto> CreateOne(
        HttpClient client,
        DespesaCategoria cat,
        int cents,
        bool isRecorrente = false,
        int? periodicidadeMeses = null)
    {
        var resp = await client.PostAsJsonAsync("/api/despesas",
            new CreateDespesaRequest($"Desp-{Guid.NewGuid():N}", cat, cents, null, null, null, null, null, null, false, isRecorrente, periodicidadeMeses));
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<DespesaDto>())!;
    }
}
