using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using RepairDesk.API.Infrastructure;
using RepairDesk.Services.Financas;
using RepairDesk.Tests.Auth;

namespace RepairDesk.Tests.Financas;

/// <summary>
/// Sprint 634: importação do Excel real do Bruno. Só corre na máquina dele (o ficheiro tem dados pessoais e não vai
/// para o repositório); noutro sítio passa sem fazer nada.
/// </summary>
public class FinancasImportLocalTests : IClassFixture<RepairDeskApiFactory>
{
    private const string Ficheiro = @"C:\Users\Utilizador\Documents\Gestão Financeira\Setembro\Gestao_Financeira_v10.xlsx";
    private readonly RepairDeskApiFactory _factory;
    public FinancasImportLocalTests(RepairDeskApiFactory factory) => _factory = factory;

    [Fact]
    public async Task ExcelDeSetembro_Importa27_ESegundaVezNaoDuplica()
    {
        if (!File.Exists(Ficheiro)) return;
        var c = _factory.CreateClient();
        var login = await c.PostAsJsonAsync("/api/auth/login", new LoginRequest(RepairDeskApiFactory.AdminEmail, RepairDeskApiFactory.AdminPassword));
        var auth = (await login.Content.ReadFromJsonAsync<AuthResponse>())!;
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        async Task<FinancasImportResultado> Importar()
        {
            using var form = new MultipartFormDataContent();
            form.Add(new ByteArrayContent(await File.ReadAllBytesAsync(Ficheiro)), "file", "g.xlsx");
            var r = await c.PostAsync("/api/financas/importar-excel", form);
            r.EnsureSuccessStatusCode();
            return (await r.Content.ReadFromJsonAsync<FinancasImportResultado>())!;
        }

        var primeira = await Importar();
        primeira.Importadas.Should().Be(27);
        primeira.Ignoradas.Should().Be(0);
        var segunda = await Importar();
        segunda.Importadas.Should().Be(0);
        segunda.JaExistiam.Should().Be(27);

        var m = (await c.GetFromJsonAsync<FinancasMesDto>("/api/financas/mes?ano=2026&mes=9"))!;
        m.DespesasTrading.Should().Be(729m);
        m.ImpostosPagos.Should().Be(240m);
        (m.DespesasPessoais + m.Obrigacoes).Should().Be(1336m);
        m.DespesasGerais.Should().Be(131.5m);
    }
}
