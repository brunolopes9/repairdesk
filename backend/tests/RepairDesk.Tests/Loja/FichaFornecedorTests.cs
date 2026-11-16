using FluentAssertions;
using RepairDesk.Services.Loja;

namespace RepairDesk.Tests.Loja;

/// <summary>Sprint 689: a ficha do fornecedor chega em HTML — a IA recebe texto simples, linha a linha.</summary>
public class FichaFornecedorTests
{
    [Fact]
    public void Limpar_TiraHtml_MantemLinhas_EConverteVirgulasChinesas()
    {
        var html = "<p>Output Power: 20W MAX<br>Input:DC 12-24V，2A</p><ul><li>Weight: 30g</li><li>Size: 5.4*2.6cm</li></ul>&amp; mais";
        FichaFornecedor.Limpar(html).Should().Be("Output Power: 20W MAX\nInput:DC 12-24V,2A\nWeight: 30g\nSize: 5.4*2.6cm\n& mais");
        FichaFornecedor.Limpar(null).Should().BeEmpty();
    }
}
