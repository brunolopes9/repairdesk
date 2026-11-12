using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using RepairDesk.API.Infrastructure;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;
using RepairDesk.Services.Push;
using RepairDesk.Services.Rotina;
using RepairDesk.Tests.Auth;

namespace RepairDesk.Tests.Rotina;

/// <summary>
/// Sprint 615 (Doc 98): rotina pessoal — avisos por níveis (antes, à hora com botões, insistência),
/// idempotência, nada depois de «Feito», e as regras do registo (limite exige motivo, peso exige valor).
/// Avisos em março de 2031 (Lisboa = UTC); registos no dia de hoje (a API só aceita hoje/7 dias para trás).
/// </summary>
public class RotinaTests : IClassFixture<RepairDeskApiFactory>
{
    private const int Todos = 127;
    private readonly RepairDeskApiFactory _factory;
    public RotinaTests(RepairDeskApiFactory factory) => _factory = factory;

    [Fact]
    public async Task FechoDoPc_AvisaAntes_AHora_EInsiste_UmaVezCada()
    {
        var client = await Admin();
        var item = await Criar(client, new GuardarRotinaItemRequest("Fim do PC", "Desliga os ecrãs.", RotinaTipo.Limite, "18:00", 0, Todos,
            null, null, "5, 15", true, false));
        item.AvisosAntes.Should().Be("15,5");
        await Jobs();

        DateTime T(int h, int m) => new(2031, 3, 12, h, m, 10, DateTimeKind.Utc);
        (await Processar(T(17, 40))).Should().Be(0);

        (await Processar(T(17, 45))).Should().Be(1);
        (await Jobs()).Single().Title.Should().Contain("15 min");
        (await Processar(T(17, 46))).Should().Be(0);

        (await Processar(T(17, 55))).Should().Be(1);
        (await Jobs()).Single().Title.Should().Contain("5 min");

        (await Processar(T(18, 0))).Should().Be(1);
        var hora = (await Jobs()).Single();
        hora.Title.Should().Contain("18:00").And.Contain("Fim do PC");
        hora.UserId.Should().NotBeNull();
        hora.Insistente.Should().BeTrue();
        hora.Acoes!.Select(a => a.Action).Should().BeEquivalentTo("feito", "adiar");
        hora.Url.Should().Contain($"item={item.Id}").And.Contain("dia=2031-03-12");

        (await Processar(T(18, 15))).Should().Be(1);
        (await Jobs()).Single().Title.Should().Contain("Ainda no PC");
        (await Processar(T(18, 30))).Should().Be(1);
        (await Processar(T(18, 41))).Should().Be(0);
    }

    [Fact]
    public async Task ServidorParado_SoSaiOAvisoMaisRecente_EFeitoCalaOsAvisos()
    {
        var client = await Admin();
        var item = await Criar(client, new GuardarRotinaItemRequest("Almoço teste", "Arroz\n150 g carne", RotinaTipo.Refeicao, "13:00", 0, Todos,
            null, null, "5", true, false));
        await Jobs();

        // Às 13:16 (parado desde antes das 12:55): só a insistência dos +15, nada em rajada.
        (await Processar(new DateTime(2031, 3, 13, 13, 16, 0, DateTimeKind.Utc))).Should().Be(1);
        var j = (await Jobs()).Single(x => x.Title.Contains("Almoço teste"));
        j.Title.Should().Contain("Ainda não registaste");

        // Marcado como feito → os +30 já não saem.
        using (var scope = _factory.Services.CreateScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<IRotinaRepository>();
            var reg = await repo.FindRegistoAsync(item.Id, new DateOnly(2031, 3, 13));
            reg!.Estado = RotinaRegistoEstado.Feito;
            await repo.SaveAsync();
        }
        (await Processar(new DateTime(2031, 3, 13, 13, 30, 30, DateTimeKind.Utc))).Should().Be(0);
    }

