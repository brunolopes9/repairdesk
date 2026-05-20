using FluentAssertions;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;
using RepairDesk.Services.Vendas;

namespace RepairDesk.Tests.Vendas;

/// <summary>
/// Sprint 127: resolução do período de garantia DL 84/2021 a partir de
/// <see cref="CondicaoArtigo"/> dos items + defaults do tenant. Critério: Max() — favorável
/// ao consumidor, sempre conforme com o mínimo legal.
/// </summary>
public class GarantiaPorCondicaoTests
{
    [Fact]
    public void DiasParaCondicao_RouteByEnum()
    {
        VendaService.DiasParaCondicao(CondicaoArtigo.Novo, 1095, 730, 540, 540).Should().Be(1095);
        VendaService.DiasParaCondicao(CondicaoArtigo.OpenBox, 1095, 730, 540, 540).Should().Be(730);
        VendaService.DiasParaCondicao(CondicaoArtigo.Recondicionado, 1095, 730, 540, 540).Should().Be(540);
        VendaService.DiasParaCondicao(CondicaoArtigo.Usado, 1095, 730, 540, 540).Should().Be(540);
        VendaService.DiasParaCondicao(CondicaoArtigo.NaoAplicavel, 1095, 730, 540, 540).Should().Be(1095);
    }

    [Fact]
    public void Resolve_AllNovo_ReturnsTresAnos()
    {
        var venda = MakeVenda(CondicaoArtigo.Novo, CondicaoArtigo.Novo);
        var tenant = MakeTenant();
        VendaService.ResolveGarantiaDiasFromItems(venda, tenant).Should().Be(1095);
    }

    [Fact]
    public void Resolve_AllRecondicionado_Returns18Meses()
    {
        var venda = MakeVenda(CondicaoArtigo.Recondicionado, CondicaoArtigo.Recondicionado);
        var tenant = MakeTenant();
        VendaService.ResolveGarantiaDiasFromItems(venda, tenant).Should().Be(540);
    }

    [Fact]
    public void Resolve_MixedNovoEUsado_PicksMaxNovo()
    {
        // Critério favorável ao cliente: usado dá direito a 18m, mas o cliente que compra
        // num combo com algo novo beneficia da cobertura do Max → 3 anos.
        var venda = MakeVenda(CondicaoArtigo.Novo, CondicaoArtigo.Usado);
        var tenant = MakeTenant();
        VendaService.ResolveGarantiaDiasFromItems(venda, tenant).Should().Be(1095);
    }

    [Fact]
    public void Resolve_EmptyItems_FallsBackToDefault()
    {
        var venda = new Venda { TenantId = Guid.NewGuid(), Numero = 1, ClienteId = null };
        var tenant = MakeTenant();
        VendaService.ResolveGarantiaDiasFromItems(venda, tenant).Should().Be(1095);
    }

    [Fact]
    public void Resolve_NullTenant_UsesLegalDefaults()
    {
        // Defaults internos da função: 1095/730/540/540 (DL 84/2021 mínimo legal).
        var venda = MakeVenda(CondicaoArtigo.Recondicionado);
        VendaService.ResolveGarantiaDiasFromItems(venda, tenant: null).Should().Be(540);
    }

    [Fact]
    public void ResolveCondicaoDominante_PicksCondicaoQueGeraMaiorPeriodo()
    {
        // Mixed Novo + Recondicionado → dominante é Novo (3 anos > 18m).
        var venda = MakeVenda(CondicaoArtigo.Recondicionado, CondicaoArtigo.Novo);
        var tenant = MakeTenant();
        VendaService.ResolveCondicaoDominante(venda, tenant).Should().Be(CondicaoArtigo.Novo);
    }

    [Fact]
    public void ResolveCondicaoDominante_AllRefurbished_ReturnsRecondicionado()
    {
        var venda = MakeVenda(CondicaoArtigo.Recondicionado, CondicaoArtigo.Recondicionado);
        var tenant = MakeTenant();
        VendaService.ResolveCondicaoDominante(venda, tenant).Should().Be(CondicaoArtigo.Recondicionado);
    }

    [Fact]
    public void ResolveCondicaoDominante_EmptyItems_NaoAplicavel()
    {
        var venda = new Venda { TenantId = Guid.NewGuid(), Numero = 1, ClienteId = null };
        VendaService.ResolveCondicaoDominante(venda, tenant: null).Should().Be(CondicaoArtigo.NaoAplicavel);
    }

    [Fact]
    public void Resolve_TenantWithCustomRecondicionado_HonoursOverride()
    {
        // Política comercial LopesTech: 3 anos para tudo (refurbished incluído).
        var venda = MakeVenda(CondicaoArtigo.Recondicionado, CondicaoArtigo.Recondicionado);
        var tenant = MakeTenant(recondicionado: 1095);
        VendaService.ResolveGarantiaDiasFromItems(venda, tenant).Should().Be(1095);
    }

    private static Venda MakeVenda(params CondicaoArtigo[] condicoes)
    {
        var venda = new Venda { TenantId = Guid.NewGuid(), Numero = 1, ClienteId = null };
        foreach (var c in condicoes)
            venda.Items.Add(new VendaItem
            {
                TenantId = venda.TenantId,
                Descricao = $"Item {c}",
                Quantidade = 1,
                PrecoUnitarioCents = 10000,
                Condicao = c,
            });
        return venda;
    }

    private static Tenant MakeTenant(int novo = 1095, int openBox = 730, int recondicionado = 540, int usado = 540) =>
        new()
        {
            Name = "Test Tenant",
            GarantiaVendaDiasDefault = novo,
            GarantiaVendaOpenBoxDias = openBox,
            GarantiaVendaRecondicionadoDias = recondicionado,
            GarantiaVendaUsadoDias = usado,
        };
}
