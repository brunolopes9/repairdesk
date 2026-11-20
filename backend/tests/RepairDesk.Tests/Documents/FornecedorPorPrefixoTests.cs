using FluentAssertions;
using RepairDesk.Core.Entities;
using RepairDesk.Services.Documents;

namespace RepairDesk.Tests.Documents;

/// <summary>20/11: o nome completo da fatura («TUDO4MOBILE - Import. Export. Unip. Lda.») liga ao fornecedor «Tudo4Mobile».</summary>
public class FornecedorPorPrefixoTests
{
    private static Fornecedor F(string n) => new() { Name = n };

    [Fact]
    public void NomeCompletoDaFatura_LigaAoFornecedorCurto()
    {
        var lista = new[] { F("4Phones"), F("Tudo4Mobile"), F("MobileSentrix") };
        SupplierInvoiceImportService.FornecedorPorPrefixo(lista, "TUDO4MOBILE - Import. Export. Unip.Lda.")!.Name.Should().Be("Tudo4Mobile");
    }

    [Fact]
    public void EscolheOMaisLongo_E_IgnoraNomesCurtos()
    {
        var lista = new[] { F("MRC"), F("Amazon"), F("Amazon.es (vendido por x)") };
        SupplierInvoiceImportService.FornecedorPorPrefixo(lista, "MRC Informática").Should().BeNull("«MRC» tem menos de 5 letras");
        SupplierInvoiceImportService.FornecedorPorPrefixo(lista, "Amazon EU S.a.r.l.")!.Name.Should().Be("Amazon");
        SupplierInvoiceImportService.FornecedorPorPrefixo(lista, "Loja Nova").Should().BeNull();
    }
}
