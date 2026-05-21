using RepairDesk.Core.Abstractions;

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
    /// <summary>Nome legível e único por tenant (ex: "Molano", "Tudo4Mobile", "LCPhones").</summary>
    public required string Name { get; set; }
    /// <summary>
    /// Sprint 151: slug estável usado em integrações (webhook payload, CSV importer dropship).
    /// Ex: "molano", "tudo4mobile", "lcphones". Único por tenant. Auto-gerado de Name se não
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
    /// Dias padrão de garantia B2B que este fornecedor dá ao tenant (ex: Molano open-box = 60).
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
}
