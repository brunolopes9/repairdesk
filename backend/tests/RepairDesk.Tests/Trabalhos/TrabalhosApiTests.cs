using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using RepairDesk.API.Infrastructure;
using RepairDesk.Core.Enums;
using RepairDesk.Services.Clientes;
using RepairDesk.Services.Despesas;
using RepairDesk.Services.Trabalhos;
using RepairDesk.Tests.Auth;

namespace RepairDesk.Tests.Trabalhos;

public class TrabalhosApiTests : IClassFixture<RepairDeskApiFactory>
{
    private readonly RepairDeskApiFactory _factory;
    public TrabalhosApiTests(RepairDeskApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Create_WithoutCliente_Rejects422()
    {
        var client = await NewAuthedClient(RepairDeskApiFactory.AdminEmail);

        var resp = await client.PostAsJsonAsync("/api/trabalhos",
            new CreateTrabalhoRequest(
                ClienteId: null,
                Titulo: "Material para stock",
                Descricao: "Pedido geral",
                Categoria: JobCategory.EquipamentoNovo,
                OrcamentoCents: 5000,
                Notas: null));

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Create_WithCliente_AttachesIt()
    {
        var client = await NewAuthedClient(RepairDeskApiFactory.AdminEmail);
        var cliente = await CreateCliente(client, "JuntaFreguesia");

        var resp = await client.PostAsJsonAsync("/api/trabalhos",
            new CreateTrabalhoRequest(
                ClienteId: cliente.Id,
                Titulo: "Website Junta",
                Descricao: null,
                Categoria: JobCategory.Website,
                OrcamentoCents: 80000,
                Notas: null));

        resp.EnsureSuccessStatusCode();
        var dto = await resp.Content.ReadFromJsonAsync<TrabalhoDto>();
        dto!.Cliente.Should().NotBeNull();
        dto.Cliente!.Id.Should().Be(cliente.Id);
    }

    [Fact]
    public async Task Update_ChangesStatusAndCalculatesFinal()
    {
        var client = await NewAuthedClient(RepairDeskApiFactory.AdminEmail);
        var dto = await CreateOne(client, JobCategory.Software);

        var update = new UpdateTrabalhoRequest(
            ClienteId: null,
            Titulo: dto.Titulo,
            Descricao: dto.Descricao,
            Categoria: JobCategory.Software,
            Status: TrabalhoStatus.Concluido,
            DataInicio: DateTime.UtcNow.AddDays(-2),
            DataConclusao: DateTime.UtcNow,
            OrcamentoCents: 10000,
            PrecoFinalCents: 12000,
            HorasGastas: 8m,
            Notas: "feito",
            EstadoPagamento: PaymentStatus.Pago);

        var resp = await client.PutAsJsonAsync($"/api/trabalhos/{dto.Id}", update);
        resp.EnsureSuccessStatusCode();
        var updated = await resp.Content.ReadFromJsonAsync<TrabalhoDto>();
        updated!.Status.Should().Be(TrabalhoStatus.Concluido);
        updated.PrecoFinalCents.Should().Be(12000);
        updated.EstadoPagamento.Should().Be(PaymentStatus.Pago);
    }

    [Fact]
    public async Task Search_FilterByCategoria()
    {
        var client = await NewAuthedClient(RepairDeskApiFactory.AdminEmail);
        await CreateOne(client, JobCategory.Website);
        await CreateOne(client, JobCategory.Software);

        var websites = await client.GetFromJsonAsync<PagedResult<TrabalhoDto>>("/api/trabalhos?categoria=1");
        websites!.Items.Should().NotBeEmpty();
        websites.Items.Should().OnlyContain(t => t.Categoria == JobCategory.Website);
    }

    [Fact]
    public async Task LucroReal_SubtractsLinkedDespesas()
    {
        var client = await NewAuthedClient(RepairDeskApiFactory.AdminEmail);
        var trabalho = await CreateOne(client, JobCategory.Website);

        // marca como concluído com preço final
        await client.PutAsJsonAsync($"/api/trabalhos/{trabalho.Id}",
            new UpdateTrabalhoRequest(
                ClienteId: null, Titulo: trabalho.Titulo, Descricao: null,
                Categoria: JobCategory.Website, Status: TrabalhoStatus.Concluido,
                DataInicio: DateTime.UtcNow.AddDays(-1), DataConclusao: DateTime.UtcNow,
                OrcamentoCents: 50000, PrecoFinalCents: 50000, HorasGastas: 5m,
                Notas: null, EstadoPagamento: PaymentStatus.Pago));

        // adiciona 2 despesas linked
        await client.PostAsJsonAsync("/api/despesas",
            new CreateDespesaRequest("Domínio", DespesaCategoria.Software, 1500, null, null, null, null, trabalho.Id, null));
        await client.PostAsJsonAsync("/api/despesas",
            new CreateDespesaRequest("Hosting", DespesaCategoria.Software, 2500, null, null, null, null, trabalho.Id, null));

        var dto = await client.GetFromJsonAsync<TrabalhoDto>($"/api/trabalhos/{trabalho.Id}");
        dto!.CustoDespesasCents.Should().Be(4000);
        dto.LucroCents.Should().Be(50000 - 4000); // 46000
    }

    [Fact]
    public async Task TenantIsolation_TrabalhosScoped()
    {
        var clientA = await NewAuthedClient(RepairDeskApiFactory.AdminEmail);
        var clientB = await NewAuthedClient(RepairDeskApiFactory.SecondAdminEmail);
        var inA = await CreateOne(clientA, JobCategory.Outro);
        var inB = await CreateOne(clientB, JobCategory.Outro);

        var crossGet = await clientA.GetAsync($"/api/trabalhos/{inB.Id}");
        crossGet.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var listA = await clientA.GetFromJsonAsync<PagedResult<TrabalhoDto>>("/api/trabalhos");
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

    private static async Task<ClienteDto> CreateCliente(HttpClient client, string nome)
    {
        var phone = "9" + Random.Shared.Next(10000000, 99999999).ToString();
        var resp = await client.PostAsJsonAsync("/api/clientes",
            new CreateClienteRequest(nome, phone, null, null, null));
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<ClienteDto>())!;
    }

    private static async Task<TrabalhoDto> CreateOne(HttpClient client, JobCategory cat)
    {
        var cliente = await CreateCliente(client, $"Cliente-{Guid.NewGuid():N}");
        var resp = await client.PostAsJsonAsync("/api/trabalhos",
            new CreateTrabalhoRequest(cliente.Id, $"Trab-{Guid.NewGuid():N}", null, cat, null, null));
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<TrabalhoDto>())!;
    }
}
