namespace RepairDesk.Core.Enums;

/// <summary>
/// Condição de qualidade de um <see cref="Entities.Product"/> revendido. Specific a telemóveis
/// refurbished/novos. NÃO confundir com <see cref="CondicaoArtigo"/> (Sprint 107 — usada em
/// VendaItem snapshot do fornecedor B2B).
/// </summary>
public enum ProductGrading
{
    Novo = 0,
    GradeA = 1,
    GradeB = 2,
    GradeC = 3,
    OpenBox = 4,
    /// <summary>Visualmente perfeito, sem garantia de tela 100%. Termo Molano.</summary>
    Premium = 5,
}

/// <summary>
/// Tipo de fornecimento — diz à loja se precisa de manter stock ou se é dropshipping.
/// Decide o flow de checkout: stock real verifica QtdStock; dropship aceita sempre e
/// encomenda ao fornecedor depois.
/// </summary>
public enum ProductSupplyType
{
    /// <summary>Bruno tem o equipamento físicamente. QtdStock decrementa ao vender.</summary>
    Stock = 0,
    /// <summary>Sem stock — encomenda-se ao fornecedor após venda (Molano).</summary>
    Dropship = 1,
}

/// <summary>
/// Sprint 151: categoria de produto na loja online. Bruno tem 2 tipos distintos:
/// telemóveis (refurbished/novos) e acessórios (capas, películas, cabos). Loja precisa
/// separar para filtros/listings com semântica diferente. <see cref="Other"/> é placeholder
/// para futuras categorias (tablets, smartwatches) sem nova migration.
/// </summary>
public enum ProductCategory
{
    Phone = 0,
    Accessory = 1,
    Other = 2,
}
