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
