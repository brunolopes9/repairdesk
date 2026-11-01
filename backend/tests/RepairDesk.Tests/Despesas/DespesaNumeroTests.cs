using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using RepairDesk.API.Infrastructure;
using RepairDesk.Core.Enums;
using RepairDesk.Services.Clientes;
using RepairDesk.Services.Despesas;
using RepairDesk.Tests.Auth;

namespace RepairDesk.Tests.Despesas;

/// <summary>Sprint 584: cada despesa (= uma fatura) tem nº visível sequencial, como as compras; pesquisa pelo nº.</summary>
public class DespesaNumeroTests : IClassFixture<RepairDeskApiFactory>
{
    private readonly RepairDeskApiFactory _factory;
    public DespesaNumeroTests(RepairDeskApiFactory factory) => _factory = factory;

    [Fact]
    public async Task DespesasNovas_TemNumeroSequencial_EPesquisaPeloNumeroEDocumento()
    {
        var client = _factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(RepairDeskApiFactory.AdminEmail, RepairDeskApiFactory.AdminPassword));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await login.Content.ReadFromJsonAsync<AuthResponse>())!.AccessToken);
        var doc = "FT MV/" + Random.Shared.Next(100000, 999999);

        async Task<DespesaDto> Criar(string d, string? numero)
        {
            var r = await client.PostAsJsonAsync("/api/despesas", new CreateDespesaRequest(d, DespesaCategoria.Comunicacoes, 749, new DateTime(2026, 10, 4), "MEO", numero, null));
            r.EnsureSuccessStatusCode();
            return (await r.Content.ReadFromJsonAsync<DespesaDto>())!;
        }
        var a = await Criar("Telemóvel (outubro)", doc);
        var b = await Criar("Telemóvel (novembro)", null);

        a.Numero.Should().BeGreaterThan(0);
        b.Numero.Should().Be(a.Numero + 1);
        var porDoc = await client.GetFromJsonAsync<PagedResult<DespesaDto>>($"/api/despesas?q={Uri.EscapeDataString(doc)}");
        porDoc!.Items.Should().ContainSingle(x => x.Id == a.Id);
        var porNumero = await client.GetFromJsonAsync<PagedResult<DespesaDto>>($"/api/despesas?q={b.Numero}");
        porNumero!.Items.Should().Contain(x => x.Id == b.Id);
    }
}
