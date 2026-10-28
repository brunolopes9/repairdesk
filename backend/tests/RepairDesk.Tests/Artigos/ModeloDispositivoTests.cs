using FluentAssertions;
using RepairDesk.Services.Artigos;

namespace RepairDesk.Tests.Artigos;

/// <summary>Sprint 582: a fatura ao cliente leva o modelo em português, nunca o texto do fornecedor.</summary>
public class ModeloDispositivoTests
{
    [Theory]
    [InlineData("Rixus Clear HD Full Curved Edge Tempered Glass For Apple iPhone XR, 11", "iPhone XR / 11")]
    [InlineData("Rixus Clear HD Tempered Glass With Fit Box For Apple iPhone 14 , 13, 13 Pro, 16e, 17e", "iPhone 14 / 13 / 13 Pro / 16e / 17e")]
    [InlineData("Samsung Galaxy A16 A165 - Full Arc Tempered Glass Black", "Samsung Galaxy A16 A165")]
    [InlineData("Xiaomi Redmi 14C 2411DRN47C, 2409BRN2CA, 2409BRN2CG, 2409BRN2CY - Full Arc Tempered Glass Black", "Xiaomi Redmi 14C")]
    [InlineData("Vidro temperado iph 15", "iPhone 15")]
    [InlineData("Vidro temperado Hoco Samsung A12", "Samsung A12")]
    [InlineData("Capa silicone Samsung A16 4G / 5G preta", "Samsung A16 4G / 5G preta")]
    [InlineData("Rixus Anti-Burst Case For Apple iPhone 14 Black", "iPhone 14 preto")]
    [InlineData("VIDRO TEMPERADO NORMAL", null)]
    public void Extrair(string descricao, string? modelo) => ModeloDispositivo.Extrair(descricao).Should().Be(modelo);

    [Theory]
    [InlineData("Película vidro curva", "Rixus Clear HD Full Curved Edge Tempered Glass For Apple iPhone XR, 11", "Película vidro curva – iPhone XR / 11")]
    [InlineData("Película vidro", "VIDRO TEMPERADO NORMAL", "Película vidro")]
    [InlineData("Cabo", "CABO HOCO X88 GRATIFIED 2,4AA LIGHTNING 2M BRANCO", "CABO HOCO X88 GRATIFIED 2,4AA LIGHTNING 2M BRANCO")]
    [InlineData("Cartão de memória", "Cartão microSD Kingston Canvas Select Plus 64GB c/ adaptador", "Cartão microSD Kingston Canvas Select Plus 64GB c/ adaptador")]
    public void TextoFatura(string artigo, string lote, string esperado) => ModeloDispositivo.TextoFatura(artigo, lote).Should().Be(esperado);

    [Theory]
    [InlineData("Capa", "Capa – iPhone 14 preta")]
    [InlineData("Cabo", "Cabo – iPhone 14 preto")]
    public void Cor_ConcordaComOGeneroDoArtigo(string artigo, string esperado)
        => ModeloDispositivo.TextoFatura(artigo, "Rixus Anti-Burst Case For Apple iPhone 14 Black").Should().Be(esperado);

    [Fact]
    public void SemModeloNaDescricao_PedeModelo_EUsaOEscritoAMao()
    {
        ModeloDispositivo.PrecisaModelo("Película vidro", "VIDRO TEMPERADO NORMAL").Should().BeTrue();
        ModeloDispositivo.PrecisaModelo("Película vidro", "Vidro temperado iph 15").Should().BeFalse();
        ModeloDispositivo.PrecisaModelo("Cabo", "CABO HOCO X88 LIGHTNING 2M BRANCO").Should().BeFalse("a descrição já diz o produto");
        ModeloDispositivo.TextoFatura("Película vidro", "VIDRO TEMPERADO NORMAL", "iPhone 12").Should().Be("Película vidro – iPhone 12");
    }
}
