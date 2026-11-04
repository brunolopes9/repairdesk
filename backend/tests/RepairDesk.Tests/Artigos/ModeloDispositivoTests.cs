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
    // Sprint 588: película de gel (Sunshine) — o tipo vem da folha usada; o modelo escreve-se no balcão.
    [InlineData("SUNSHINE SS-057E Matte Imported Hydrogel Film (50pcs)", "Película de hidrogel mate – iPhone 14")]
    [InlineData("SUNSHINE SS-057S Privacy Imported Flexible Hydrogel Film (50pcs)", "Película de hidrogel de privacidade – iPhone 14")]
    [InlineData("SUNSHINE SS-057 HD Hydrogel Film (50pcs)", "Película de hidrogel – iPhone 14")]
    public void PeliculaDeGel_TipoDaFolha_EModeloEscrito(string lote, string esperado)
        => ModeloDispositivo.TextoFatura("Película de hidrogel", lote, "iPhone 14").Should().Be(esperado);

    [Fact]
    public void Variante_NaoRepeteONomeDoArtigo()
        => ModeloDispositivo.TextoFatura("Película de privacidade", "Rixus Privacy Tempered Glass For Apple iPhone 15").Should().Be("Película de privacidade – iPhone 15");

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

    [Theory]
    [InlineData("PIXDURA For Apple iPhone 14 Display And Digitizer In-Cell Premium (IC Removable) (60Hz)", "Ecrã iPhone 14")]
    [InlineData("For Apple iPhone 14 Pro Max Display And Digitizer Soft-OLED (Run Diagnostics)", "Ecrã iPhone 14 Pro Max")]
    [InlineData("Xiaomi Mi A3 M1906F9SH, M1906F9SI - Battery Cover with Adhesive Kind of Gray", "Tampa traseira Xiaomi Mi A3 M1906F9SH / M1906F9SI")]
    [InlineData("Samsung Galaxy A13 (2022) A137 - Dock Charging Connector Board < Service Pack >", "Conector de carga Samsung Galaxy A13")]
    [InlineData("iPhone 15 - Battery A3018 3349mAh 12.981Wh (NO Error, Run Diagnostics)", "Bateria iPhone 15")]
    [InlineData("Touch e lcd para iph 11 preto", "Ecrã iPhone 11 preto")]
    [InlineData("Touch para apple iph 5s telemovel", "Ecrã iPhone 5s")]
    [InlineData("REALME 12X 5G 6.67 INCH REALME V60 REALME V60S PANTALLA LCD", "Ecrã Realme 12X 5G")]
    // Sprint 587: cauda em espanhol e códigos do fornecedor fora; marcas com a grafia certa.
    [InlineData("TAPA TRASERA SAMSUNG GALAXY A15 4G A155 A15 5G A156 TAPA TRASERA O TAPA", "Tampa traseira Samsung Galaxy A15 4G A155 A15 5G A156")]
    [InlineData("BATERIA IPHONE 5S APN: 616-0720", "Bateria iPhone 5S")]
    [InlineData("Pantalla Lcd para Realme 12x RMX3997 Marco Negro", "Ecrã Realme 12x RMX3997")]
    [InlineData("Amaoe - BGA Reballing Stencil With CPU Position For iPhone 12", null)]
    public void NomePeca(string descricao, string? esperado) => ModeloDispositivo.NomePeca(descricao).Should().Be(esperado);
}
