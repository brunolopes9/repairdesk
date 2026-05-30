using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using RepairDesk.API.Infrastructure;
using RepairDesk.Services.Clientes;
using RepairDesk.Tests.Auth;

namespace RepairDesk.Tests.Devices;

/// <summary>
/// Sprint 463 — testes para S461/S462 (Device entity asset registry).
/// Cobre: happy path CRUD, validação Tipo, IMEI duplicado, cliente inexistente,
/// tenant isolation, list por cliente com/sem arquivados.
/// </summary>
public class DevicesApiTests : IClassFixture<RepairDeskApiFactory>
{
    private readonly RepairDeskApiFactory _factory;
    public DevicesApiTests(RepairDeskApiFactory factory) => _factory = factory;

    private sealed record DeviceDto(
        Guid Id, Guid ClienteId, string Tipo, string? Marca, string? Modelo, string? Apelido,
        string? Imei, string? Serial, string? Cor, DateOnly? DataAquisicao,
        DateOnly? GarantiaFabricanteUntil, string? Notas, bool Arquivado, DateTime CreatedAt);

    private sealed record CreateDeviceReq(
        Guid ClienteId, string Tipo, string? Marca = null, string? Modelo = null,
        string? Apelido = null, string? Imei = null, string? Serial = null, string? Cor = null,
        DateOnly? DataAquisicao = null, DateOnly? GarantiaFabricanteUntil = null, string? Notas = null);

    private sealed record UpdateDeviceReq(
        string Tipo, string? Marca, string? Modelo, string? Apelido, string? Imei,
        string? Serial, string? Cor, DateOnly? DataAquisicao,
        DateOnly? GarantiaFabricanteUntil, string? Notas, bool Arquivado);

    [Fact]
    public async Task Create_Valido_DevolveDtoEListaCobre()
    {
        var client = await NewAuthedClientAsync();
        var cliente = await CreateClienteAsync(client);

        var resp = await client.PostAsJsonAsync("/api/devices",
            new CreateDeviceReq(
                ClienteId: cliente.Id,
                Tipo: "Telemóvel",
                Marca: "Apple",
                Modelo: "iPhone 13",
                Apelido: "iPhone do João",
                Imei: "356789012345678",
                Cor: "Azul"));
        resp.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = (await resp.Content.ReadFromJsonAsync<DeviceDto>())!;
        created.Tipo.Should().Be("Telemóvel");
        created.Apelido.Should().Be("iPhone do João");
        created.Imei.Should().Be("356789012345678");
        created.Arquivado.Should().BeFalse();

        var list = await client.GetFromJsonAsync<List<DeviceDto>>($"/api/devices?clienteId={cliente.Id}");
        list!.Should().ContainSingle(d => d.Id == created.Id);
    }

    [Fact]
    public async Task Create_TipoVazio_422()
    {
        var client = await NewAuthedClientAsync();
        var cliente = await CreateClienteAsync(client);
        var resp = await client.PostAsJsonAsync("/api/devices",
            new CreateDeviceReq(cliente.Id, Tipo: "  "));
        resp.StatusCode.Should().Be((HttpStatusCode)422);
    }

    [Fact]
    public async Task Create_ImeiDuplicado_422()
    {
        var client = await NewAuthedClientAsync();
        var cliente = await CreateClienteAsync(client);
        var imei = "359" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpperInvariant().Replace('A', '1').Replace('B', '2').Replace('C', '3').Replace('D', '4').Replace('E', '5').Replace('F', '6');

        var first = await client.PostAsJsonAsync("/api/devices",
            new CreateDeviceReq(cliente.Id, "Telemóvel", Imei: imei));
        first.EnsureSuccessStatusCode();

        var dup = await client.PostAsJsonAsync("/api/devices",
            new CreateDeviceReq(cliente.Id, "Telemóvel", Imei: imei));
        dup.StatusCode.Should().Be((HttpStatusCode)422);
    }

    [Fact]
    public async Task Create_ClienteInexistente_404()
    {
        var client = await NewAuthedClientAsync();
        var resp = await client.PostAsJsonAsync("/api/devices",
            new CreateDeviceReq(Guid.NewGuid(), "Telemóvel"));
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Update_Arquivado_DesapareceDaListaPorDefault()
    {
        var client = await NewAuthedClientAsync();
        var cliente = await CreateClienteAsync(client);

        var post = await client.PostAsJsonAsync("/api/devices",
            new CreateDeviceReq(cliente.Id, "Tablet", Marca: "Samsung", Modelo: "Tab S8"));
        var d = (await post.Content.ReadFromJsonAsync<DeviceDto>())!;

        var put = await client.PutAsJsonAsync($"/api/devices/{d.Id}",
            new UpdateDeviceReq(d.Tipo, d.Marca, d.Modelo, d.Apelido, d.Imei, d.Serial, d.Cor, d.DataAquisicao, d.GarantiaFabricanteUntil, d.Notas, Arquivado: true));
        put.EnsureSuccessStatusCode();

        var ativos = await client.GetFromJsonAsync<List<DeviceDto>>($"/api/devices?clienteId={cliente.Id}");
        ativos!.Should().NotContain(x => x.Id == d.Id);

        var todos = await client.GetFromJsonAsync<List<DeviceDto>>($"/api/devices?clienteId={cliente.Id}&incluirArquivados=true");
        todos!.Should().Contain(x => x.Id == d.Id && x.Arquivado);
    }

    [Fact]
    public async Task Delete_RemoveDaLista()
    {
        var client = await NewAuthedClientAsync();
        var cliente = await CreateClienteAsync(client);

        var post = await client.PostAsJsonAsync("/api/devices",
            new CreateDeviceReq(cliente.Id, "Smartwatch"));
        var d = (await post.Content.ReadFromJsonAsync<DeviceDto>())!;

        var del = await client.DeleteAsync($"/api/devices/{d.Id}");
        del.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var get = await client.GetAsync($"/api/devices/{d.Id}");
        get.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task TenantIsolation_DeviceTenantA_NaoApareceEmB()
    {
        var adminA = await NewAuthedClientAsync(RepairDeskApiFactory.AdminEmail);
        var adminB = await NewAuthedClientAsync(RepairDeskApiFactory.SecondAdminEmail);

        var clienteA = await CreateClienteAsync(adminA);
        var marker = Guid.NewGuid().ToString("N")[..6];
        var post = await adminA.PostAsJsonAsync("/api/devices",
            new CreateDeviceReq(clienteA.Id, "Telemóvel", Apelido: $"iso-{marker}"));
        post.EnsureSuccessStatusCode();

        // Tenant B não vê o cliente de A → lista por clienteId de A devolve vazia (filter global).
        var listB = await adminB.GetFromJsonAsync<List<DeviceDto>>($"/api/devices?clienteId={clienteA.Id}");
        listB!.Should().NotContain(x => x.Apelido != null && x.Apelido.Contains(marker), "devices de tenant A não podem vazar para tenant B");
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
            new CreateClienteRequest("Cliente Device " + Guid.NewGuid().ToString("N")[..6], "919000000", null, null, null));
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<ClienteDto>())!;
    }
}
