using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Entities;
using RepairDesk.Core.Enums;

namespace RepairDesk.Services.Catalog;

public interface ICatalogService
{
    Task<CatalogListDto> ListAsync(CatalogQuery query, CancellationToken ct = default);

    /// <summary>Sprint 388: liga/desliga a visibilidade na loja de uma variante (Product ou Part).</summary>
    Task<bool> SetVariantLojaOnlineAsync(string kind, Guid id, bool value, CancellationToken ct = default);

    /// <summary>
    /// Sprint 395: edita preço de venda e/ou stock de uma variante de RETAIL (Product). Peças (Part)
    /// não são editadas aqui — o stock de peças passa pelo ledger de PartMovimento (página Stock).
    /// </summary>
    Task UpdateProductFieldsAsync(Guid id, int? priceCents, int? stockQuantity, CancellationToken ct = default);
}

/// <summary>
/// Sprint 385 (Doc 87): monta a vista unificada "Catálogo &amp; Stock" a partir das três fontes
/// (ProductModel pai + Product variante retail + Part stock técnico). É um READ MODEL — não escreve
/// nada; cada acção (toggle loja, nova variante, etc.) continua a chamar o serviço próprio da
/// entidade. Os KPIs refletem o catálogo todo; os filtros/tabs só afetam as linhas devolvidas.
/// </summary>
public sealed class CatalogService : ICatalogService
{
    private readonly ICatalogReadRepository _repo;
    private readonly IProductRepository _products;
    private readonly IPartRepository _parts;
    public CatalogService(ICatalogReadRepository repo, IProductRepository products, IPartRepository parts)
    {
        _repo = repo;
        _products = products;
        _parts = parts;
    }

    public async Task<CatalogListDto> ListAsync(CatalogQuery query, CancellationToken ct = default)
    {
        var data = await _repo.LoadAsync(ct);
        var parents = BuildParents(data);
        var kpis = BuildKpis(data, parents);
        var filtered = ApplyFilters(parents, query);
        return new CatalogListDto(kpis, filtered);
    }

    public async Task<bool> SetVariantLojaOnlineAsync(string kind, Guid id, bool value, CancellationToken ct = default)
    {
        switch (kind)
        {
            case "product":
            {
                var p = await _products.FindByIdAsync(id, ct) ?? throw new KeyNotFoundException("Produto não encontrado.");
                p.MostrarLojaOnline = value;
                await _products.SaveAsync(ct);
                return value;
            }
            case "part":
            {
                var p = await _parts.FindByIdAsync(id, ct) ?? throw new KeyNotFoundException("Peça não encontrada.");
                p.MostrarLojaOnline = value;
                await _parts.SaveAsync(ct);
                return value;
            }
            default:
                throw new ArgumentException($"kind inválido: {kind}");
        }
    }

    public async Task UpdateProductFieldsAsync(Guid id, int? priceCents, int? stockQuantity, CancellationToken ct = default)
    {
        var p = await _products.FindByIdAsync(id, ct) ?? throw new KeyNotFoundException("Produto não encontrado.");
        if (priceCents is >= 0) p.PriceCents = priceCents.Value;
        if (stockQuantity is >= 0) p.StockQuantity = stockQuantity.Value;
        await _products.SaveAsync(ct);
    }

