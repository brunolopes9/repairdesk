using FluentAssertions;
using RepairDesk.Services.Documents;

namespace RepairDesk.Tests.Common;

public class SupplierInvoiceItemClassifierTests
{
    [Theory]
    [InlineData("Portes de envio", null, SupplierItemKind.Shipping)]
    [InlineData("Serviço de garantia premium", null, SupplierItemKind.Service)]
    [InlineData("Samsung Galaxy A15 128GB Black", null, SupplierItemKind.Phone)]
    [InlineData("Bateria iPhone 12", null, SupplierItemKind.Part)]
    [InlineData("Consumivel loja diverso", null, SupplierItemKind.Unknown)]
    [InlineData("touch + display iPhone 13", null, SupplierItemKind.Part)]
    [InlineData("iPhone 13 128GB", null, SupplierItemKind.Phone)]
    [InlineData("Carregador USB-C 20W", 2990, SupplierItemKind.Part)]
    public void ClassifyItemDescription_ReturnsExpectedKind(
        string description,
        int? unitCostCents,
        SupplierItemKind expected)
    {
        SupplierInvoiceImportService
            .ClassifyItemDescription(description, unitCostCents)
            .Should()
            .Be(expected);
    }
}
