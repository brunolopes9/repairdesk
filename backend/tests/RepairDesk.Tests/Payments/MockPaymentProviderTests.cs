using FluentAssertions;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Enums;
using RepairDesk.Services.Payments;

namespace RepairDesk.Tests.Payments;

public class MockPaymentProviderTests
{
    private static PaymentInitiationRequest Req(PaymentMethod method = PaymentMethod.MBWay, int amount = 1500) =>
        new(TenantId: Guid.NewGuid(), VendaId: Guid.NewGuid(), Method: method, AmountCents: amount);

    [Fact]
    public void Provider_IsMock()
        => new MockPaymentProvider().Provider.Should().Be(PaymentProvider.Mock);

    [Fact]
    public void SupportedMethods_ContainsMBWayAndMultibanco()
    {
        var sut = new MockPaymentProvider();
        sut.SupportedMethods.Should().Contain(PaymentMethod.MBWay);
        sut.SupportedMethods.Should().Contain(PaymentMethod.Multibanco);
    }

    [Fact]
    public async Task InitiateAsync_ReturnsPagoImmediately()
    {
        var sut = new MockPaymentProvider();
        var result = await sut.InitiateAsync(Req());

        result.Status.Should().Be(PaymentStatus.Pago);
        result.ProviderRef.Should().NotBeNullOrWhiteSpace();
        result.ExternalId.Should().Be(result.ProviderRef);
        result.MetadataJson.Should().Contain("\"mock\":true");
    }

    [Fact]
    public async Task InitiateAsync_GeneratesUniqueProviderRefPerCall()
    {
        var sut = new MockPaymentProvider();
        var a = await sut.InitiateAsync(Req());
        var b = await sut.InitiateAsync(Req());

        a.ProviderRef.Should().NotBe(b.ProviderRef);
    }

    [Fact]
    public async Task CheckStatusAsync_AlwaysReturnsPago()
    {
        var sut = new MockPaymentProvider();
        var snap = await sut.CheckStatusAsync("any-ref");

        snap.Status.Should().Be(PaymentStatus.Pago);
        snap.ConfirmedAt.Should().NotBeNull();
        snap.FailureReason.Should().BeNull();
    }
}