    private static List<CatalogParentDto> BuildParents(CatalogReadData data)
    {
        var parents = new List<CatalogParentDto>();

        // 1) Modelos retail (pai) → variantes = produtos ligados via ModelId.
        var productsByModel = data.Products
            .Where(p => p.ModelId != null)
            .GroupBy(p => p.ModelId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var model in data.Models)
        {
            productsByModel.TryGetValue(model.Id, out var units);
            units ??= new List<Product>();
            var variants = units.Select(MapProduct).ToList();
            parents.Add(BuildRetailParent(
                kind: "model",
                key: model.Id.ToString(),
                modelId: model.Id,
                nome: $"{model.Brand} {model.Model}".Trim(),
                subtitle: CategoriaLabel(model.Category),
                skuPai: null,
                categoria: CategoriaLabel(model.Category),
                marca: model.Brand,
                hasModelDescription: !string.IsNullOrWhiteSpace(model.DescriptionMarkdown),
                hasModelImage: model.Images.Count > 0,
                imageUrl: model.Images.OrderBy(i => i.Ordem).FirstOrDefault()?.Url,
                units: units,
                variants: variants));
        }

        // 2) Produtos standalone (sem ModelId) → agrupados por (Brand, Model).
        foreach (var grp in data.Products
            .Where(p => p.ModelId == null)
            .GroupBy(p => (Brand: p.Brand.Trim(), Model: p.Model.Trim())))
        {
            var units = grp.ToList();
            var variants = units.Select(MapProduct).ToList();
            var first = units[0];
            parents.Add(BuildRetailParent(
                kind: "product-group",
                key: $"pg|{grp.Key.Brand}|{grp.Key.Model}",
                modelId: null,
                nome: $"{grp.Key.Brand} {grp.Key.Model}".Trim(),
                subtitle: CategoriaLabel(first.Category),
                skuPai: units.Count == 1 ? first.Sku : null,
                categoria: CategoriaLabel(first.Category),
                marca: grp.Key.Brand,
                hasModelDescription: units.Any(u => !string.IsNullOrWhiteSpace(u.DescriptionMarkdown)),
                hasModelImage: units.Any(u => u.Images.Count > 0),
                imageUrl: units.SelectMany(u => u.Images).OrderBy(i => i.Ordem).FirstOrDefault()?.Url,
                units: units,
                variants: variants));
        }

        // 3) Peças técnicas → agrupadas por (Categoria, Marca, Modelo) quando há modelo; senão standalone.
        foreach (var grp in data.Parts.GroupBy(PartGroupKey))
        {
            var items = grp.ToList();
            var first = items[0];
            var temModelo = !string.IsNullOrWhiteSpace(first.Modelo);
            var nome = temModelo
                ? $"{PartCategoriaLabel(first.Categoria)} {first.Modelo}".Trim()
                : first.Nome;
            var variants = items.Select(MapPart).ToList();
            parents.Add(new CatalogParentDto(
                Kind: "part-group",
                Key: grp.Key,
                ModelId: null,
                Nome: nome,
                Subtitle: temModelo ? first.Marca : PartCategoriaLabel(first.Categoria),
                SkuPai: items.Count == 1 ? first.Sku : null,
                Categoria: PartCategoriaLabel(first.Categoria),
                Marca: first.Marca,
                VariantCount: variants.Count,
                StockFisicoUnidades: variants.Sum(v => v.Qtd),
                StockVirtualUnidades: 0,
                ValorStockCents: variants.Sum(v => v.Qtd * v.CustoUnitarioCents),
                LojaOnline: LojaStatus(variants),
                Conteudo: "—",
                MargemMediaPct: null,
                ImageUrl: null,
                Variants: variants));
        }

        return parents;
    }

