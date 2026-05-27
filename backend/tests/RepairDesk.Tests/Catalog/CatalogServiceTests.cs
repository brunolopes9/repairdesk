using FluentAssertions;
using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;
using RepairDesk.Services.Catalog;

namespace RepairDesk.Tests.Catalog;

public class CatalogServiceTests
{
    private sealed class StubRepo : ICatalogReadRepository
    {
        private readonly CatalogReadData _data;
        public StubRepo(CatalogReadData data) => _data = data;
        public Task<CatalogReadData> LoadAsync(CancellationToken ct = default) => Task.FromResult(_data);
    }

    private static CatalogService Build(CatalogReadData data) => new(new StubRepo(data));

    private static CatalogReadData Sample()
    {
        var modelId = Guid.NewGuid();
        var model = new ProductModel
        {
            Id = modelId,
            Brand = "Apple",
            Model = "iPhone 15",
            Category = ProductCategory.Phone,
            DescriptionMarkdown = "iPhone 15 com USB-C.",
            Images = { new ProductModelImage { Url = "https://img/iphone15.webp", Ordem = 0 } },
        };

        // Variante ligada ao modelo: stock físico, publicada.
        var variantePreta = new Product
        {
            Id = Guid.NewGuid(), Sku = "APP-IP15-PRT-128", Slug = "iphone-15-preto-128",
            Brand = "Apple", Model = "iPhone 15", Color = "Preto", Storage = "128GB",
            ModelId = modelId, SupplyType = ProductSupplyType.Stock,
            StockQuantity = 8, StockMinima = 2, PriceCents = 89900, CustoUnitarioCents = 70000,
            MostrarLojaOnline = true, Active = true,
        };
        // Variante virtual (dropship), oculta.
        var varianteVerde = new Product
        {
            Id = Guid.NewGuid(), Sku = "APP-IP15-VRD-512", Slug = "iphone-15-verde-512",
            Brand = "Apple", Model = "iPhone 15", Color = "Verde", Storage = "512GB",
            ModelId = modelId, SupplyType = ProductSupplyType.Dropship,
            StockQuantity = 50, PriceCents = 109900, CustoUnitarioCents = 90000,
            MostrarLojaOnline = false, Active = true,
        };

        // Peça técnica (stock físico, crítico).
        var ecra = new Part
        {
            Id = Guid.NewGuid(), Sku = "ECR-IP12", Nome = "Ecrã iPhone 12",
            Categoria = PartCategoria.Outro, Marca = "Apple", Modelo = "iPhone 12",
            QtdStock = 1, QtdMinima = 3, CustoUnitarioCents = 4500, MostrarLojaOnline = false, Activo = true,
        };

        return new CatalogReadData(
            new[] { model },
            new[] { variantePreta, varianteVerde },
            new[] { ecra });
    }

    [Fact]
    public async Task Lista_agrupa_modelo_com_variantes()
    {
        var svc = Build(Sample());
        var res = await svc.ListAsync(new CatalogQuery());

        var modelo = res.Parents.Should().ContainSingle(p => p.Kind == "model").Subject;
        modelo.Nome.Should().Be("Apple iPhone 15");
        modelo.VariantCount.Should().Be(2);
        modelo.StockFisicoUnidades.Should().Be(8);
        modelo.StockVirtualUnidades.Should().Be(50);
        modelo.LojaOnline.Should().Be("Parcial"); // uma publicada, outra oculta
        modelo.Conteudo.Should().Be("Completo");   // tem descrição + imagem
    }

    [Fact]
    public async Task Pecas_aparecem_como_part_group()
    {
        var svc = Build(Sample());
        var res = await svc.ListAsync(new CatalogQuery());

        var peca = res.Parents.Should().ContainSingle(p => p.Kind == "part-group").Subject;
        peca.Nome.Should().Contain("iPhone 12");
        peca.Variants.Should().ContainSingle().Which.TipoStock.Should().Be("fisico");
        peca.Conteudo.Should().Be("—");
    }

    [Fact]
    public async Task Kpis_somam_stock_fisico_e_virtual()
    {
        var svc = Build(Sample());
        var res = await svc.ListAsync(new CatalogQuery());

        // Físico = produto stock 8 + peça 1 = 9; virtual = produto dropship 50.
        res.Kpis.StockFisicoUnidades.Should().Be(9);
        res.Kpis.StockVirtualUnidades.Should().Be(50);
        // Custo físico = 8*70000 + 1*4500 = 564500.
        res.Kpis.StockFisicoCustoCents.Should().Be(564500);
        // Crítico: a peça (1<=3). O produto stock 8>2 não conta.
        res.Kpis.StockCritico.Should().Be(1);
        // Publicados = 1 produto + 0 peças; universo = 2 produtos + 1 peça.
        res.Kpis.PublicadosLoja.Should().Be(1);
        res.Kpis.TotalPublicavel.Should().Be(3);
    }

    [Fact]
    public async Task Tab_critico_filtra_so_linhas_com_variante_critica()
    {
        var svc = Build(Sample());
        var res = await svc.ListAsync(new CatalogQuery(Tab: "critico"));

        res.Parents.Should().OnlyContain(p => p.Variants.Any(v => v.StockCritico));
        res.Parents.Should().ContainSingle(p => p.Kind == "part-group");
    }
}
