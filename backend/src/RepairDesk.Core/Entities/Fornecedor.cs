using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Enums;

namespace RepairDesk.Core.Entities;

/// <summary>
/// Sprint 120: entity formal para fornecedor B2B. Promove o texto livre "FornecedorNome" em
/// VendaItem/Despesa/Part. Centraliza contactos RMA, emails de encomendas, condições padrão.
///
/// Backwards-compat: as 3 strings continuam a existir nas entidades originais. FK opcional
/// (FornecedorId) será adicionada em sprint follow-up. Migração de dados é manual/opcional —
/// Bruno mantém strings antigas tal e qual; novos registos podem ligar a esta entity.
/// </summary>
public class Fornecedor : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    /// <summary>Nome legível e único por tenant (ex: "Tudo4Mobile", "Utopya", "MobileSentrix").</summary>
    public required string Name { get; set; }
    /// <summary>
    /// Sprint 151: slug estável usado em integrações (importação de faturas).
    /// Ex: "tudo4mobile", "utopya", "mobilesentrix". Único por tenant. Auto-gerado de Name se não
    /// definido. Não muda quando Name muda — clientes externos podem referenciá-lo.
    /// </summary>
    public string? Code { get; set; }
    /// <summary>Email comercial para encomendas (info@tudo4mobile.pt).</summary>
    public string? Email { get; set; }
    /// <summary>Email/contacto específico para RMA (devolver peças defeituosas).</summary>
    public string? RmaEmail { get; set; }
    public string? Phone { get; set; }
    public string? Website { get; set; }
    /// <summary>
    /// Dias padrão de garantia B2B que este fornecedor dá ao tenant (ex: 60).
    /// Usado como sugestão ao popular VendaItem.GarantiaFornecedorAteAo.
    /// </summary>
    public int? GarantiaB2BDiasDefault { get; set; }
    /// <summary>Notas internas — formas de pagamento, contactos, ToS observados, etc.</summary>
    public string? Notas { get; set; }
    public bool Active { get; set; } = true;
    /// <summary>
    /// Sprint 162: JSON array de regex patterns para SupplierFingerprintingService
    /// detectar este fornecedor em emails/PDFs automaticamente.
    /// Ex: ["@meufornecedor\\.com", "noreply@meufornecedor", "fatura-meufornecedor"].
    /// NULL → só usa known list hardcoded.
    /// </summary>
    public string? MatchPatternsJson { get; set; }

    /// <summary>
    /// Sprint 184: regra aprendida — default action ao aprovar facturas deste fornecedor.
    /// NULL/Auto = sistema decide (heurística existente: shipping=skip, peça=stock).
    /// Stock = items defaultam a 'new' (criar Part nova).
    /// Despesa = items defaultam a 'despesa' (não entra em stock).
    /// Permite Bruno classificar uma vez e reduzir cliques nas próximas facturas.
    /// </summary>
    public DefaultImportAction DefaultImportAction { get; set; } = DefaultImportAction.Auto;

    /// <summary>
    /// Sprint 543: categoria de Despesa aprendida para este fornecedor (Anthropic→Software,
    /// Vodafone→Comunicações). Pré-selecionada no modal de aprovação — classifica-se uma vez e as
    /// faturas seguintes vêm certas. Bootstrap por lista conhecida ao auto-criar (KnownDespesaSuppliers);
    /// depois aprende com cada aprovação (last-wins). NULL = sem regra (UI usa o default antigo).
    /// </summary>
    public Enums.DespesaCategoria? DefaultDespesaCategoria { get; set; }

    /// <summary>
    /// Regime de IVA das compras a este fornecedor (Nacional 23% / UE autoliquidação 0% / fora da UE).
    /// Dá a taxa de IVA por defeito das linhas de compra. Na UE o adquirente autoliquida: liquida e
    /// deduz o mesmo valor (efeito 0 €); o IVA estrangeiro da fatura NÃO é dedutível em PT (RITI art. 19.º).
    /// </summary>
    public RegimeIvaFornecedor RegimeIva { get; set; } = RegimeIvaFornecedor.Nacional;

    /// <summary>País do fornecedor (ISO 3166-1 alpha-2, ex.: "PT", "NL", "FR").</summary>
    public string? Pais { get; set; }

    /// <summary>Atalho: compras intra-UE em autoliquidação (carimba ReverseCharge nas despesas/movimentos).</summary>
    public bool IntraUe => RegimeIva == RegimeIvaFornecedor.UeAutoliquidacao;

    /// <summary>Taxa de IVA por defeito das linhas de compra deste fornecedor (0,23 nacional; 0 UE/fora).</summary>
    public decimal TaxaIvaCompraPorDefeito(decimal taxaNormal)
        => RegimeIva == RegimeIvaFornecedor.Nacional ? taxaNormal : 0m;
}

public enum DefaultImportAction
{
    Auto = 0,
    Stock = 1,
    Despesa = 2,
}