    private static CatalogParentDto BuildRetailParent(
        string kind, string key, Guid? modelId, string nome, string? subtitle, string? skuPai,
        string categoria, string? marca, bool hasModelDescription, bool hasModelImage,
        string? imageUrl, List<Product> units, List<CatalogVariantDto> variants)
    {
        var temConteudo = (hasModelDescription || units.Any(u => !string.IsNullOrWhiteSpace(u.DescriptionMarkdown)))
            && (hasModelImage || units.Any(u => u.Images.Count > 0));
        var margens = units
            .Where(u => u.PriceCents > 0 && u.CustoUnitarioCents > 0)
            .Select(u => (int)Math.Round((u.PriceCents - u.CustoUnitarioCents) * 100.0 / u.PriceCents))
            .ToList();
        return new CatalogParentDto(
            Kind: kind,
            Key: key,
            ModelId: modelId,
            Nome: nome,
            Subtitle: subtitle,
            SkuPai: skuPai,
            Categoria: categoria,
            Marca: marca,
            VariantCount: variants.Count,
            StockFisicoUnidades: variants.Where(v => v.TipoStock == "fisico").Sum(v => v.Qtd),
            StockVirtualUnidades: variants.Where(v => v.TipoStock == "virtual").Sum(v => v.Qtd),
            ValorStockCents: variants.Where(v => v.TipoStock == "fisico").Sum(v => v.Qtd * v.CustoUnitarioCents),
            LojaOnline: LojaStatus(variants),
            Conteudo: variants.Count == 0 ? "Incompleto" : (temConteudo ? "Completo" : "Incompleto"),
            MargemMediaPct: margens.Count > 0 ? (int)Math.Round(margens.Average()) : null,
            ImageUrl: imageUrl,
            Variants: variants);
    }

    private static CatalogVariantDto MapProduct(Product p) => new(
        Kind: "product",
        Id: p.Id,
        Sku: p.Sku,
        Cor: p.Color,
        Armazenamento: p.Storage,
        Grade: !string.IsNullOrWhiteSpace(p.SupplierGrade) ? p.SupplierGrade : GradeLabel(p.Grade),
        Fornecedor: p.Fornecedor?.Name,
        TipoStock: p.SupplyType == ProductSupplyType.Dropship ? "virtual" : "fisico",
        Qtd: p.StockQuantity,
        PrecoVendaCents: p.PriceCents,
        CustoUnitarioCents: p.CustoUnitarioCents,
        LojaOnline: p.MostrarLojaOnline,
        StockCritico: p.SupplyType == ProductSupplyType.Stock && p.StockMinima > 0 && p.StockQuantity <= p.StockMinima,
        Estado: p.Active ? "Activo" : "Inactivo");

    private static CatalogVariantDto MapPart(Part p) => new(
        Kind: "part",
        Id: p.Id,
        Sku: p.Sku,
        Cor: null,
        Armazenamento: null,
        Grade: null,
        Fornecedor: p.Fornecedor,
        TipoStock: "fisico",
        Qtd: p.QtdStock,
        PrecoVendaCents: null,
        CustoUnitarioCents: p.CustoUnitarioCents,
        LojaOnline: p.MostrarLojaOnline,
        StockCritico: p.QtdMinima > 0 && p.QtdStock <= p.QtdMinima,
        Estado: p.Activo ? "Activo" : "Inactivo");

    private static CatalogKpisDto BuildKpis(CatalogReadData data, List<CatalogParentDto> parents)
    {
        var stockProducts = data.Products.Where(p => p.SupplyType == ProductSupplyType.Stock).ToList();
        var virtualProducts = data.Products.Where(p => p.SupplyType == ProductSupplyType.Dropship).ToList();

        var stockFisicoUn = data.Parts.Sum(p => p.QtdStock) + stockProducts.Sum(p => p.StockQuantity);
        var stockFisicoCusto = data.Parts.Sum(p => p.QtdStock * p.CustoUnitarioCents)
            + stockProducts.Sum(p => p.StockQuantity * p.CustoUnitarioCents);
        var stockVirtualUn = virtualProducts.Sum(p => p.StockQuantity);

        var publicados = data.Products.Count(p => p.MostrarLojaOnline) + data.Parts.Count(p => p.MostrarLojaOnline);
        var totalPublicavel = data.Products.Count + data.Parts.Count;

        var stockCritico = data.Parts.Count(p => p.QtdMinima > 0 && p.QtdStock <= p.QtdMinima)
            + stockProducts.Count(p => p.StockMinima > 0 && p.StockQuantity <= p.StockMinima);

        var semConteudo = parents.Count(p => p.Kind != "part-group" && p.Conteudo == "Incompleto");

        return new CatalogKpisDto(
            StockFisicoUnidades: stockFisicoUn,
            StockFisicoCustoCents: stockFisicoCusto,
            StockVirtualUnidades: stockVirtualUn,
            PublicadosLoja: publicados,
            TotalPublicavel: totalPublicavel,
            StockCritico: stockCritico,
            SemConteudo: semConteudo);
    }

