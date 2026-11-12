using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using RepairDesk.API.Infrastructure;
using RepairDesk.Services.Financas;
using RepairDesk.Tests.Auth;

namespace RepairDesk.Tests.Financas;

/// <summary>Sprint 634: o Excel real de outubro (só na máquina do Bruno — dados pessoais fora do repositório).</summary>
public class FinancasImportDiagTests : IClassFixture<RepairDeskApiFactory>
{
    private const string Ficheiro = @"C:\Users\Utilizador\Documents\Gestão Financeira\Outubro\Gestao_Financeira_OUTUBRO_2026 (atualizado 01-11).xlsx";
    private readonly RepairDeskApiFactory _factory;
    public FinancasImportDiagTests(RepairDeskApiFactory factory) => _factory = factory;

    [Fact]
    public async Task ExcelDeOutubro_ImportaAsPessoais()
    {
        if (!File.Exists(Ficheiro)) return;
        var c = _factory.CreateClient();
        var login = await c.PostAsJsonAsync("/api/auth/login", new LoginRequest(RepairDeskApiFactory.AdminEmail, RepairDeskApiFactory.AdminPassword));
        var auth = (await login.Content.ReadFromJsonAsync<AuthResponse>())!;
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(await File.ReadAllBytesAsync(Ficheiro)), "file", "o.xlsx");
        var r = await c.PostAsync("/api/financas/importar-excel", form);
        var corpo = await r.Content.ReadAsStringAsync();
        r.IsSuccessStatusCode.Should().BeTrue(corpo);
        var res = (await r.Content.ReadFromJsonAsync<FinancasImportResultado>())!;
        (res.Importadas + res.JaExistiam).Should().Be(41, corpo);
        var m = (await c.GetFromJsonAsync<FinancasMesDto>("/api/financas/mes?ano=2026&mes=10"))!;
        (m.DespesasPessoais + m.Obrigacoes).Should().Be(606.87m, corpo);
    }
}