    [Fact]
    public async Task Registo_LimiteExigeMotivo_PesoExigeValor_EODiaConta()
    {
        var client = await Admin();
        var hoje = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, RotinaCalendario.Lisboa));
        var limite = await Criar(client, new GuardarRotinaItemRequest("Fecho teste", null, RotinaTipo.Limite, "00:00", 0, Todos, null, null, null, true, false));
        var peso = await Criar(client, new GuardarRotinaItemRequest("Peso teste", null, RotinaTipo.Peso, "00:01", 0, 0, 14, hoje, null, false, false));

        (await client.PostAsJsonAsync($"/api/rotina/itens/{limite.Id}/dia/{hoje:yyyy-MM-dd}",
            new RegistarRotinaRequest(RotinaRegistoEstado.Adiado, null, null, 15))).StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var adiado = await client.PostAsJsonAsync($"/api/rotina/itens/{limite.Id}/dia/{hoje:yyyy-MM-dd}",
            new RegistarRotinaRequest(RotinaRegistoEstado.Adiado, "Cliente à espera", null, 15));
        adiado.EnsureSuccessStatusCode();
        (await adiado.Content.ReadFromJsonAsync<RotinaDiaItemDto>())!.AdiadoAte.Should().NotBeNull();

        (await client.PostAsJsonAsync($"/api/rotina/itens/{peso.Id}/dia/{hoje:yyyy-MM-dd}",
            new RegistarRotinaRequest(RotinaRegistoEstado.Feito, null, null, null))).StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await client.PostAsJsonAsync($"/api/rotina/itens/{peso.Id}/dia/{hoje:yyyy-MM-dd}",
            new RegistarRotinaRequest(RotinaRegistoEstado.Feito, null, 82.4m, null))).EnsureSuccessStatusCode();
        var valores = await client.GetFromJsonAsync<List<RotinaValorDto>>($"/api/rotina/itens/{peso.Id}/valores");
        valores!.Should().ContainSingle(v => v.Valor == 82.4m && v.Dia == hoje);

        // O peso de 14 em 14 dias não está na rotina de amanhã.
        (await client.PostAsJsonAsync($"/api/rotina/itens/{peso.Id}/dia/{hoje.AddDays(-1):yyyy-MM-dd}",
            new RegistarRotinaRequest(RotinaRegistoEstado.Feito, null, 80m, null))).StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        var dia = await client.GetFromJsonAsync<RotinaDiaDto>("/api/rotina/dia");
        dia!.Itens.Should().Contain(l => l.Item.Id == peso.Id && l.Estado == RotinaRegistoEstado.Feito);
        dia.Feitos.Should().BeGreaterThanOrEqualTo(1);

        (await client.PostAsJsonAsync("/api/rotina/itens", new GuardarRotinaItemRequest("Hora má", null, RotinaTipo.Pessoal, "25:00", 0, Todos, null, null, null, false, false)))
            .StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Importar_TudoOuNada()
    {
        var client = await Admin();
        var antes = (await client.GetFromJsonAsync<List<RotinaItemDto>>("/api/rotina/itens"))!.Count;
        var ok = new GuardarRotinaItemRequest("Import A", "Banana\nCafé", RotinaTipo.Refeicao, "08:00", 0, Todos, null, null, null, true, false);
        var mau = ok with { Titulo = "Import B", Hora = "8h" };

        var falha = await client.PostAsJsonAsync("/api/rotina/importar", new[] { ok, mau });
        falha.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await falha.Content.ReadAsStringAsync()).Should().Contain("Item 2");
        (await client.GetFromJsonAsync<List<RotinaItemDto>>("/api/rotina/itens"))!.Count.Should().Be(antes);

        (await client.PostAsJsonAsync("/api/rotina/importar", new[] { ok, mau with { Hora = "08:30" } })).EnsureSuccessStatusCode();
        (await client.GetFromJsonAsync<List<RotinaItemDto>>("/api/rotina/itens"))!.Count.Should().Be(antes + 2);
    }

    [Fact]
    public async Task Alimentacao_MacrosPorRefeicao_PlaneadoEComido()
    {
        var client = await Admin();
        var hoje = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, RotinaCalendario.Lisboa));
        var almoco = await Criar(client, new GuardarRotinaItemRequest("Almoço macros", null, RotinaTipo.Refeicao, "00:02", 0, Todos, null, null, null, false, false));
        var r = await client.PostAsJsonAsync("/api/rotina/alimentos/importar", new[]
        {
            new ImportarAlimentosRequest("Almoço macros", new[]
            {
                new RotinaAlimentoDto("Arroz", "150 g", 195, 4, 42, 0.5m),
                new RotinaAlimentoDto("Frango", "175 g", 290, 52, 0, 7),
            }),
        });
        r.EnsureSuccessStatusCode();
        var dia = await client.GetFromJsonAsync<RotinaDiaDto>("/api/rotina/dia");
        dia!.Itens.Single(l => l.Item.Id == almoco.Id).Item.Macros!.Kcal.Should().Be(485);
        dia.MacrosComidos!.Kcal.Should().Be(0);
        (await client.PostAsJsonAsync($"/api/rotina/itens/{almoco.Id}/dia/{hoje:yyyy-MM-dd}", new RegistarRotinaRequest(RotinaRegistoEstado.Feito, null, null, null))).EnsureSuccessStatusCode();
        dia = await client.GetFromJsonAsync<RotinaDiaDto>("/api/rotina/dia");
        dia!.MacrosComidos!.ProteinaG.Should().Be(56);
        (await client.PostAsJsonAsync("/api/rotina/alimentos/importar", new[] { new ImportarAlimentosRequest("Não existe", Array.Empty<RotinaAlimentoDto>()) }))
            .StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    private async Task<int> Processar(DateTime agoraUtc)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IRotinaAvisosService>().ProcessarAsync(agoraUtc);
    }

    private async Task<List<StaffPushJob>> Jobs()
    {
        var fila = _factory.Services.GetRequiredService<IStaffPushQueue>();
        var jobs = new List<StaffPushJob>();
        while (true)
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(150));
            try { jobs.Add(await fila.DequeueAsync(cts.Token)); }
            catch (OperationCanceledException) { return jobs; }
        }
    }

    private static async Task<RotinaItemDto> Criar(HttpClient client, GuardarRotinaItemRequest req)
    {
        var resp = await client.PostAsJsonAsync("/api/rotina/itens", req);
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<RotinaItemDto>())!;
    }

    private async Task<HttpClient> Admin()
    {
        var client = _factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(RepairDeskApiFactory.AdminEmail, RepairDeskApiFactory.AdminPassword));
        login.EnsureSuccessStatusCode();
        var auth = (await login.Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }
}
