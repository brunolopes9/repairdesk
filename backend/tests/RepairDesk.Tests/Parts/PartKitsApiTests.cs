using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using RepairDesk.API.Infrastructure;
using RepairDesk.Core.Enums;
using RepairDesk.Services.Clientes;
using RepairDesk.Services.Parts;
using RepairDesk.Services.Reparacoes;
using RepairDesk.Tests.Auth;

namespace RepairDesk.Tests.Parts;

/// <summary>
/// Sprint 353: kits de peças. Cobre criação e o apply numa reparação
/// (cada item vira PartMovimento de saída, decrementa stock).
/// </summary>
public class PartKitsApiTests : IClassFixture<RepairDeskApiFactory>
{
    private readonly RepairDeskApiFactory _factory;
    public PartKitsApiTests(RepairDeskApiFactory factory) => _factory = factory;

    private sealed record KitItemInput(Guid PartId, int Quantidade);
    private sealed record CreateKitReq(string Nome, string? Descricao, List<KitItemInput> Items);
    private sealed record KitItemDto(Guid PartId, string PartNome, string? PartSku, int Quantidade, int CustoUnitarioCents);
    private sealed record KitDto(Guid Id, string Nome, string? Descricao, List<KitItemDto> Items, int CustoTotalCents);
    private sealed record ApplyKitReq(Guid ReparacaoId);
    private sealed record AppliedItemDto(Guid PartId, string PartNome, int Quantidade);
    private sealed record ApplyKitResult(List<AppliedItemDto> Applied, string? FailedAt);

    [Fact]
    public async Task Create_ComItens_CalculaCustoTotal()
    {
        var client = await NewAuthedClient();
        var p1 = await CreatePartAsync(client, qtd: 10, custo: 4200);
        var p2 = await CreatePartAsync(client, qtd: 10, custo: 150);

        var resp = await client.PostAsJsonAsync("/api/part-kits", new CreateKitReq(
            "Kit ecrã " + Guid.NewGuid().ToString("N")[..6], "ecrã + adesivo",
            new List<KitItemInput> { new(p1.Id, 1), new(p2.Id, 2) }));
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var kit = (await resp.Content.ReadFromJsonAsync<KitDto>())!;

        kit.Items.Should().HaveCount(2);
        kit.CustoTotalCents.Should().Be(4200 * 1 + 150 * 2);
    }

    [Fact]
    public async Task Apply_CriaMovimentos_DecrementaStock()
    {
        var client = await NewAuthedClient();
        var part = await CreatePartAsync(client, qtd: 5, custo: 4200);
        var kitResp = await client.PostAsJsonAsync("/api/part-kits", new CreateKitReq(
            "Kit apply " + Guid.NewGuid().ToString("N")[..6], null,
            new List<KitItemInput> { new(part.Id, 2) }));
        var kit = (await kitResp.Content.ReadFromJsonAsync<KitDto>())!;

        var cliente = await CreateClienteAsync(client);
        var rep = await CreateReparacaoAsync(client, cliente.Id);

        var apply = await client.PostAsJsonAsync($"/api/part-kits/{kit.Id}/apply", new ApplyKitReq(rep.Id));
        apply.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = (await apply.Content.ReadFromJsonAsync<ApplyKitResult>())!;
        result.FailedAt.Should().BeNull();
        result.Applied.Should().ContainSingle(a => a.PartId == part.Id && a.Quantidade == 2);

        // Stock decrementado de 5 → 3.
        var updated = await client.GetFromJsonAsync<PartDto>($"/api/parts/{part.Id}");
        updated!.QtdStock.Should().Be(3);

        // Movimento ligado à reparação.
        var movimentos = await client.GetFromJsonAsync<IReadOnlyList<PartMovimentoDto>>($"/api/parts/movimentos?reparacaoId={rep.Id}");
        movimentos!.Should().Contain(m => m.PartId == part.Id);
    }

    [Fact]
    public async Task Apply_StockInsuficiente_AbortaComConflito()
    {
        var client = await NewAuthedClient();
        var part = await CreatePartAsync(client, qtd: 1, custo: 1000);
        var kitResp = await client.PostAsJsonAsync("/api/part-kits", new CreateKitReq(
            "Kit overflow " + Guid.NewGuid().ToString("N")[..6], null,
            new List<KitItemInput> { new(part.Id, 5) })); // pede 5, só há 1
        var kit = (await kitResp.Content.ReadFromJsonAsync<KitDto>())!;

        var cliente = await CreateClienteAsync(client);
        var rep = await CreateReparacaoAsync(client, cliente.Id);

        var apply = await client.PostAsJsonAsync($"/api/part-kits/{kit.Id}/apply", new ApplyKitReq(rep.Id));
        apply.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    private async Task<HttpClient> NewAuthedClient()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true, AllowAutoRedirect = false });
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(RepairDeskApiFactory.AdminEmail, RepairDeskApiFactory.AdminPassword));
        login.EnsureSuccessStatusCode();
        var auth = (await login.Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }

    private static async Task<PartDto> CreatePartAsync(HttpClient client, int qtd, int custo)
    {
        var resp = await client.PostAsJsonAsync("/api/parts", new CreatePartRequest(
            "KIT-" + Guid.NewGuid().ToString("N")[..8], "Peça kit", PartCategoria.Outro,
            null, null, null, QtdStock: qtd, QtdMinima: 0, CustoUnitarioCents: custo,
            Fornecedor: null, LocalArmazenamento: null, Notas: null));
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<PartDto>())!;
    }

    private static async Task<ClienteDto> CreateClienteAsync(HttpClient client)
    {
        var resp = await client.PostAsJsonAsync("/api/clientes",
            new CreateClienteRequest("Cliente Kit " + Guid.NewGuid().ToString("N")[..6], "912000333", null, null, null));
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<ClienteDto>())!;
    }

    private static async Task<ReparacaoDto> CreateReparacaoAsync(HttpClient client, Guid clienteId)
    {
        var resp = await client.PostAsJsonAsync("/api/reparacoes",
            new CreateReparacaoRequest(clienteId, "iPhone 13", "Ecrã", null, 15000, null));
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<ReparacaoDto>())!;
    }
}
