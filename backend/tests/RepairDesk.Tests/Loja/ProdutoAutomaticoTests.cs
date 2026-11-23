using System.Text.Json;
using FluentAssertions;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;
using RepairDesk.Services.Loja;

namespace RepairDesk.Tests.Loja;

/// <summary>23/11: «Pôr na loja» num clique — regras do título, marca, categoria e leitura das fotos do fornecedor.</summary>
public class ProdutoAutomaticoTests
{
    private static Artigo A(string nome, string? nomeFatura = null, string? categoria = null) => new()
    {
        Nome = nome, NomeFatura = nomeFatura, Categoria = categoria, Sku = "X1", CodigoBarras = "123",
    };

    [Fact]
    public void Titulo_UsaONomeDaFatura_LegivelESemAllColours()
    {
        var t = ProdutoAutomatico.Titulo(A("Película", "CARREGADOR RIXUS 45W USB-C + USB-A (ALL COLOURS)"));
        t.Should().NotContainEquivalentOf("all colours").And.NotContain("()");
        t.Should().Contain("45W");
        t.Should().NotBe(t.ToUpperInvariant(), "maiúsculas a gritar passam a texto legível");
    }

    [Fact]
    public void Titulo_MantemAQualidadeDaPeca()
        => ProdutoAutomatico.Titulo(A("Ecrã A52", "Display Samsung A52 Service Pack")).Should().Contain("Service Pack");

    [Theory]
    [InlineData("Rixus", "Carregador 45W", "Rixus")]
    [InlineData("4Phones", "Película iPhone 15 Pro", "Apple")]
    [InlineData(null, "Capa Samsung Galaxy A25", "Samsung")]
    [InlineData(null, "Película Redmi 15C", "Xiaomi")]
    [InlineData(null, "Suporte de carro magnético", "Genérico")]
    public void Marca_DoFornecedorOuPeloTitulo(string? vendor, string titulo, string esperado)
        => ProdutoAutomatico.Marca(vendor, titulo).Should().Be(esperado);

    [Theory]
    [InlineData("Peças", "Ecrã Samsung A52", LojaCategoria.Pecas)]
    [InlineData(null, "Bateria iPhone 12", LojaCategoria.Pecas)]
    [InlineData("Acessórios", "Carregador 45W", LojaCategoria.Acessorios)]
    [InlineData(null, "Portátil Lenovo ThinkPad", LojaCategoria.Computadores)]
    [InlineData(null, "Película iPad 10", LojaCategoria.Tablets)]
    public void Categoria_PeloArtigoEPeloTitulo(string? cat, string titulo, LojaCategoria esperado)
        => ProdutoAutomatico.Categoria(cat, titulo).Should().Be(esperado);

    [Fact]
    public void Codigos_JuntaSkuDosLotesEFabricanteSemRepetidos()
    {
        var a = A("x");
        a.CodigoFabricante = "RXCC16D";
        a.Lotes.Add(new CompraLinha { Descricao = "x", SkuFornecedor = "RXCC16D" });
        a.Lotes.Add(new CompraLinha { Descricao = "x", SkuFornecedor = "PLV-123" });
        ProdutoAutomatico.Codigos(a).Should().BeEquivalentTo("RXCC16D", "PLV-123");
    }

    [Fact]
    public void LerProduto_ImagensShopifyPassamAHttpsSemRepetir()
    {
        var json = JsonDocument.Parse("""
            {"title":"Rixus 45W","vendor":"Rixus","description":"<p>PD <b>45W</b></p>",
             "images":["//4phones.eu/cdn/shop/files/a.jpg?v=1","//4phones.eu/cdn/shop/files/a.jpg?v=1","https://cdn.x/b.png"]}
            """).RootElement;
        var f = FichaFornecedor.LerProduto(json);
        f.Marca.Should().Be("Rixus");
        f.Texto.Should().Be("PD 45W");
        f.Imagens.Should().Equal("https://4phones.eu/cdn/shop/files/a.jpg?v=1", "https://cdn.x/b.png");
        f.TextoCompleto.Should().Be("Rixus 45W\nPD 45W");
    }
}

/// <summary>23/11: procurar no fornecedor pelo nome da fatura sem trazer fotos de outro modelo.</summary>
public class SemelhancaFichaTests
{
    [Fact]
    public void MesmoProduto_PontuaAlto()
        => FichaFornecedor.Semelhanca("Rixus Ultra Clear Camera Protection For Apple iPhone 17 Pro Deep Blue",
            "Rixus Ultra Clear Camera Protection For Apple iPhone 17 Pro Deep Blue").Should().Be(1);

