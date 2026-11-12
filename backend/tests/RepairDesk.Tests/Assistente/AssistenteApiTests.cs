using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using RepairDesk.API.Controllers;
using RepairDesk.API.Infrastructure;
using RepairDesk.Core.Enums;
using RepairDesk.Services.InternalTasks;
using RepairDesk.Services.Rotina;
using RepairDesk.Services.ServiceApiKeys;
using RepairDesk.Tests.Auth;

namespace RepairDesk.Tests.Assistente;

/// <summary>
/// Sprint 637 (Doc 98 F8): API do assistente — a chave «assistente» age em nome de quem a criou; cria tarefas,
/// marca a rotina por título aproximado e desfaz. Outras chaves não entram.
/// </summary>
public class AssistenteApiTests : IClassFixture<RepairDeskApiFactory>
{
    private readonly RepairDeskApiFactory _factory;
    public AssistenteApiTests(RepairDeskApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Assistente_CriaTarefa_MarcaRotina_EDesfaz()
    {
        var admin = await Admin();
        var item = await (await admin.PostAsJsonAsync("/api/rotina/itens", new GuardarRotinaItemRequest("Almoço assistente", null, RotinaTipo.Refeicao, "00:03", 0, 127, null, null, null, false, false)))
            .Content.ReadFromJsonAsync<RotinaItemDto>();
        var bot = await Chave(admin, "assistente");

        var agora = await bot.GetAsync("/api/external/assistente/agora");
        agora.StatusCode.Should().Be(HttpStatusCode.OK, await agora.Content.ReadAsStringAsync());

        var t = (await (await bot.PostAsJsonAsync("/api/external/assistente/tarefas", new AssistenteTarefaRequest("Promover publicações", "semana", null, null)))
            .Content.ReadFromJsonAsync<AssistenteResultado>())!;
        t.DesfazerId.Should().NotBeNull();
        var tarefas = await admin.GetFromJsonAsync<List<InternalTaskDto>>("/api/internal-tasks");
        tarefas!.Should().Contain(x => x.Title == "Promover publicações" && x.Foco == TarefaFoco.Semana);

        var r = (await (await bot.PostAsJsonAsync("/api/external/assistente/rotina", new AssistenteRotinaRequest("almoco assistente", null, null, null, null, null)))
            .Content.ReadFromJsonAsync<AssistenteResultado>())!;
        r.Resumo.Should().Contain("feito");
        var dia = await admin.GetFromJsonAsync<RotinaDiaDto>("/api/rotina/dia");
        dia!.Itens.Single(l => l.Item.Id == item!.Id).Estado.Should().Be(RotinaRegistoEstado.Feito);

        (await bot.PostAsync($"/api/external/assistente/desfazer/{r.DesfazerId}", null)).EnsureSuccessStatusCode();
        dia = await admin.GetFromJsonAsync<RotinaDiaDto>("/api/rotina/dia");
        dia!.Itens.Single(l => l.Item.Id == item!.Id).Estado.Should().BeNull();
        (await bot.PostAsync($"/api/external/assistente/desfazer/{t.DesfazerId}", null)).EnsureSuccessStatusCode();
        (await admin.GetFromJsonAsync<List<InternalTaskDto>>("/api/internal-tasks"))!.Should().NotContain(x => x.Title == "Promover publicações");

        var outra = await Chave(admin, "conteudo");
        (await outra.GetAsync("/api/external/assistente/agora")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private async Task<HttpClient> Chave(HttpClient admin, string scope)
    {
        var resp = await admin.PostAsJsonAsync("/api/service-keys", new CreateServiceApiKeyRequest($"chave {scope} {Guid.NewGuid():N}", new[] { scope }));
        resp.EnsureSuccessStatusCode();
        var created = (await resp.Content.ReadFromJsonAsync<CreateServiceApiKeyResponse>())!;
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", created.PlainKey);
        return client;
    }

    private async Task<HttpClient> Admin()
    {
        var client = _factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(RepairDeskApiFactory.AdminEmail, RepairDeskApiFactory.AdminPassword));
        login.EnsureSuccessStatusCode();
        var auth = (await login.Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }
}
