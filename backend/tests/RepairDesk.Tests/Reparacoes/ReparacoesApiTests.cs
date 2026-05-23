using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using RepairDesk.API.Infrastructure;
using RepairDesk.Core.Enums;
using RepairDesk.Services.Clientes;
using RepairDesk.Services.Reparacoes;
using RepairDesk.Services.TenantPreferences;
using RepairDesk.Tests.Auth;

namespace RepairDesk.Tests.Reparacoes;

public class ReparacoesApiTests : IClassFixture<RepairDeskApiFactory>
{
    private readonly RepairDeskApiFactory _factory;

    public ReparacoesApiTests(RepairDeskApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Create_AssignsSequentialNumero_AndSeedsTimeline()
    {
        var client = await NewAuthedClient(RepairDeskApiFactory.AdminEmail);
        var cliente = await CreateCliente(client, "Cli-A");

        var first = await Create(client, cliente.Id, "iPhone 13", "Ecrã partido");
        var second = await Create(client, cliente.Id, "iPhone 14", "Bateria");

        second.Numero.Should().Be(first.Numero + 1);

        var detail = await client.GetFromJsonAsync<ReparacaoDetalhadaDto>($"/api/reparacoes/{first.Id}");
        detail!.Reparacao.Estado.Should().Be(RepairStatus.Recebido);
        detail.Timeline.Should().ContainSingle();
        detail.Timeline[0].EstadoTo.Should().Be(RepairStatus.Recebido);
        detail.Timeline[0].EstadoFrom.Should().BeNull();
    }

    [Fact]
    public async Task ChangeEstado_FollowsValidWorkflow_AndAppendsTimeline()
    {
        var client = await NewAuthedClient(RepairDeskApiFactory.AdminEmail);
        var cliente = await CreateCliente(client, "Cli-B");
        var rep = await Create(client, cliente.Id, "Samsung", "Não liga");

        var transitions = new[]
        {
            RepairStatus.Diagnostico, // = Diagnóstico Concluído
            RepairStatus.Pronto,       // = Reparado
            RepairStatus.Entregue,
        };

        foreach (var st in transitions)
        {
            var resp = await client.PostAsJsonAsync($"/api/reparacoes/{rep.Id}/estado",
                new ChangeEstadoRequest(st, $"para {st}"));
            resp.EnsureSuccessStatusCode();
            var dto = await resp.Content.ReadFromJsonAsync<ReparacaoDto>();
            dto!.Estado.Should().Be(st);
        }

        var detail = await client.GetFromJsonAsync<ReparacaoDetalhadaDto>($"/api/reparacoes/{rep.Id}");
        detail!.Timeline.Should().HaveCount(1 + transitions.Length);
        detail.Reparacao.EntregueEm.Should().NotBeNull();
        detail.Timeline.Last().EstadoTo.Should().Be(RepairStatus.Entregue);
    }

    [Fact]
    public async Task ChangeEstado_ToEntregue_AutoMarksPago()
    {
        var client = await NewAuthedClient(RepairDeskApiFactory.AdminEmail);
        var prefs = await client.GetFromJsonAsync<TenantPreferencesRoot>("/api/tenant-settings/me/preferences");
        prefs = prefs! with { Repairs = prefs.Repairs with { EntregarMarcaPago = EntregarMarcaPagoMode.Sim } };
        (await client.PutAsJsonAsync("/api/tenant-settings/me/preferences", prefs)).EnsureSuccessStatusCode();

        var cliente = await CreateCliente(client, "Cli-Pago");
        var rep = await Create(client, cliente.Id, "iPhone", "x");

        // Avança ao longo do workflow
        foreach (var st in new[] { RepairStatus.Diagnostico, RepairStatus.Pronto })
        {
            var r = await client.PostAsJsonAsync($"/api/reparacoes/{rep.Id}/estado", new ChangeEstadoRequest(st, null));
            r.EnsureSuccessStatusCode();
        }

        // Marca Entregue — deve auto-marcar Pago
        var entregue = await client.PostAsJsonAsync($"/api/reparacoes/{rep.Id}/estado",
            new ChangeEstadoRequest(RepairStatus.Entregue, null));
        entregue.EnsureSuccessStatusCode();
        var dto = await entregue.Content.ReadFromJsonAsync<ReparacaoDto>();
        dto!.Estado.Should().Be(RepairStatus.Entregue);
        dto.EstadoPagamento.Should().Be(PaymentStatus.Pago);
    }

    [Fact]
    public async Task ChangeEstado_ToEntregue_WithPreferenceNao_DoesNotAutoMarkPago()
    {
        var client = await NewAuthedClient(RepairDeskApiFactory.AdminEmail);
        var prefs = await client.GetFromJsonAsync<TenantPreferencesRoot>("/api/tenant-settings/me/preferences");
        prefs = prefs! with { Repairs = prefs.Repairs with { EntregarMarcaPago = EntregarMarcaPagoMode.Nao } };
        (await client.PutAsJsonAsync("/api/tenant-settings/me/preferences", prefs)).EnsureSuccessStatusCode();

        var cliente = await CreateCliente(client, "Cli-Nao-Pago");
        var rep = await Create(client, cliente.Id, "iPhone", "x");
        foreach (var st in new[] { RepairStatus.Diagnostico, RepairStatus.Pronto })
            (await client.PostAsJsonAsync($"/api/reparacoes/{rep.Id}/estado", new ChangeEstadoRequest(st, null))).EnsureSuccessStatusCode();

        var entregue = await client.PostAsJsonAsync($"/api/reparacoes/{rep.Id}/estado",
            new ChangeEstadoRequest(RepairStatus.Entregue, null));
        entregue.EnsureSuccessStatusCode();
        var dto = await entregue.Content.ReadFromJsonAsync<ReparacaoDto>();

        dto!.EstadoPagamento.Should().Be(PaymentStatus.NaoPago);
    }

    [Fact]
    public async Task ChangeEstado_ToEntregue_WithPreferencePerguntar_ReturnsConfirmationFlag()
    {
        var client = await NewAuthedClient(RepairDeskApiFactory.AdminEmail);
        var prefs = await client.GetFromJsonAsync<TenantPreferencesRoot>("/api/tenant-settings/me/preferences");
        prefs = prefs! with { Repairs = prefs.Repairs with { EntregarMarcaPago = EntregarMarcaPagoMode.Perguntar } };
        (await client.PutAsJsonAsync("/api/tenant-settings/me/preferences", prefs)).EnsureSuccessStatusCode();

        var cliente = await CreateCliente(client, "Cli-Perguntar-Pago");
        var rep = await Create(client, cliente.Id, "iPhone", "x");
        foreach (var st in new[] { RepairStatus.Diagnostico, RepairStatus.Pronto })
            (await client.PostAsJsonAsync($"/api/reparacoes/{rep.Id}/estado", new ChangeEstadoRequest(st, null))).EnsureSuccessStatusCode();

        var entregue = await client.PostAsJsonAsync($"/api/reparacoes/{rep.Id}/estado",
            new ChangeEstadoRequest(RepairStatus.Entregue, null));
        entregue.EnsureSuccessStatusCode();
        var dto = await entregue.Content.ReadFromJsonAsync<ReparacaoDto>();

        dto!.EstadoPagamento.Should().Be(PaymentStatus.NaoPago);
        dto.PrecisaConfirmacaoPagamento.Should().BeTrue();
    }

    [Fact]
    public async Task ChangeEstado_RejectsInvalidTransition()
    {
        var client = await NewAuthedClient(RepairDeskApiFactory.AdminEmail);
        var cliente = await CreateCliente(client, "Cli-C");
        var rep = await Create(client, cliente.Id, "iPad", "Falha");

        // Recebido → Pronto não é válido
        var bad = await client.PostAsJsonAsync($"/api/reparacoes/{rep.Id}/estado",
            new ChangeEstadoRequest(RepairStatus.Pronto, null));
        bad.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task ChangeEstado_RejectsSameState()
    {
        var client = await NewAuthedClient(RepairDeskApiFactory.AdminEmail);
        var cliente = await CreateCliente(client, "Cli-D");
        var rep = await Create(client, cliente.Id, "x", "y");

        var resp = await client.PostAsJsonAsync($"/api/reparacoes/{rep.Id}/estado",
            new ChangeEstadoRequest(RepairStatus.Recebido, null));
        resp.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Search_ByEstado_ReturnsOnlyMatching()
    {
        var client = await NewAuthedClient(RepairDeskApiFactory.AdminEmail);
        var cliente = await CreateCliente(client, "Cli-E");
        var r1 = await Create(client, cliente.Id, "Equip-X", "z");
        var r2 = await Create(client, cliente.Id, "Equip-X", "z");

        await client.PostAsJsonAsync($"/api/reparacoes/{r2.Id}/estado", new ChangeEstadoRequest(RepairStatus.Diagnostico, null));

        var recebidos = await client.GetFromJsonAsync<PagedResult<ReparacaoDto>>("/api/reparacoes?estado=0");
        recebidos!.Items.Select(i => i.Id).Should().Contain(r1.Id).And.NotContain(r2.Id);

        var diag = await client.GetFromJsonAsync<PagedResult<ReparacaoDto>>("/api/reparacoes?estado=1");
        diag!.Items.Select(i => i.Id).Should().Contain(r2.Id);
    }

    [Fact]
    public async Task Update_RecalculatesLucro()
    {
        var client = await NewAuthedClient(RepairDeskApiFactory.AdminEmail);
        var cliente = await CreateCliente(client, "Cli-F");
        var rep = await Create(client, cliente.Id, "L", "L");

        var update = new UpdateReparacaoRequest(
            Equipamento: rep.Equipamento,
            Avaria: rep.Avaria,
            Imei: null,
            Diagnostico: "Substituir ecrã",
            OrcamentoCents: 8000,
            OrcamentoAprovado: true,
            PrecoFinalCents: 8000,
            CustoPecasCents: 3500,
            HorasGastas: 1.5m,
            Notas: null,
            EstadoPagamento: PaymentStatus.Pago);

        var resp = await client.PutAsJsonAsync($"/api/reparacoes/{rep.Id}", update);
        resp.EnsureSuccessStatusCode();
        var dto = await resp.Content.ReadFromJsonAsync<ReparacaoDto>();
        // Lucro = PrecoFinal (8000) − despesas linked (0). CustoPecas manual está deprecated.
        dto!.LucroCents.Should().Be(8000);
        dto.EstadoPagamento.Should().Be(PaymentStatus.Pago);
    }

    [Fact]
    public async Task TenantIsolation_ReparacoesAreScoped()
    {
        var clientA = await NewAuthedClient(RepairDeskApiFactory.AdminEmail);
        var clientB = await NewAuthedClient(RepairDeskApiFactory.SecondAdminEmail);

        var clienteA = await CreateCliente(clientA, "Iso-A");
        var clienteB = await CreateCliente(clientB, "Iso-B");

        var inA = await Create(clientA, clienteA.Id, "DeviceA", "ProblemA");
        var inB = await Create(clientB, clienteB.Id, "DeviceB", "ProblemB");

        var listA = await clientA.GetFromJsonAsync<PagedResult<ReparacaoDto>>("/api/reparacoes");
        listA!.Items.Select(i => i.Id).Should().Contain(inA.Id).And.NotContain(inB.Id);

        var crossGet = await clientA.GetAsync($"/api/reparacoes/{inB.Id}");
        crossGet.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Create_AsOrcamento_StaysAsRascunho_AndCanTransitionToRecebido()
    {
        var client = await NewAuthedClient(RepairDeskApiFactory.AdminEmail);
        var cliente = await CreateCliente(client, "Cli-Q");

        // Cria como rascunho/orçamento (sem o telemóvel ter chegado)
        var resp = await client.PostAsJsonAsync("/api/reparacoes",
            new CreateReparacaoRequest(cliente.Id, "iPhone 12", "Ecrã partido (orçamento por mensagem)",
                Imei: null, OrcamentoCents: 8000, Notas: null, EstadoInicial: RepairStatus.Orcamento));
        resp.StatusCode.Should().Be(HttpStatusCode.Created);
        var dto = (await resp.Content.ReadFromJsonAsync<ReparacaoDto>())!;
        dto.Estado.Should().Be(RepairStatus.Orcamento);

        // Cliente entrega o telemóvel — passa para Recebido
        var change = await client.PostAsJsonAsync($"/api/reparacoes/{dto.Id}/estado",
            new ChangeEstadoRequest(RepairStatus.Recebido, "Cliente entregou"));
        change.EnsureSuccessStatusCode();
        var updated = await change.Content.ReadFromJsonAsync<ReparacaoDto>();
        updated!.Estado.Should().Be(RepairStatus.Recebido);

        // Timeline tem 2 entradas
        var detail = await client.GetFromJsonAsync<ReparacaoDetalhadaDto>($"/api/reparacoes/{dto.Id}");
        detail!.Timeline.Should().HaveCount(2);
        detail.Timeline[0].EstadoTo.Should().Be(RepairStatus.Orcamento);
        detail.Timeline[1].EstadoTo.Should().Be(RepairStatus.Recebido);
    }

    [Fact]
    public async Task Create_RejectsInvalidEstadoInicial()
    {
        var client = await NewAuthedClient(RepairDeskApiFactory.AdminEmail);
        var cliente = await CreateCliente(client, "Cli-R");

        var resp = await client.PostAsJsonAsync("/api/reparacoes",
            new CreateReparacaoRequest(cliente.Id, "x", "y", null, null, null,
                EstadoInicial: RepairStatus.Pronto));
        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Anonymous_Returns401()
    {
        var client = _factory.CreateClient();
        (await client.GetAsync("/api/reparacoes")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
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

    private static async Task<ReparacaoDto> Create(HttpClient client, Guid clienteId, string equip, string avaria)
    {
        var resp = await client.PostAsJsonAsync("/api/reparacoes",
            new CreateReparacaoRequest(clienteId, equip, avaria, null, null, null));
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<ReparacaoDto>())!;
    }
}