    [Theory]
    [InlineData("Rixus Ultra Clear Camera Protection For Apple iPhone 17 Pro Max Deep Blue")]
    [InlineData("Rixus Ultra Clear Camera Protection For Apple iPhone 16 Pro Deep Blue")]
    [InlineData("Rixus Ultra Clear Camera Protection For Apple iPhone 17 Deep Blue")]
    public void OutroModelo_DaZero(string titulo)
        => FichaFornecedor.Semelhanca("Rixus Ultra Clear Camera Protection For Apple iPhone 17 Pro Deep Blue", titulo).Should().Be(0);
}

/// <summary>23/11: página de produto da Tudo4Mobile (OpenCart) — SKU, nome e fotos 1000×1000 da galeria.</summary>
public class Tudo4MobilePaginaTests
{
    private const string Html = """
        <script type="application/ld+json">{"@context":"http://schema.org","@type":"Organization","url":"x"}</script>
        <img src="https://www.tudo4mobile.pt/image/cache/catalog/Ivan/fav_t4m-1000x1000.png">
        <a href="https://www.tudo4mobile.pt/image/cache/catalog/1 Karan/160436-1000x1000.jpg"><img src="https://www.tudo4mobile.pt/image/cache/catalog/1 Karan/160436-500x500.jpg"></a>
        <a href="https://www.tudo4mobile.pt/image/cache/catalog/1 Karan/160436-2-1000x1000.jpg"></a>
        <script type="application/ld+json">{"@context":"http://schema.org/","@type":"Product","name":"Protetor de Vidro da C&acirc;mera Traseira para Apple iPhone 17 Pro/iPhone 17 Pro Max Azul Profundo","sku":"160436","brand":{"@type":"Thing","name":"UNIVERSAL"},"description":"Compra Protetor. Ajuste perfeito."}</script>
        """;

    [Fact]
    public void LeSkuNomeEFotosSemLogos()
    {
        var f = FichaFornecedor.LerPaginaTudo4Mobile(Html, out var sku)!;
        sku.Should().Be("160436");
        f.Titulo.Should().StartWith("Protetor de Vidro da Câmera");
        f.Marca.Should().BeNull("«UNIVERSAL» não é marca");
        f.Imagens.Should().Equal(
            "https://www.tudo4mobile.pt/image/cache/catalog/1%20Karan/160436-1000x1000.jpg",
            "https://www.tudo4mobile.pt/image/cache/catalog/1%20Karan/160436-2-1000x1000.jpg");
    }

    [Fact]
    public void ModeloDaFaturaEmPortugues_BateComOTituloDoSite()
        => FichaFornecedor.Semelhanca("Película de câmara traseira iPhone 17 Pro / 17 Pro Max Deep Blue",
            "Protetor de Vidro da Câmera Traseira para Apple iPhone 17 Pro/iPhone 17 Pro Max Azul Profundo").Should().BeGreaterThan(0.2);
}

/// <summary>23/11: o 4Phones só pesquisa em inglês — a peça em português vira pesquisa em inglês com a mesma qualidade.</summary>
public class ConsultaInglesaTests
{
    [Theory]
    [InlineData("Ecrã Samsung A16 5G azul / preto (Service Pack)", "Galaxy A16 5G display service pack")]
    [InlineData("Touch e lcd com frame Samsung A16 5G azul/preto (Service Pack)", "Galaxy A16 5G display service pack")]
    [InlineData("Bateria iPhone 13 Pro (OEM)", "iPhone 13 Pro battery oem")]
    [InlineData("Bateria iPhone 13 Pro (sem placa)", null)]
    [InlineData("Capa silicone Samsung A16 4G / 5G preta", null)]
    public void Consulta(string nome, string? esperado)
        => RepairDesk.Services.Loja.FichaFornecedor.ConsultaInglesa(nome)?.Q.Should().Be(esperado);

    [Theory]
    [InlineData("Ecrã Samsung A16 5G azul / preto (Service Pack)", "Samsung Galaxy A16 5G A166B Display And Digitizer With Frame Blue Black Service Pack", true)]
    [InlineData("Ecrã Samsung A16 5G azul / preto (Service Pack)", "Samsung Galaxy A16 5G A166B Display And Digitizer With Frame Blue Black In-Cell", false)]
    [InlineData("Ecrã Samsung A16 5G azul / preto (Service Pack)", "Samsung Galaxy A16 4G A165F Display And Digitizer With Frame Black Service Pack", false)]
    [InlineData("Bateria iPhone 13 Pro (OEM)", "For Apple iPhone 13 Pro Run Diagnostics Battery OEM", true)]
    [InlineData("Bateria iPhone 13 Pro (OEM)", "For Apple iPhone 13 Pro Max Run Diagnostics Battery OEM", false)]
    [InlineData("Bateria iPhone 13 Pro (original)", "For Apple iPhone 13 Pro Battery 661-21996 Original", true)]
    public void MesmaPeca(string nome, string titulo, bool esperado)
        => RepairDesk.Services.Loja.FichaFornecedor.MesmaPeca(RepairDesk.Services.Loja.FichaFornecedor.ConsultaInglesa(nome)!, titulo).Should().Be(esperado);
}
