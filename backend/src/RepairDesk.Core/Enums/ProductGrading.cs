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
/// Sprint 197: de onde vem o produto. Eixo independente do estado visual/funcional (<see cref="ProductGrade"/>).
/// Esta separação 2D substitui o enum monolítico <see cref="ProductGrading"/> que misturava conceitos.
/// </summary>
public enum ProductOrigin
{
    /// <summary>Caixa selada nunca aberta. Vem directo do fornecedor.</summary>
    New = 0,
    /// <summary>Caixa foi aberta, equipamento foi usado por um cliente final, mas NUNCA foi reparado.
    /// Inclui produtos open-box (exposição loja, devolução em condições perfeitas).</summary>
    Used = 1,
    /// <summary>Foi reparado em algum momento. Componentes podem ter sido substituídos.</summary>
    Refurbished = 2,
}

/// <summary>
/// Sprint 197: estado visual/funcional. Escala alinhada com fornecedores PT/EU:
/// - A++ exclusivo Bruno (open-box premium: 100% bateria, zero riscos) — fornecedor "activated-only"
/// - A+ a C são standard Molano (todos com 80%+ bateria garantida) — Mais detalhes em buymolano.com
/// - Sealed só faz sentido para Origin=New
///
/// Futuro: se aparecerem fornecedores com escalas diferentes (Backmarket A/B/C, Swappie "Excellent/Good/Fair"),
/// adiciona-se mapping por Fornecedor.GradeMapping em vez de expandir este enum infinitamente.
/// </summary>
public enum ProductGrade
{
    /// <summary>Selado original. Só válido para Origin=New.</summary>
    Sealed = 0,
    /// <summary>A++ — bateria 100%, zero riscos, open-box only-activated. Tier acima do Molano A+.</summary>
    APlusPlus = 1,
    /// <summary>A+ Molano "como novo" — vestígio quase impercetível.</summary>
    APlus = 2,
    /// <summary>A Molano "excelente" — ligeira descoloração possível.</summary>
    A = 3,
    /// <summary>B+ Molano "muito bom" — max 3 vestígios menores.</summary>
    BPlus = 4,
    /// <summary>B Molano "bom" — max 5 vestígios menores.</summary>
    B = 5,
    /// <summary>C+ Molano "razoável" — riscos profundos ou amolgadelas possíveis.</summary>
    CPlus = 6,
    /// <summary>C Molano "aceitável" — desgaste significativo. Funcional. Bateria ainda 80%+.</summary>
    C = 7,
}

/// <summary>
/// Sprint 204: estado técnico do equipamento (independente do estético Grade).
/// Pedido pelo shop Claude para filtros trust ("Peças originais", "Nunca aberto").
/// </summary>
public enum ProductTechnicalState
{
    /// <summary>Sem info (default — não exposto na loja).</summary>
    Unknown = 0,
    /// <summary>Caixa nunca aberta. Tipicamente Origin=New + Sealed.</summary>
    NeverOpened = 1,
    /// <summary>Foi aberto/usado mas todas as peças são originais. Não reparado.</summary>
    OriginalParts = 2,
    /// <summary>Componentes substituídos (ecrã/bateria/etc). Detalhe em TechnicalNotes.</summary>
    Repaired = 3,
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
