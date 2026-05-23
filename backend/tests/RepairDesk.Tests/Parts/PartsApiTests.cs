using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using RepairDesk.API.Infrastructure;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Enums;
using RepairDesk.Services.Clientes;
using RepairDesk.Services.Parts;
using RepairDesk.Services.Reparacoes;
using RepairDesk.Tests.Auth;

namespace RepairDesk.Tests.Parts;

public class PartsApiTests : IClassFixture<RepairDeskApiFactory>
{
    private readonly RepairDeskApiFactory _factory;

    public PartsApiTests(RepairDeskApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Create_ThenAdjustStock_UpdatesPartAndLowStock()
    {
        var client = await NewAuthedClient(RepairDeskApiFactory.AdminEmail);
        var part = await CreatePartAsync(client, new CreatePartRequest(
            "BAT-IP12-" + Guid.NewGuid().ToString("N")[..6],
            "Bateria iPhone 12",
            PartCategoria.Bateria,
            "Apple",
            "iPhone 12",
            null,
            QtdStock: 5,
            QtdMinima: 2,
            CustoUnitarioCents: 1800,
            Fornecedor: "Tudo4Mobile",
            LocalArmazenamento: "A3",
            Notas: null));

        var movResp = await client.PostAsJsonAsync($"/api/parts/{part.Id}/movimento",
            new CreatePartMovimentoRequest(-3, PartMovimentoMotivo.Saida, null, "Uso em balcão"));
        movResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var movimento = await movResp.Content.ReadFromJsonAsync<PartMovimentoDto>();
        movimento!.StockAntes.Should().Be(5);
        movimento.StockDepois.Should().Be(2);

        var updated = await client.GetFromJsonAsync<PartDto>($"/api/parts/{part.Id}");
        updated!.QtdStock.Should().Be(2);
        updated.StockBaixo.Should().BeTrue();

        var low = await client.GetFromJsonAsync<IReadOnlyList<PartDto>>("/api/parts/low-stock");
        low!.Select(p => p.Id).Should().Contain(part.Id);
    }

    [Fact]
    public async Task AddPartToReparacao_DecrementsStockAndRecalculatesCustoPecas()
    {
        var client = await NewAuthedClient(RepairDeskApiFactory.AdminEmail);
        var cliente = await CreateClienteAsync(client);
        var reparacao = await CreateReparacaoAsync(client, cliente.Id);
        var part = await CreatePartAsync(client, new CreatePartRequest(
            "LCD-A52-" + Guid.NewGuid().ToString("N")[..6],
            "Ecrã Samsung A52",
            PartCategoria.Ecra,
            "Samsung",
            "A52",
            null,
            QtdStock: 3,
            QtdMinima: 1,
            CustoUnitarioCents: 4200,
            Fornecedor: "Mobiltrust",
            LocalArmazenamento: "B1",
            Notas: null));

        var movResp = await client.PostAsJsonAsync($"/api/parts/{part.Id}/movimento",
            new CreatePartMovimentoRequest(-1, PartMovimentoMotivo.UsoEmReparacao, reparacao.Id, "Ecrã usado"));
        movResp.EnsureSuccessStatusCode();

        var updatedPart = await client.GetFromJsonAsync<PartDto>($"/api/parts/{part.Id}");
        updatedPart!.QtdStock.Should().Be(2);

        var detail = await client.GetFromJsonAsync<ReparacaoDetalhadaDto>($"/api/reparacoes/{reparacao.Id}");
        detail!.Reparacao.CustoPecasCents.Should().Be(4200);
        detail.Reparacao.LucroCents.Should().Be((detail.Reparacao.PrecoFinalCents ?? 0) - 4200 - detail.Reparacao.CustoDespesasCents);

        var movimentos = await client.GetFromJsonAsync<IReadOnlyList<PartMovimentoDto>>($"/api/parts/movimentos?reparacaoId={reparacao.Id}");
        movimentos!.Should().ContainSingle(m => m.PartId == part.Id && m.ReparacaoId == reparacao.Id);
    }

    [Fact]
    public async Task LowStock_Filter_ReturnsOnlyPartsBelowMinimum()
    {
        var client = await NewAuthedClient(RepairDeskApiFactory.AdminEmail);
        var marker = Guid.NewGuid().ToString("N")[..6];
        var low = await CreatePartAsync(client, new CreatePartRequest("LOW-" + marker, "Peça baixa " + marker, PartCategoria.Conector, "Apple", "iPhone 11", null, 1, 2, 900, null, null, null));
        var ok = await CreatePartAsync(client, new CreatePartRequest("OK-" + marker, "Peça ok " + marker, PartCategoria.Conector, "Apple", "iPhone 11", null, 8, 2, 900, null, null, null));

        var result = await client.GetFromJsonAsync<PagedResult<PartDto>>($"/api/parts?q={marker}&lowStockOnly=true");

        result!.Items.Select(p => p.Id).Should().Contain(low.Id).And.NotContain(ok.Id);
    }

    [Fact]
    public async Task TenantIsolation_TenantA_DoesNotSeeTenantB_Parts()
    {
        var clientA = await NewAuthedClient(RepairDeskApiFactory.AdminEmail);
        var clientB = await NewAuthedClient(RepairDeskApiFactory.SecondAdminEmail);
        var marker = "ISO-" + Guid.NewGuid().ToString("N")[..8];

        var inA = await CreatePartAsync(clientA, new CreatePartRequest(marker + "-A", "Peça A " + marker, PartCategoria.Outro, null, null, null, 5, 1, 100, null, null, null));
        var inB = await CreatePartAsync(clientB, new CreatePartRequest(marker + "-B", "Peça B " + marker, PartCategoria.Outro, null, null, null, 5, 1, 100, null, null, null));

        var listA = await clientA.GetFromJsonAsync<PagedResult<PartDto>>($"/api/parts?q={marker}");
        listA!.Items.Select(p => p.Id).Should().Contain(inA.Id).And.NotContain(inB.Id);

        var crossGet = await clientA.GetAsync($"/api/parts/{inB.Id}");
        crossGet.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// Sprint 198 regression: Uso seguido de Devolução resulta em consumo NET 0.
    /// Bruno bug 1ª vez: widget Reabastecer contava 3 quando real era 2 após estorno.
    /// </summary>
    [Fact]
    public async Task ReabastecerSugestoes_UsoMaisDevolucao_NetZero_NaoConta()
    {
        var client = await NewAuthedClient(RepairDeskApiFactory.AdminEmail);
        var part = await CreatePartAsync(client, new CreatePartRequest(
            "REG198-" + Guid.NewGuid().ToString("N")[..6], "Test net zero",
            PartCategoria.Outro, null, null, null, QtdStock: 1, QtdMinima: 1,
            CustoUnitarioCents: 100, Fornecedor: null, LocalArmazenamento: null, Notas: null));

        var cliente = await CreateClienteAsync(client);
        var reparacao = await CreateReparacaoAsync(client, cliente.Id);

        // Uso (qty -1) seguido de Devolução (qty +1) na mesma reparação
        await client.PostAsJsonAsync($"/api/parts/{part.Id}/movimento",
            new CreatePartMovimentoRequest(-1, PartMovimentoMotivo.UsoEmReparacao, reparacao.Id, "Test uso"));
        await client.PostAsJsonAsync($"/api/parts/{part.Id}/movimento",
            new CreatePartMovimentoRequest(1, PartMovimentoMotivo.Devolucao, reparacao.Id, "Test devolução"));

        var sugestoes = await client.GetFromJsonAsync<IReadOnlyList<ReabastecerSugestao>>("/api/parts/reabastecer-sugestoes?days=30");
        sugestoes!.Should().NotContain(s => s.PartId == part.Id, "net=0 não deve aparecer no reabastecer");
    }

    /// <summary>
    /// Sprint 208 regression: movimentos de reparações soft-deleted NÃO contam no consumo.
    /// Bruno bug 2ª vez: peça aparecia 2/30d mas só usou 1× — outra reparação tinha sido apagada.
    /// </summary>
    [Fact]
    public async Task ReabastecerSugestoes_ReparacaoApagada_NaoConta()
    {
        var client = await NewAuthedClient(RepairDeskApiFactory.AdminEmail);
        var part = await CreatePartAsync(client, new CreatePartRequest(
            "REG208-" + Guid.NewGuid().ToString("N")[..6], "Test deleted rep",
            PartCategoria.Outro, null, null, null, QtdStock: 1, QtdMinima: 1,
            CustoUnitarioCents: 100, Fornecedor: null, LocalArmazenamento: null, Notas: null));

        var cliente = await CreateClienteAsync(client);
        var repApagada = await CreateReparacaoAsync(client, cliente.Id);

        // Uso na reparação ANTES de apagar
        await client.PostAsJsonAsync($"/api/parts/{part.Id}/movimento",
            new CreatePartMovimentoRequest(-1, PartMovimentoMotivo.UsoEmReparacao, repApagada.Id, "Test uso pré-delete"));

        // Apagar reparação (soft-delete)
        var del = await client.DeleteAsync($"/api/reparacoes/{repApagada.Id}");
        del.StatusCode.Should().BeOneOf(HttpStatusCode.NoContent, HttpStatusCode.OK);

        var sugestoes = await client.GetFromJsonAsync<IReadOnlyList<ReabastecerSugestao>>("/api/parts/reabastecer-sugestoes?days=30");
        sugestoes!.Should().NotContain(s => s.PartId == part.Id, "movimentos de reparações apagadas não devem contar");
    }

    /// <summary>
    /// Sprint 214: endpoint admin purge apaga movimentos de reparações soft-deleted.
    /// </summary>
    [Fact]
    public async Task PurgeOrphanMovimentos_ApagaMovimentosDeReparacoesDeleted()
    {
        var client = await NewAuthedClient(RepairDeskApiFactory.AdminEmail);
        var part = await CreatePartAsync(client, new CreatePartRequest(
            "ORPH-" + Guid.NewGuid().ToString("N")[..6], "Test orphan",
            PartCategoria.Outro, null, null, null, 1, 1, 100, null, null, null));

        var cliente = await CreateClienteAsync(client);
        var rep = await CreateReparacaoAsync(client, cliente.Id);

        await client.PostAsJsonAsync($"/api/parts/{part.Id}/movimento",
            new CreatePartMovimentoRequest(-1, PartMovimentoMotivo.UsoEmReparacao, rep.Id, "Test"));

        var del = await client.DeleteAsync($"/api/reparacoes/{rep.Id}");
        del.StatusCode.Should().BeOneOf(HttpStatusCode.NoContent, HttpStatusCode.OK);

        var purge = await client.PostAsync("/api/parts/admin/orphan-movimentos/purge", null);
        purge.EnsureSuccessStatusCode();
        var result = await purge.Content.ReadFromJsonAsync<PurgeOrphansResult>();
        result!.Purged.Should().BeGreaterThan(0);
    }

    private record PurgeOrphansResult(int Purged);

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

    private static async Task<PartDto> CreatePartAsync(HttpClient client, CreatePartRequest req)
    {
        var resp = await client.PostAsJsonAsync("/api/parts", req);
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<PartDto>())!;
    }

    private static async Task<ClienteDto> CreateClienteAsync(HttpClient client)
    {
        var resp = await client.PostAsJsonAsync("/api/clientes",
            new CreateClienteRequest("Cliente Stock " + Guid.NewGuid().ToString("N")[..6], "912000111", null, null, null));
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<ClienteDto>())!;
    }

    private static async Task<ReparacaoDto> CreateReparacaoAsync(HttpClient client, Guid clienteId)
    {
        var resp = await client.PostAsJsonAsync("/api/reparacoes",
            new CreateReparacaoRequest(clienteId, "Samsung A52", "Ecrã partido", "359123456789012", 12000, null));
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<ReparacaoDto>())!;
    }
}
