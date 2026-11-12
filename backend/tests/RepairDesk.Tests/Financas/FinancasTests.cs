using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using RepairDesk.API.Infrastructure;
using RepairDesk.Core.Enums;
using RepairDesk.Services.Clientes;
using RepairDesk.Services.Despesas;
using RepairDesk.Services.Financas;
using RepairDesk.Services.Vendas;
using RepairDesk.Tests.Auth;

namespace RepairDesk.Tests.Financas;

/// <summary>
/// Sprint 633 (Doc 99): o Painel de finanças do dono — lucro da loja (com ou sem fatura), despesas por âmbito e
/// afetação, IVA pago fora do saldo, pessoais só para o Admin e fora do apuramento.
/// </summary>
public class FinancasTests : IClassFixture<RepairDeskApiFactory>
{
    private readonly RepairDeskApiFactory _factory;
    public FinancasTests(RepairDeskApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Painel_SaldoContaPessoaisESs_IvaPagoFica_Fora()
    {
        var c = await Admin();
        var agora = DateTime.UtcNow;
        var venda = await (await c.PostAsJsonAsync("/api/vendas", new VendaWriteRequest(VendaTipo.Produto, null, null, null, null,
            [new VendaLinhaWriteRequest(null, null, "Limpeza PS5", 1, 4000)], VendaEstado.Entregue))).Content.ReadFromJsonAsync<VendaDto>();
        (await c.PutAsJsonAsync($"/api/vendas/{venda!.Id}/fora-apuramento", new ForaApuramentoRequest(true, null))).EnsureSuccessStatusCode();

        async Task Despesa(string desc, DespesaCategoria cat, int cents, DespesaAmbito ambito, AfetacaoIva afet = AfetacaoIva.LopesTech, DespesaNatureza nat = DespesaNatureza.Gasto)
            => (await c.PostAsJsonAsync("/api/despesas", new CreateDespesaRequest(desc, cat, cents, agora, null, null, null,
                Afetacao: afet, Ambito: ambito, Natureza: nat))).EnsureSuccessStatusCode();
        await Despesa("Trade Copier", DespesaCategoria.VpsCopier, 4500, DespesaAmbito.Negocio, AfetacaoIva.Trading, DespesaNatureza.Investimento);
        await Despesa("Supermercado", DespesaCategoria.Alimentacao, 2000, DespesaAmbito.Pessoal);
        await Despesa("Segurança Social", DespesaCategoria.SegurancaSocial, 11794, DespesaAmbito.Obrigacao);
        await Despesa("IVA 3.º trimestre", DespesaCategoria.PagamentoIva, 24000, DespesaAmbito.Obrigacao);

        var m = (await c.GetFromJsonAsync<FinancasMesDto>($"/api/financas/mes?ano={agora.Year}&mes={agora.Month}"))!;
        m.LucroInformatica.Should().Be(40m);
        m.DespesasTrading.Should().Be(45m);
        m.DespesasPessoais.Should().Be(20m);
        m.Obrigacoes.Should().Be(117.94m);
        m.ImpostosPagos.Should().Be(240m);
        m.Saldo.Should().Be(40m - 45m - 20m - 117.94m);
        m.Investimento.Should().Be(45m);
        m.VendasSemFatura.Should().Be(1);
        m.Objetivo.Should().Be(1500m);

        // As pessoais não aparecem a quem só pode ver as do negócio (filtro explícito também funciona para o Admin).
        var soNegocio = await c.GetFromJsonAsync<PagedResult<DespesaDto>>("/api/despesas?ambito=Negocio&pageSize=100");
        soNegocio!.Items.Should().NotContain(d => d.Ambito != DespesaAmbito.Negocio);

        (await c.PutAsJsonAsync("/api/financas/objetivo", new ObjetivoRequestDto(2000m))).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await c.GetFromJsonAsync<FinancasMesDto>($"/api/financas/mes?ano={agora.Year}&mes={agora.Month}"))!.Objetivo.Should().Be(2000m);
    }

    private sealed record ObjetivoRequestDto(decimal Objetivo);

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
