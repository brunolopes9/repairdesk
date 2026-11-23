using System.Text.Json;
using FluentAssertions;
using RepairDesk.Services.Loja;

namespace RepairDesk.Tests.Loja;

/// <summary>23/11: MB WAY e Multibanco pagos dentro do site (APIs diretas do ifthenpay).</summary>
public class PagamentoNoSiteTests
{
    [Theory]
    [InlineData("912 345 678", "351#912345678")]
    [InlineData("+351 912345678", "351#912345678")]
    [InlineData("00351912345678", "351#912345678")]
    [InlineData("351912345678", "351#912345678")]
    [InlineData("232 123 456", null)]
    [InlineData("91234567", null)]
    [InlineData(null, null)]
    public void TelefoneMbWay_SoTelemoveisPortugueses(string? entrada, string? esperado)
        => PagamentoNoSiteIfthenpay.TelefoneMbWay(entrada).Should().Be(esperado);

    [Theory]
    [InlineData("000", EstadoPagamentoNoSite.Pago)]
    [InlineData("020", EstadoPagamentoNoSite.Recusado)]
    [InlineData("122", EstadoPagamentoNoSite.Recusado)]
    [InlineData("101", EstadoPagamentoNoSite.Expirado)]
    [InlineData("123", EstadoPagamentoNoSite.Pendente)]
    [InlineData(null, EstadoPagamentoNoSite.Pendente)]
    public void EstadoMbWay_CodigosDoIfthenpay(string? status, EstadoPagamentoNoSite esperado)
        => PagamentoNoSiteIfthenpay.EstadoMbWay(status).Should().Be(esperado);

    private static JsonElement Json(string s) => JsonDocument.Parse(s).RootElement;

    [Fact]
    public void Multibanco_PagaSoComPaidEValorCerto()
    {
        var agora = new DateTime(2026, 11, 24, 10, 0, 0, DateTimeKind.Utc);
        PagamentoNoSiteIfthenpay.EstadoMultibanco(Json("""[{"status":"PAID","amount":"14.80"}]"""), 1480, agora.AddDays(2), agora)
            .Should().Be(EstadoPagamentoNoSite.Pago);
        PagamentoNoSiteIfthenpay.EstadoMultibanco(Json("""[{"status":"PAID","amount":"10.00"}]"""), 1480, agora.AddDays(2), agora)
            .Should().Be(EstadoPagamentoNoSite.Pendente, "valor diferente nunca confirma");
        PagamentoNoSiteIfthenpay.EstadoMultibanco(Json("""[{"status":"NOT_PAID","amount":"14.80"}]"""), 1480, agora.AddDays(2), agora)
            .Should().Be(EstadoPagamentoNoSite.Pendente);
    }

    [Fact]
    public void Multibanco_SemPagamentoDepoisDaValidade_Expira()
    {
        var agora = new DateTime(2026, 11, 28, 10, 0, 0, DateTimeKind.Utc);
        PagamentoNoSiteIfthenpay.EstadoMultibanco(Json("[]"), 1480, agora.AddDays(-1), agora).Should().Be(EstadoPagamentoNoSite.Expirado);
    }

    [Theory]
    [InlineData("544241017", "544 241 017")]
    [InlineData("12345", "12345")]
    [InlineData(null, null)]
    public void Referencia_Agrupada3a3(string? r, string? esperado)
        => PagamentoNoSiteIfthenpay.FormatarReferencia(r).Should().Be(esperado);
}