    private static List<CatalogParentDto> ApplyFilters(List<CatalogParentDto> parents, CatalogQuery q)
    {
        IEnumerable<CatalogParentDto> r = parents;

        r = (q.Tab?.Trim().ToLowerInvariant()) switch
        {
            "fisico" => r.Where(p => p.StockFisicoUnidades > 0),
            "virtual" => r.Where(p => p.StockVirtualUnidades > 0),
            "loja" => r.Where(p => p.LojaOnline is "Publicado" or "Parcial"),
            "sem-conteudo" => r.Where(p => p.Conteudo == "Incompleto"),
            "critico" => r.Where(p => p.Variants.Any(v => v.StockCritico)),
            _ => r,
        };

        if (!string.IsNullOrWhiteSpace(q.Categoria))
            r = r.Where(p => string.Equals(p.Categoria, q.Categoria, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(q.Marca))
            r = r.Where(p => string.Equals(p.Marca, q.Marca, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(q.Fornecedor))
            r = r.Where(p => p.Variants.Any(v => string.Equals(v.Fornecedor, q.Fornecedor, StringComparison.OrdinalIgnoreCase)));

        if (!string.IsNullOrWhiteSpace(q.Estado))
            r = r.Where(p => p.Variants.Any(v => string.Equals(v.Estado, q.Estado, StringComparison.OrdinalIgnoreCase)));

        if (!string.IsNullOrWhiteSpace(q.Q))
        {
            var term = q.Q.Trim();
            r = r.Where(p =>
                p.Nome.Contains(term, StringComparison.OrdinalIgnoreCase)
                || (p.SkuPai != null && p.SkuPai.Contains(term, StringComparison.OrdinalIgnoreCase))
                || p.Variants.Any(v =>
                    (v.Sku != null && v.Sku.Contains(term, StringComparison.OrdinalIgnoreCase))
                    || (v.Cor != null && v.Cor.Contains(term, StringComparison.OrdinalIgnoreCase))));
        }

        return r.OrderBy(p => p.Marca).ThenBy(p => p.Nome).ToList();
    }

    private static string PartGroupKey(Part p) =>
        !string.IsNullOrWhiteSpace(p.Modelo)
            ? $"part|{p.Categoria}|{(p.Marca ?? "").Trim().ToLowerInvariant()}|{p.Modelo!.Trim().ToLowerInvariant()}"
            : $"part|{p.Id}";

    private static string LojaStatus(IReadOnlyList<CatalogVariantDto> variants)
    {
        if (variants.Count == 0) return "—";
        var on = variants.Count(v => v.LojaOnline);
        if (on == 0) return "Oculto";
        return on == variants.Count ? "Publicado" : "Parcial";
    }

    private static string CategoriaLabel(ProductCategory c) => c switch
    {
        ProductCategory.Phone => "Telemóveis",
        ProductCategory.Accessory => "Acessórios",
        _ => "Outro",
    };

    private static string PartCategoriaLabel(PartCategoria c) => c.ToString();

    private static string GradeLabel(ProductGrade g) => g switch
    {
        ProductGrade.Sealed => "Novo",
        ProductGrade.APlusPlus => "A++",
        ProductGrade.APlus => "A+",
        ProductGrade.A => "A",
        ProductGrade.BPlus => "B+",
        ProductGrade.B => "B",
        ProductGrade.CPlus => "C+",
        ProductGrade.C => "C",
        _ => g.ToString(),
    };
}
