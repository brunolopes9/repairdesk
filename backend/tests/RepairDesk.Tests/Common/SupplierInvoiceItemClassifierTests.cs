using FluentAssertions;
using RepairDesk.Services.Documents;

namespace RepairDesk.Tests.Common;

public class SupplierInvoiceItemClassifierTests
{
    [Theory]
    [InlineData("Portes de envio", null, SupplierItemKind.Shipping)]
    [InlineData("Serviço de garantia premium", null, SupplierItemKind.Service)]
    // Telemóveis COMPLETOS (modelo + capacidade GB/TB) → revenda, não stock de peças.
    [InlineData("Samsung Galaxy A15 128GB Black", null, SupplierItemKind.Phone)]
    [InlineData("iPhone 13 128GB", null, SupplierItemKind.Phone)]
    [InlineData("Bateria iPhone 12", null, SupplierItemKind.Part)]
    [InlineData("touch + display iPhone 13", null, SupplierItemKind.Part)]
    [InlineData("Carregador USB-C 20W", 2990, SupplierItemKind.Part)]
    // Sprint 521/522: o bug do Bruno — ecrã com nome de modelo (A15/A155) era classificado como
    // TELEMÓVEL e ia para Despesa. Tem de ser Peça → Stock.
    [InlineData("Touch+Display+Frame Samsung Galaxy A15 4G/A155/A15 5G/A156 Service Pack Black", null, SupplierItemKind.Part)]
    // Sprint 522: tipos de peça que o Bruno compra muito — todos têm de ir para Peça (Stock).
    [InlineData("Chassis Samsung Galaxy A15", null, SupplierItemKind.Part)]
    [InlineData("Housing back cover iPhone 14", null, SupplierItemKind.Part)]
    [InlineData("Pelicula vidro temperado Redmi Note 13", null, SupplierItemKind.Part)]
    [InlineData("Capa silicone iPhone 15 Pro", null, SupplierItemKind.Part)]
    // Sprint 522: DEFAULT ROBUSTO — o que não é portes/serviço/telemóvel numa fatura de fornecedor = Peça.
    [InlineData("Consumivel loja diverso", null, SupplierItemKind.Part)]
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
