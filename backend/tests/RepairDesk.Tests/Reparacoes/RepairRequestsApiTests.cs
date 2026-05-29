using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using RepairDesk.API.Infrastructure;
using RepairDesk.Tests.Auth;

namespace RepairDesk.Tests.Reparacoes;

/// <summary>
/// Sprint 354/356: cobre o widget público de pedido de reparação e a gestão
/// interna (converter/rejeitar). Foco em segurança: honeypot, validação, slug.
/// </summary>
public class RepairRequestsApiTests : IClassFixture<RepairDeskApiFactory>
{
    private readonly RepairDeskApiFactory _factory;
    public RepairRequestsApiTests(RepairDeskApiFactory factory) => _factory = factory;

    private sealed record IntakeWidgetDto(string Slug, string? PublicUrl);
    private sealed record SubmitRequest(string Nome, string? Email, string? Telefone, string Equipamento, string Descricao, string? Website);
    private sealed record RequestDto(
        Guid Id, string Nome, string? Email, string? Telefone, string Equipamento,
        string Descricao, int Estado, Guid? ReparacaoId, string? MotivoRejeicao, DateTime CreatedAt,
        string? NotasInternas, int Prioridade, Guid? TrabalhoId, int Origem);
    // Sprint 436+438+439 payloads para os tests.
    private sealed record TriagemRequest(string? NotasInternas, int Prioridade);
    private sealed record ManualRequest(string Nome, string? Telefone, string? Email, string Equipamento, string Descricao, int Origem, int? Prioridade, string? NotasInternas);

    [Fact]
    public async Task Submit_Valido_CriaPedido_AdminVePendente()
    {
        var admin = await NewAuthedClient(RepairDeskApiFactory.AdminEmail);
        var slug = await EnsureIntakeSlugAsync(admin);

        var anon = _factory.CreateClient();
        var marker = Guid.NewGuid().ToString("N")[..8];
        var submit = await anon.PostAsJsonAsync($"/api/public/repair-requests/{slug}",
            new SubmitRequest($"Cliente {marker}", "c@ex.pt", "912000000", "iPhone 13", $"Ecrã partido {marker}", Website: null));
        submit.StatusCode.Should().Be(HttpStatusCode.OK);

        var pendentes = await admin.GetFromJsonAsync<List<RequestDto>>("/api/repair-requests?estado=0");
        pendentes!.Should().Contain(r => r.Descricao.Contains(marker) && r.Estado == 0);
    }

    [Fact]
    public async Task Submit_Honeypot_NaoCriaPedido_MasDevolveOk()
    {
        var admin = await NewAuthedClient(RepairDeskApiFactory.AdminEmail);
        var slug = await EnsureIntakeSlugAsync(admin);

        var anon = _factory.CreateClient();
        var marker = Guid.NewGuid().ToString("N")[..8];
        // Website preenchido = bot. Devolve 200 falso, não cria.
        var submit = await anon.PostAsJsonAsync($"/api/public/repair-requests/{slug}",
            new SubmitRequest($"Bot {marker}", null, null, "spam", $"spam {marker}", Website: "http://spam.example"));
        submit.StatusCode.Should().Be(HttpStatusCode.OK);

        var pendentes = await admin.GetFromJsonAsync<List<RequestDto>>("/api/repair-requests?estado=0");
        pendentes!.Should().NotContain(r => r.Descricao.Contains(marker), "honeypot deve descartar o pedido silenciosamente");
    }

