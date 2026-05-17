using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using RepairDesk.API.Infrastructure;
using RepairDesk.Services.Clientes;
using RepairDesk.Tests.Auth;

namespace RepairDesk.Tests.Clientes;

public class ClientesApiTests : IClassFixture<RepairDeskApiFactory>
{
    private readonly RepairDeskApiFactory _factory;

    public ClientesApiTests(RepairDeskApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Create_ThenList_ReturnsTheNewCliente()
    {
        var client = await NewAuthedClient(RepairDeskApiFactory.AdminEmail);

        var create = await client.PostAsJsonAsync("/api/clientes",
            new CreateClienteRequest("Bruno Lopes", "+351 912 345 678", "bruno@example.com", "503000000", "VIP"));
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var dto = await create.Content.ReadFromJsonAsync<ClienteDto>();
        dto.Should().NotBeNull();
        dto!.Telefone.Should().Be("+351912345678");
        dto.Nif.Should().Be("503000000");

        var list = await client.GetFromJsonAsync<PagedResult<ClienteDto>>("/api/clientes?q=Bruno");
        list!.Total.Should().BeGreaterThanOrEqualTo(1);
        list.Items.Should().Contain(c => c.Id == dto.Id);
    }

    [Fact]
    public async Task Create_WithInvalidPhone_Returns422()
    {
        var client = await NewAuthedClient(RepairDeskApiFactory.AdminEmail);

        var resp = await client.PostAsJsonAsync("/api/clientes",
            new CreateClienteRequest("X", "abc", null, null, null));

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Create_WithDuplicateNif_Returns409()
    {
        var client = await NewAuthedClient(RepairDeskApiFactory.AdminEmail);
        var nif = "510000000";

        var first = await client.PostAsJsonAsync("/api/clientes",
            new CreateClienteRequest("João", "912000000", null, nif, null));
        first.EnsureSuccessStatusCode();

        var second = await client.PostAsJsonAsync("/api/clientes",
            new CreateClienteRequest("Outro", "913000000", null, nif, null));
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Update_ChangesFields()
    {
        var client = await NewAuthedClient(RepairDeskApiFactory.AdminEmail);
        var created = await CreateAsync(client, new CreateClienteRequest("Maria", "914000000", null, null, null));

        var update = await client.PutAsJsonAsync($"/api/clientes/{created.Id}",
            new UpdateClienteRequest("Maria Silva", "914999999", "maria@example.com", null, "Notas"));

        update.StatusCode.Should().Be(HttpStatusCode.OK);
        var dto = await update.Content.ReadFromJsonAsync<ClienteDto>();
        dto!.Nome.Should().Be("Maria Silva");
        dto.Telefone.Should().Be("914999999");
        dto.Email.Should().Be("maria@example.com");
    }

    [Fact]
    public async Task Delete_HidesFromList()
    {
        var client = await NewAuthedClient(RepairDeskApiFactory.AdminEmail);
        var created = await CreateAsync(client, new CreateClienteRequest("ToDelete", "915000000", null, null, null));

        var del = await client.DeleteAsync($"/api/clientes/{created.Id}");
        del.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var list = await client.GetFromJsonAsync<PagedResult<ClienteDto>>("/api/clientes?q=ToDelete");
        list!.Items.Should().NotContain(c => c.Id == created.Id);

        var get = await client.GetAsync($"/api/clientes/{created.Id}");
        get.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task TenantIsolation_TenantA_DoesNotSeeTenantB_Cliente()
    {
        var clientA = await NewAuthedClient(RepairDeskApiFactory.AdminEmail);
        var clientB = await NewAuthedClient(RepairDeskApiFactory.SecondAdminEmail);

        var marker = "Iso-" + Guid.NewGuid().ToString("N")[..8];
        var inA = await CreateAsync(clientA, new CreateClienteRequest(marker + "-A", "916000000", null, null, null));
        var inB = await CreateAsync(clientB, new CreateClienteRequest(marker + "-B", "917000000", null, null, null));

        var listA = await clientA.GetFromJsonAsync<PagedResult<ClienteDto>>($"/api/clientes?q={marker}");
        listA!.Items.Select(c => c.Id).Should().Contain(inA.Id).And.NotContain(inB.Id);

        var crossGet = await clientA.GetAsync($"/api/clientes/{inB.Id}");
        crossGet.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Anonymous_CannotAccessClientes()
    {
        var client = _factory.CreateClient();
        var resp = await client.GetAsync("/api/clientes");
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
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

    private static async Task<ClienteDto> CreateAsync(HttpClient client, CreateClienteRequest req)
    {
        var resp = await client.PostAsJsonAsync("/api/clientes", req);
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<ClienteDto>())!;
    }
}
