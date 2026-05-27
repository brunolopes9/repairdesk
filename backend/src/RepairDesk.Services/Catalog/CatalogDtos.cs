namespace RepairDesk.Services.Catalog;

/// <summary>Sprint 385 (Doc 87): filtros da listagem unificada "Catálogo &amp; Stock".</summary>
public sealed record CatalogQuery(
    string? Q = null,
    string? Categoria = null,
    string? Marca = null,
    string? Fornecedor = null,
    string? Estado = null,
    /// <summary>todos | fisico | virtual | loja | sem-conteudo | critico</summary>
    string Tab = "todos");

/// <summary>KPIs do topo do catálogo.</summary>
public sealed record CatalogKpisDto(
    int StockFisicoUnidades,
    int StockFisicoCustoCents,
    int StockVirtualUnidades,
    int PublicadosLoja,
    int TotalPublicavel,
    int StockCritico,
    int SemConteudo);

/// <summary>Variante (linha-filho): uma unidade <c>Product</c> (retail) ou uma <c>Part</c> (técnico).</summary>
public sealed record CatalogVariantDto(
    string Kind,          // "product" | "part"
    Guid Id,
    string? Sku,
    string? Cor,
    string? Armazenamento,
    string? Grade,
    string? Fornecedor,
    string TipoStock,     // "fisico" | "virtual"
    int Qtd,
    int? PrecoVendaCents, // null para peças (preço definido na venda)
    int CustoUnitarioCents,
    bool LojaOnline,
    bool StockCritico,
    string Estado);       // "Activo" | "Inactivo"

/// <summary>Linha-pai expansível: um modelo retail, um grupo de produtos, ou um grupo de peças.</summary>
public sealed record CatalogParentDto(
    string Kind,          // "model" | "product-group" | "part-group"
    string Key,           // id estável para o frontend (modelId ou chave sintética)
    Guid? ModelId,        // preenchido quando Kind == "model"
    string Nome,
    string? Subtitle,
    string? SkuPai,
    string Categoria,
    string? Marca,
    int VariantCount,
    int StockFisicoUnidades,
    int StockVirtualUnidades,
    int ValorStockCents,
    string LojaOnline,    // "Publicado" | "Oculto" | "Parcial" | "—"
    string Conteudo,      // "Completo" | "Incompleto" | "—"
    int? MargemMediaPct,
    string? ImageUrl,
    IReadOnlyList<CatalogVariantDto> Variants);

/// <summary>Resposta da listagem: KPIs globais + linhas-pai filtradas.</summary>
public sealed record CatalogListDto(
    CatalogKpisDto Kpis,
    IReadOnlyList<CatalogParentDto> Parents);
