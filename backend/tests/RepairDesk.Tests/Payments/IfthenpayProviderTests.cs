using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Enums;
using RepairDesk.Services.Payments;
using RepairDesk.Services.Payments.Ifthenpay;

namespace RepairDesk.Tests.Payments;

public class IfthenpayProviderTests
{
    private static IfthenpayOptions Opts() => new()
    {
        MBWayKey = "MBW-TEST-KEY",
        MultibancoKey = "MB-TEST-KEY",
        AntiPhishingKey = "anti-phish-secret",
        BaseUrl = "https://api.ifthenpay.test",
    };

    private static PaymentInitiationRequest Req(PaymentMethod method, string? phone = "912345678") =>
        new(TenantId: Guid.NewGuid(), VendaId: Guid.NewGuid(), Method: method, AmountCents: 1500, CustomerPhone: phone);

    private static IfthenpayProvider MakeProvider(string responseJson, HttpStatusCode code = HttpStatusCode.OK)
    {
        var handler = new StubHandler(responseJson, code);
        var client = new HttpClient(handler);
        return new IfthenpayProvider(client, Opts(), NullLogger<IfthenpayProvider>.Instance);
    }

    [Fact]
    public async Task MbWay_Success_ReturnsPendingWithRequestId()
    {
        var sut = MakeProvider("""{"Status":"000","Message":"OK","RequestId":"REQ-123"}""");
        var result = await sut.InitiateAsync(Req(PaymentMethod.MBWay));

        result.Status.Should().Be(PaymentStatus.NaoPago);
        result.ProviderRef.Should().Be("REQ-123");
        result.ExpiresAt.Should().NotBeNull();
        result.CustomerInstructions.Should().Contain("MBWay");
    }

    [Fact]
    public async Task MbWay_Rejected_ReturnsNaoPagoWithMessage()
    {
        var sut = MakeProvider("""{"Status":"001","Message":"Telemóvel inválido"}""");
        var result = await sut.InitiateAsync(Req(PaymentMethod.MBWay));

        result.Status.Should().Be(PaymentStatus.NaoPago);
        result.ProviderRef.Should().BeNull();
        result.CustomerInstructions.Should().Contain("Telemóvel inválido");
    }

    [Fact]
    public async Task MbWay_MissingPhone_Throws()
    {
        var sut = MakeProvider("{}");
        await FluentActions.Awaiting(() => sut.InitiateAsync(Req(PaymentMethod.MBWay, phone: null)))
            .Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Multibanco_Success_ReturnsEntidadeReferencia()
    {
        var sut = MakeProvider("""{"Status":"0","Entidade":"12345","Referencia":"123456789"}""");
        var result = await sut.InitiateAsync(Req(PaymentMethod.Multibanco));

        result.Status.Should().Be(PaymentStatus.NaoPago);
        result.ProviderRef.Should().Be("12345-123456789");
        result.CustomerInstructions.Should().Contain("12345").And.Contain("123456789");
    }

    [Fact]
    public async Task UnsupportedMethod_Throws()
    {
        var sut = MakeProvider("{}");
        await FluentActions.Awaiting(() => sut.InitiateAsync(Req(PaymentMethod.Dinheiro)))
            .Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task CheckStatusAsync_ReturnsNaoPagoAlways()
    {
        var sut = MakeProvider("{}");
        var snap = await sut.CheckStatusAsync("any-ref");
        snap.Status.Should().Be(PaymentStatus.NaoPago);
    }

    private sealed class StubHandler(string body, HttpStatusCode code) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(code)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            });
    }
}
