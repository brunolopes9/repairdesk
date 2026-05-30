using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using RepairDesk.API.Infrastructure;
using RepairDesk.Services.Clientes;
using RepairDesk.Tests.Auth;

namespace RepairDesk.Tests.Dashboard;

/// <summary>
/// Sprint 468 — testes para endpoint S467 (Devices com garantia fabricante a expirar).
/// Cobre: janela de filtro (só inclui Devices que expiram dentro de N dias E ainda
/// não expiraram), filter !Arquivado, tenant isolation.
/// </summary>
public class DevicesGarantiaAExpirarTests : IClassFixture<RepairDeskApiFactory>
{
    private readonly RepairDeskApiFactory _factory;
    public DevicesGarantiaAExpirarTests(RepairDeskApiFactory factory) => _factory = factory;

    private sealed record CreateDeviceReq(
        Guid ClienteId, string Tipo, string? Marca = null, string? Modelo = null,
        string? Apelido = null, string? Imei = null, string? Serial = null, string? Cor = null,
        DateOnly? DataAquisicao = null, DateOnly? GarantiaFabricanteUntil = null, string? Notas = null);

    private sealed record DeviceDto(Guid Id);

    private sealed record DeviceGarantiaItem(
        Guid DeviceId, Guid ClienteId, string ClienteNome,
        string Tipo, string? Marca, string? Modelo, string? Apelido,
        string? Imei, DateOnly GarantiaFabricanteUntil);

    private sealed record DevicesGarantiaAExpirarResponse(
        IReadOnlyList<DeviceGarantiaItem> Items, int TotalCount, int DiasJanela);

    [Fact]
    public async Task DentroJanela_Aparece_ForaJanela_NaoAparece()
    {
        var client = await NewAuthedClientAsync();
        var cliente = await CreateClienteAsync(client);

        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
        var dentro = await CreateDevice(client, cliente.Id, "iPhone 12 dentro", hoje.AddDays(15));
        var fora = await CreateDevice(client, cliente.Id, "iPhone 13 fora", hoje.AddDays(60));
        var passado = await CreateDevice(client, cliente.Id, "iPhone 11 passou", hoje.AddDays(-5));
        var semGarantia = await CreateDevice(client, cliente.Id, "iPhone 14 sem", null);

        var resp = await client.GetFromJsonAsync<DevicesGarantiaAExpirarResponse>("/api/dashboard/devices-garantia-a-expirar?days=30");
        resp!.Items.Should().Contain(i => i.DeviceId == dentro);
        resp.Items.Should().NotContain(i => i.DeviceId == fora, "fora da janela (60d > 30d)");
        resp.Items.Should().NotContain(i => i.DeviceId == passado, "garantia já expirou");
        resp.Items.Should().NotContain(i => i.DeviceId == semGarantia, "sem garantia fabricante registada");
    }

    [Fact]
    public async Task Arquivado_NaoAparece()
    {
        var client = await NewAuthedClientAsync();
        var cliente = await CreateClienteAsync(client);
        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
        var deviceId = await CreateDevice(client, cliente.Id, "Tablet arquivado", hoje.AddDays(10));

        // Arquiva.
        var device = await client.GetFromJsonAsync<DeviceFull>($"/api/devices/{deviceId}");
        var put = await client.PutAsJsonAsync($"/api/devices/{deviceId}", new
        {
            tipo = device!.tipo,
            marca = device.marca,
            modelo = device.modelo,
            apelido = device.apelido,
            imei = device.imei,
            serial = device.serial,
            cor = device.cor,
            dataAquisicao = device.dataAquisicao,
            garantiaFabricanteUntil = device.garantiaFabricanteUntil,
            notas = device.notas,
            arquivado = true,
        });
        put.EnsureSuccessStatusCode();

        var resp = await client.GetFromJsonAsync<DevicesGarantiaAExpirarResponse>("/api/dashboard/devices-garantia-a-expirar?days=30");
        resp!.Items.Should().NotContain(i => i.DeviceId == deviceId, "Devices arquivados não aparecem no cross-sell");
    }

    [Fact]
    public async Task TenantIsolation_DeviceTenantA_NaoApareceEmB()
    {
        var adminA = await NewAuthedClientAsync(RepairDeskApiFactory.AdminEmail);
        var adminB = await NewAuthedClientAsync(RepairDeskApiFactory.SecondAdminEmail);

        var clienteA = await CreateClienteAsync(adminA);
        var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
        var marker = Guid.NewGuid().ToString("N")[..6];
        var deviceA = await CreateDevice(adminA, clienteA.Id, $"isolation-{marker}", hoje.AddDays(20));

        var listB = await adminB.GetFromJsonAsync<DevicesGarantiaAExpirarResponse>("/api/dashboard/devices-garantia-a-expirar");
        listB!.Items.Should().NotContain(i => i.DeviceId == deviceA, "Devices de tenant A não vazam para tenant B");
    }

    private sealed record DeviceFull(
        Guid id, string tipo, string? marca, string? modelo, string? apelido,
        string? imei, string? serial, string? cor, DateOnly? dataAquisicao,
        DateOnly? garantiaFabricanteUntil, string? notas, bool arquivado);

    private static async Task<Guid> CreateDevice(HttpClient client, Guid clienteId, string apelido, DateOnly? garantia)
    {
        var resp = await client.PostAsJsonAsync("/api/devices",
            new CreateDeviceReq(clienteId, "Telemóvel", Apelido: apelido, GarantiaFabricanteUntil: garantia));
        resp.EnsureSuccessStatusCode();
        var created = (await resp.Content.ReadFromJsonAsync<DeviceDto>())!;
        return created.Id;
    }

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
            new CreateClienteRequest("Cliente DGAE " + Guid.NewGuid().ToString("N")[..6], "919000999", null, null, null));
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<ClienteDto>())!;
    }
}
