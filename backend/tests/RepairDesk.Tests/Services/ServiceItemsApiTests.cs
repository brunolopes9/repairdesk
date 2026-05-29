using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using RepairDesk.API.Infrastructure;
using RepairDesk.Tests.Auth;

namespace RepairDesk.Tests.Services;

/// <summary>
/// Sprint 448: cobre o CRUD do catálogo de serviços (S435). Mutations só Admin,
/// GET aberto a Authenticated. Validações no input (nome 2-120, preço >=0,
/// garantia 0-3650 dias).
/// </summary>
public class ServiceItemsApiTests : IClassFixture<RepairDeskApiFactory>
{
    private readonly RepairDeskApiFactory _factory;
    public ServiceItemsApiTests(RepairDeskApiFactory factory) => _factory = factory;

    private sealed record ServiceItemDto(
        Guid Id,
        string Nome,
        string? Descricao,
        int PrecoCents,
        int GarantiaDiasCliente,
        bool Activo);

    private sealed record CreateOrUpdate(
        string Nome,
        string? Descricao,
        int PrecoCents,
        int GarantiaDiasCliente,
        bool Activo);

    [Fact]
    public async Task Create_Get_Update_Delete_Flow()
    {
        var client = await NewAuthedClient();
        var marker = Guid.NewGuid().ToString("N")[..8];

        // Create.
        var create = await client.PostAsJsonAsync("/api/services",
            new CreateOrUpdate($"Bateria iPhone {marker}", "Substituição com peça original", 4000, 365, true));
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var dto = (await create.Content.ReadFromJsonAsync<ServiceItemDto>())!;
        dto.Nome.Should().Be($"Bateria iPhone {marker}");
        dto.PrecoCents.Should().Be(4000);
        dto.GarantiaDiasCliente.Should().Be(365);
        dto.Activo.Should().BeTrue();

        // Get by id.
        var got = await client.GetFromJsonAsync<ServiceItemDto>($"/api/services/{dto.Id}");
        got!.Id.Should().Be(dto.Id);

        // Update — sobe preço e garantia.
        var update = await client.PutAsJsonAsync($"/api/services/{dto.Id}",
            new CreateOrUpdate($"Bateria iPhone {marker}", "Substituição com peça compatível", 3500, 180, true));
        update.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = (await update.Content.ReadFromJsonAsync<ServiceItemDto>())!;
        updated.PrecoCents.Should().Be(3500);
        updated.GarantiaDiasCliente.Should().Be(180);
        updated.Descricao.Should().Contain("compatível");

        // List default — apareces porque estás Activo.
        var list = await client.GetFromJsonAsync<List<ServiceItemDto>>("/api/services");
        list!.Should().Contain(s => s.Id == dto.Id);

        // Delete.
        var del = await client.DeleteAsync($"/api/services/{dto.Id}");
        del.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var after = await client.GetAsync($"/api/services/{dto.Id}");
        after.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task List_HideInactive_ByDefault()
    {
        var client = await NewAuthedClient();
        var marker = Guid.NewGuid().ToString("N")[..8];

        // Cria 1 activo + 1 inactivo.
        var ativo = await client.PostAsJsonAsync("/api/services",
            new CreateOrUpdate($"Activo {marker}", null, 1000, 90, true));
        var inactivo = await client.PostAsJsonAsync("/api/services",
            new CreateOrUpdate($"Inactivo {marker}", null, 1000, 90, false));
        ativo.EnsureSuccessStatusCode();
        inactivo.EnsureSuccessStatusCode();
        var ativoDto = (await ativo.Content.ReadFromJsonAsync<ServiceItemDto>())!;
        var inactivoDto = (await inactivo.Content.ReadFromJsonAsync<ServiceItemDto>())!;

        // GET default — só vê o activo.
        var defaultList = await client.GetFromJsonAsync<List<ServiceItemDto>>("/api/services");
        defaultList!.Should().Contain(s => s.Id == ativoDto.Id);
        defaultList.Should().NotContain(s => s.Id == inactivoDto.Id);

        // GET com includeInactive=true — vê ambos.
        var fullList = await client.GetFromJsonAsync<List<ServiceItemDto>>("/api/services?includeInactive=true");
        fullList!.Should().Contain(s => s.Id == ativoDto.Id);
        fullList.Should().Contain(s => s.Id == inactivoDto.Id);
    }

    [Fact]
    public async Task Create_NomeCurto_400()
    {
        var client = await NewAuthedClient();
        var resp = await client.PostAsJsonAsync("/api/services",
            new CreateOrUpdate("A", null, 100, 90, true));
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_PrecoNegativo_400()
    {
        var client = await NewAuthedClient();
        var resp = await client.PostAsJsonAsync("/api/services",
            new CreateOrUpdate("Serviço válido", null, -1, 90, true));
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_GarantiaForaDoLimite_400()
    {
        var client = await NewAuthedClient();
        // 3650 = OK (10 anos); 3651 = inválido.
        var resp = await client.PostAsJsonAsync("/api/services",
            new CreateOrUpdate("Serviço válido", null, 100, 3651, true));
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Get_TenantIsolation_PedidoDoOutroTenant_404()
    {
        var adminA = await NewAuthedClient(RepairDeskApiFactory.AdminEmail);
        var adminB = await NewAuthedClient(RepairDeskApiFactory.SecondAdminEmail);
        var marker = Guid.NewGuid().ToString("N")[..8];

        // A cria.
        var create = await adminA.PostAsJsonAsync("/api/services",
            new CreateOrUpdate($"Iso {marker}", null, 1000, 90, true));
        create.EnsureSuccessStatusCode();
        var dto = (await create.Content.ReadFromJsonAsync<ServiceItemDto>())!;

        // B tenta GET — 404 (não vaza para outro tenant).
        var leaked = await adminB.GetAsync($"/api/services/{dto.Id}");
        leaked.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // B não vê na lista.
        var listB = await adminB.GetFromJsonAsync<List<ServiceItemDto>>("/api/services");
        listB!.Should().NotContain(s => s.Id == dto.Id);
    }

    private async Task<HttpClient> NewAuthedClient(string email = RepairDeskApiFactory.AdminEmail)
    {
        var client = _factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(email, RepairDeskApiFactory.AdminPassword));
        login.EnsureSuccessStatusCode();
        var auth = (await login.Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }
}
