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

/// <summary>Sprint 650 (Doc 99): cobranças — entregue é pago por omissão; «por receber» até o cliente pagar.</summary>
public class FinancasCobrancasTests : IClassFixture<RepairDeskApiFactory>
{
    private readonly RepairDeskApiFactory _factory;
    public FinancasCobrancasTests(RepairDeskApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Cobrancas_PorReceberAteSerPaga_ContaNoMesDoPagamento()
    {
        var c = await Admin();
        var agora = DateTime.UtcNow;
        async Task<FinancasCobrancasDto> Cobrancas()
            => (await c.GetFromJsonAsync<FinancasMesDto>($"/api/financas/mes?ano={agora.Year}&mes={agora.Month}"))!.Cobrancas!;

        var antes = await Cobrancas();
        var venda = (await (await c.PostAsJsonAsync("/api/vendas", new VendaWriteRequest(VendaTipo.Produto, null, null, null, null,
            [new VendaLinhaWriteRequest(null, null, "Reparação iPad", 1, 5000)], VendaEstado.Orcamento))).Content.ReadFromJsonAsync<VendaDto>())!;

        // Por receber só faz sentido depois de entregue.
        (await c.PutAsJsonAsync($"/api/vendas/{venda.Id}/pagamento", new PagamentoVendaRequest(true))).StatusCode.Should().Be(HttpStatusCode.Conflict);

        // Entregue, mas o cliente paga depois.
        var entregue = (await (await c.PostAsJsonAsync($"/api/vendas/{venda.Id}/estado",
            new MudarEstadoVendaRequest(VendaEstado.Entregue, PaymentMethod.Outro, PorReceber: true))).Content.ReadFromJsonAsync<VendaDto>())!;
        entregue.PorReceber.Should().BeTrue();
        var valor = entregue.TotalCents / 100m;

        var depois = await Cobrancas();
        depois.PorReceberMes.Should().Be(antes.PorReceberMes + valor);
        depois.PorReceberTotal.Should().Be(antes.PorReceberTotal + valor);
        depois.RecebidoMes.Should().Be(antes.RecebidoMes);
        depois.Devedores.Should().Contain(d => d.VendaId == venda.Id && d.Valor == valor);
        (await c.GetFromJsonAsync<PagedResult<VendaDto>>("/api/vendas?porPagar=true&pageSize=200"))!.Items.Should().Contain(v => v.Id == venda.Id);

        // Pagou: sai dos devedores e entra no recebido do mês. Repetir não muda nada.
        var paga = (await (await c.PutAsJsonAsync($"/api/vendas/{venda.Id}/pagamento", new PagamentoVendaRequest(false))).Content.ReadFromJsonAsync<VendaDto>())!;
        paga.PorReceber.Should().BeFalse();
        paga.RecebidoEm.Should().NotBeNull();
        (await c.PutAsJsonAsync($"/api/vendas/{venda.Id}/pagamento", new PagamentoVendaRequest(false))).EnsureSuccessStatusCode();

        var fim = await Cobrancas();
        fim.Devedores.Should().NotContain(d => d.VendaId == venda.Id);
        fim.PorReceberTotal.Should().Be(antes.PorReceberTotal);
        fim.RecebidoMes.Should().Be(antes.RecebidoMes + valor);
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