    [Fact]
    public async Task Submit_SlugInvalido_404()
    {
        var anon = _factory.CreateClient();
        var resp = await anon.PostAsJsonAsync("/api/public/repair-requests/slug-que-nao-existe-xyz",
            new SubmitRequest("X", null, null, "iPhone", "problema qualquer", null));
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Submit_NomeCurto_400()
    {
        var admin = await NewAuthedClient(RepairDeskApiFactory.AdminEmail);
        var slug = await EnsureIntakeSlugAsync(admin);
        var anon = _factory.CreateClient();
        var resp = await anon.PostAsJsonAsync($"/api/public/repair-requests/{slug}",
            new SubmitRequest("A", null, null, "iPhone 13", "descrição válida aqui", null));
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Converter_CriaReparacao_E_MarcaConvertido()
    {
        var admin = await NewAuthedClient(RepairDeskApiFactory.AdminEmail);
        var slug = await EnsureIntakeSlugAsync(admin);

        var anon = _factory.CreateClient();
        var marker = Guid.NewGuid().ToString("N")[..8];
        await anon.PostAsJsonAsync($"/api/public/repair-requests/{slug}",
            new SubmitRequest($"Maria {marker}", null, "913000000", "Samsung A52", $"Não liga {marker}", null));

        var pendentes = await admin.GetFromJsonAsync<List<RequestDto>>("/api/repair-requests?estado=0");
        var pedido = pendentes!.First(r => r.Descricao.Contains(marker));

        var conv = await admin.PostAsync($"/api/repair-requests/{pedido.Id}/converter", null);
        conv.StatusCode.Should().Be(HttpStatusCode.OK);
        var convertido = await conv.Content.ReadFromJsonAsync<RequestDto>();
        convertido!.Estado.Should().Be(1); // Convertido
        convertido.ReparacaoId.Should().NotBeNull();

        // Segunda conversão deve falhar (já tratado).
        var conv2 = await admin.PostAsync($"/api/repair-requests/{pedido.Id}/converter", null);
        conv2.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task TenantIsolation_PedidoDeTenantA_NaoApareceEmTenantB()
    {
        var adminA = await NewAuthedClient(RepairDeskApiFactory.AdminEmail);
        var adminB = await NewAuthedClient(RepairDeskApiFactory.SecondAdminEmail);
        var slugA = await EnsureIntakeSlugAsync(adminA);

        var anon = _factory.CreateClient();
        var marker = Guid.NewGuid().ToString("N")[..8];
        await anon.PostAsJsonAsync($"/api/public/repair-requests/{slugA}",
            new SubmitRequest($"IsoTest {marker}", null, null, "iPad", $"iso {marker}", null));

        var pendentesB = await adminB.GetFromJsonAsync<List<RequestDto>>("/api/repair-requests?estado=0");
        pendentesB!.Should().NotContain(r => r.Descricao.Contains(marker), "pedido de tenant A não pode vazar para tenant B");
    }

    // Nota: usamos /manual (S439) para criar pedidos nestes tests porque o endpoint
    // público anon tem cap de 5/24h por IP, e os tests partilham fixture.
    private async Task<RequestDto> CreateManualPedidoAsync(HttpClient admin, string marker, string equipamento = "iPad mini")
    {
        var resp = await admin.PostAsJsonAsync("/api/repair-requests/manual",
            new ManualRequest($"Cliente {marker}", "914000000", null, equipamento, $"avaria {marker}", Origem: 1, Prioridade: null, NotasInternas: null));
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await resp.Content.ReadFromJsonAsync<RequestDto>())!;
    }

    [Fact]
    public async Task Sprint436_Triagem_GuardaNotasEPrioridade()
    {
        var admin = await NewAuthedClient(RepairDeskApiFactory.AdminEmail);
        var marker = Guid.NewGuid().ToString("N")[..8];
        var pedido = await CreateManualPedidoAsync(admin, marker);
        pedido.Prioridade.Should().Be(1); // Normal default
        pedido.NotasInternas.Should().BeNull();

        var resp = await admin.PutAsJsonAsync($"/api/repair-requests/{pedido.Id}/triagem",
            new TriagemRequest("cliente já ligou, espera estimativa", Prioridade: 3));
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var atualizado = (await resp.Content.ReadFromJsonAsync<RequestDto>())!;
        atualizado.NotasInternas.Should().Contain("já ligou");
        atualizado.Prioridade.Should().Be(3); // Urgente
    }

    [Fact]
    public async Task Sprint436_Triagem_NotasMuitoLongas_400()
    {
        var admin = await NewAuthedClient(RepairDeskApiFactory.AdminEmail);
        var marker = Guid.NewGuid().ToString("N")[..8];
        var pedido = await CreateManualPedidoAsync(admin, marker, equipamento: "iPad");
        var notasLongas = new string('x', 2001);
        var resp = await admin.PutAsJsonAsync($"/api/repair-requests/{pedido.Id}/triagem",
            new TriagemRequest(notasLongas, 1));
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Sprint437_ConverterEmTrabalho_CriaTrabalho_E_MarcaConvertido()
    {
        var admin = await NewAuthedClient(RepairDeskApiFactory.AdminEmail);
        var marker = Guid.NewGuid().ToString("N")[..8];
        var pedido = await CreateManualPedidoAsync(admin, marker, equipamento: "MacBook Pro");

        var conv = await admin.PostAsync($"/api/repair-requests/{pedido.Id}/converter-em-trabalho", null);
        conv.StatusCode.Should().Be(HttpStatusCode.OK);
        var convertido = (await conv.Content.ReadFromJsonAsync<RequestDto>())!;
        convertido.Estado.Should().Be(1); // Convertido
        convertido.TrabalhoId.Should().NotBeNull();
        convertido.ReparacaoId.Should().BeNull("este caminho cria Trabalho, não Reparacao");

        // Segunda conversão deve falhar (já tratado).
        var conv2 = await admin.PostAsync($"/api/repair-requests/{pedido.Id}/converter-em-trabalho", null);
        conv2.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Sprint439_Manual_CriaPedidoNaInbox()
    {
        var admin = await NewAuthedClient(RepairDeskApiFactory.AdminEmail);
        var marker = Guid.NewGuid().ToString("N")[..8];
        var resp = await admin.PostAsJsonAsync("/api/repair-requests/manual",
            new ManualRequest($"Telefone {marker}", "917000000", null, "Xiaomi 13", $"ligou-me {marker}", Origem: 1, Prioridade: 2, NotasInternas: "voltar a ligar"));
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var criado = (await resp.Content.ReadFromJsonAsync<RequestDto>())!;
        criado.Estado.Should().Be(0); // Pendente
        criado.Origem.Should().Be(1); // Telefone
        criado.Prioridade.Should().Be(2); // Alta
        criado.NotasInternas.Should().Be("voltar a ligar");

        // Aparece na inbox.
        var pendentes = await admin.GetFromJsonAsync<List<RequestDto>>("/api/repair-requests?estado=0");
        pendentes!.Should().Contain(r => r.Id == criado.Id);
    }

    [Fact]
    public async Task Sprint439_Manual_OrigemWidget_400()
    {
        var admin = await NewAuthedClient(RepairDeskApiFactory.AdminEmail);
        var marker = Guid.NewGuid().ToString("N")[..8];
        // Widget = 0 — esse canal é exclusivo do endpoint público anónimo.
        var resp = await admin.PostAsJsonAsync("/api/repair-requests/manual",
            new ManualRequest($"FakeWidget {marker}", "918000000", null, "iPhone", $"x {marker}", Origem: 0, Prioridade: null, NotasInternas: null));
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Rejeitar_GuardaMotivo_E_MarcaRejeitado()
    {
        var admin = await NewAuthedClient(RepairDeskApiFactory.AdminEmail);
        var marker = Guid.NewGuid().ToString("N")[..8];
        var pedido = await CreateManualPedidoAsync(admin, marker, equipamento: "iPad Air");

        var resp = await admin.PostAsJsonAsync($"/api/repair-requests/{pedido.Id}/rejeitar",
            new { Motivo = "Cliente desistiu — preço acima do esperado" });
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var rejeitado = (await resp.Content.ReadFromJsonAsync<RequestDto>())!;
        rejeitado.Estado.Should().Be(2); // Rejeitado
        rejeitado.MotivoRejeicao.Should().Contain("desistiu");

        // Segunda tentativa devolve Conflict — pedido já tratado.
        var again = await admin.PostAsJsonAsync($"/api/repair-requests/{pedido.Id}/rejeitar",
            new { Motivo = "spam" });
        again.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Sprint439_Manual_SemContacto_400()
    {
        var admin = await NewAuthedClient(RepairDeskApiFactory.AdminEmail);
        var marker = Guid.NewGuid().ToString("N")[..8];
        var resp = await admin.PostAsJsonAsync("/api/repair-requests/manual",
            new ManualRequest($"NoContact {marker}", null, null, "iPad", $"x {marker}", Origem: 1, Prioridade: null, NotasInternas: null));
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private async Task<string> EnsureIntakeSlugAsync(HttpClient admin)
    {
        var resp = await admin.GetAsync("/api/automacoes/intake-widget");
        resp.EnsureSuccessStatusCode();
        var dto = await resp.Content.ReadFromJsonAsync<IntakeWidgetDto>();
        dto!.Slug.Should().NotBeNullOrWhiteSpace();
        return dto.Slug;
    }

    private async Task<HttpClient> NewAuthedClient(string email)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true, AllowAutoRedirect = false });
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, RepairDeskApiFactory.AdminPassword));
        login.EnsureSuccessStatusCode();
        var auth = (await login.Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }
}
