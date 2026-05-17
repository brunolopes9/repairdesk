using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using RepairDesk.API.Infrastructure;
using RepairDesk.Core.Enums;
using RepairDesk.Services.Clientes;
using RepairDesk.Services.Dashboard;
using RepairDesk.Services.Despesas;
using RepairDesk.Services.Trabalhos;
using RepairDesk.Tests.Auth;

namespace RepairDesk.Tests.Dashboard;

public class DashboardApiTests : IClassFixture<RepairDeskApiFactory>
{
    private readonly RepairDeskApiFactory _factory;
    public DashboardApiTests(RepairDeskApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Dashboard_ReflectsTrabalhoConcluidoPagoAndDespesa()
    {
        var client = await NewAuthedClient(RepairDeskApiFactory.AdminEmail);

        // Cria cliente para o trabalho (ClienteId agora obrigatório)
        var phone = "9" + Random.Shared.Next(10000000, 99999999).ToString();
        var clienteResp = await client.PostAsJsonAsync("/api/clientes",
            new CreateClienteRequest("Junta de Freguesia", phone, null, null, null));
        clienteResp.EnsureSuccessStatusCode();
        var cliente = (await clienteResp.Content.ReadFromJsonAsync<ClienteDto>())!;

        // Cria um trabalho concluído pago
        var create = await client.PostAsJsonAsync("/api/trabalhos",
            new CreateTrabalhoRequest(cliente.Id, "Site Junta", null, JobCategory.Website, 50000, null));
        create.EnsureSuccessStatusCode();
        var trabalho = (await create.Content.ReadFromJsonAsync<TrabalhoDto>())!;

        var update = new UpdateTrabalhoRequest(
            ClienteId: null,
            Titulo: trabalho.Titulo,
            Descricao: null,
            Categoria: JobCategory.Website,
            Status: TrabalhoStatus.Concluido,
            DataInicio: DateTime.UtcNow.AddDays(-5),
            DataConclusao: DateTime.UtcNow,
            OrcamentoCents: 50000,
            PrecoFinalCents: 60000,
            HorasGastas: 10m,
            Notas: null,
            EstadoPagamento: PaymentStatus.Pago);
        var u = await client.PutAsJsonAsync($"/api/trabalhos/{trabalho.Id}", update);
        u.EnsureSuccessStatusCode();

        // Despesa do mês
        var dResp = await client.PostAsJsonAsync("/api/despesas",
            new CreateDespesaRequest("Domínio + hosting", DespesaCategoria.Software, 7500, DateTime.UtcNow, "Cloudflare", null, null, null, null));
        dResp.EnsureSuccessStatusCode();

        var dash = await client.GetFromJsonAsync<DashboardResponse>("/api/dashboard");
        dash!.Kpis.ReceitaCentsMes.Should().BeGreaterThanOrEqualTo(60000);
        dash.Kpis.DespesasCentsMes.Should().BeGreaterThanOrEqualTo(7500);
        dash.Kpis.LucroCentsMes.Should().Be(dash.Kpis.ReceitaCentsMes - dash.Kpis.DespesasCentsMes);
        dash.ReceitaPorCategoria.Should().Contain(c => c.Label == "Website");
        dash.DespesaPorCategoria.Should().Contain(c => c.Label == "Software");
    }

    [Fact]
    public async Task Dashboard_TenantA_DoesNotSeeTenantB_Numbers()
    {
        var clientA = await NewAuthedClient(RepairDeskApiFactory.AdminEmail);
        var clientB = await NewAuthedClient(RepairDeskApiFactory.SecondAdminEmail);

        // B cria + paga trabalho
        var b = (await (await clientB.PostAsJsonAsync("/api/trabalhos",
            new CreateTrabalhoRequest(null, "Iso B", null, JobCategory.Software, 40000, null)))
            .Content.ReadFromJsonAsync<TrabalhoDto>())!;
        await clientB.PutAsJsonAsync($"/api/trabalhos/{b.Id}",
            new UpdateTrabalhoRequest(null, b.Titulo, null, JobCategory.Software, TrabalhoStatus.Concluido,
                DateTime.UtcNow, DateTime.UtcNow, 40000, 40000, 1, null, PaymentStatus.Pago));

        var dashA = await clientA.GetFromJsonAsync<DashboardResponse>("/api/dashboard");
        // A não tem nada em Software
        dashA!.ReceitaPorCategoria.Should().NotContain(c => c.Label == "Software" && c.TotalCents >= 40000);
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
