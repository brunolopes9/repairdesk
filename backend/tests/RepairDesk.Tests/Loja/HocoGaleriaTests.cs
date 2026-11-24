using FluentAssertions;
using RepairDesk.Services.Loja;

namespace RepairDesk.Tests.Loja;

/// <summary>24/11: galeria oficial da Hoco quando o fornecedor só tem 1 foto — modelo certo e a página certa.</summary>
public class HocoGaleriaTests
{
    [Theory]
    [InlineData("Suporte de Telefone para Carro Hoco H28 Rainbow Saída de Ar Preto", "H28")]
    [InlineData("CARREGADOR RÁPIDO HOCO N83 DE 40W COM TECNOLOGIA GAN", "N83")]
    [InlineData("Película de vidro Hoco G16 – iPhone 15", "G16")]
    [InlineData("Cabo Hoco X88 Lightning 2 m branco", "X88")]
    public void Modelo_vem_logo_a_seguir_a_Hoco(string nome, string modelo) =>
        FichaFornecedor.ModeloHoco(nome).Should().Be(modelo);

    [Theory]
    [InlineData("Película de vidro Hoco – Samsung A16 5G")] // «A16» é o telemóvel, não um modelo Hoco
    [InlineData("Suporte de carro Foneng CP15")]
    public void Sem_modelo_Hoco_nao_procura(string nome) => FichaFornecedor.ModeloHoco(nome).Should().BeNull();

    private const string P = "https://hocotech.com/product/mobile-accessories/";

    [Fact]
    public void Cabo_USB_para_Lightning_nao_fica_com_o_Type_C()
    {
        var urls = new[]
        {
            P + "cables/lightning/cable-type-c-to-lightning-x88-gratified-pd-fast-charge/",
            P + "cables/lightning/cable-usb-to-lightning-x88-gratified/",
            P + "cables/micro-usb/cable-usb-to-micro-usb-x88-gratified/",
        };
        FichaFornecedor.EscolherProdutoHoco(urls, "X88", "Cabo Hoco X88 USB para Lightning 2 m branco")
            .Should().Be(urls[1]);
    }

    [Fact]
    public void Modelo_tem_de_estar_inteiro_no_endereco()
    {
        var urls = new[] { P + "stands/tabletop-holder-ph28-soaring-metal/", P + "in-car/car-holder-h28-rainbow-for-air-outlet/" };
        FichaFornecedor.EscolherProdutoHoco(urls, "H28", "Suporte Hoco H28").Should().Be(urls[1]);
        FichaFornecedor.EscolherProdutoHoco(urls[..1], "H28", "Suporte Hoco H28").Should().BeNull();
    }

    [Fact]
    public void Pelicula_do_mesmo_iPhone()
    {
        var urls = new[] { P + "screen-protectors/g16-tempered-glass-iphone-13/", P + "screen-protectors/g16-tempered-glass-iphone-15/" };
        FichaFornecedor.EscolherProdutoHoco(urls, "G16", "Película de vidro Hoco G16 – iPhone 15").Should().Be(urls[1]);
    }
}
