using FluentAssertions;
using RepairDesk.Services.Documents;

namespace RepairDesk.Tests.Common;

/// <summary>
/// Sprint 526: ao auto-criar fornecedores a partir de imports, os fornecedores intra-UE conhecidos
/// (Utopya/FR, Molano/NL) têm de ser detetados para marcar IntraUe por defeito — caso contrário o
/// IVA reverse-charge nunca seria carimbado e o crédito de IVA continuaria inflado.
/// </summary>
public class IntraUeSuppliersTests
{
    [Theory]
    [InlineData("Utopya", true)]
    [InlineData("UTOPYA SAS", true)]
    [InlineData("utopya france", true)]
    [InlineData("Molano", true)]
    [InlineData("Molano B.V.", true)]
    [InlineData("Tudo4Mobile", false)] // PT
    [InlineData("Loja do Zé Lda", false)] // PT
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsKnownIntraUe_DetectaFornecedoresEU(string? name, bool expected)
        => IntraUeSuppliers.IsKnownIntraUe(name).Should().Be(expected);
}
