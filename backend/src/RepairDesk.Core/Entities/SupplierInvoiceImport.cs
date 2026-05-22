using RepairDesk.Core.Abstractions;

namespace RepairDesk.Core.Entities;

/// <summary>
/// Sprint 147: fatura/PDF de fornecedor recebida via endpoint ingest (tipicamente n8n IMAP),
/// guardada num filesystem organizado por tenant/ano/mês/fornecedor para audit + acesso do
/// contabilista. Fica em <see cref="SupplierInvoiceImportStatus.Pending"/> até Bruno revê e
/// aprova — só então gera uma <see cref="Despesa"/> real que entra na contabilidade.
/// </summary>
public class SupplierInvoiceImport : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }

    /// <summary>FK opcional para <see cref="Fornecedor"/> reconhecido pelo parser.</summary>
    public Guid? FornecedorId { get; set; }
    public Fornecedor? Fornecedor { get; set; }

    /// <summary>Nome do fornecedor como detectado pelo parser (ex: "Tudo4Mobile"). Bruno corrige na aprovação.</summary>
    public string? FornecedorNameRaw { get; set; }

    /// <summary>SHA256 hex (64 chars) do PDF original. Unique per tenant — bloqueia re-ingest do mesmo PDF.</summary>
    public required string PdfSha256 { get; set; }

    /// <summary>Path relativo dentro do storage root (ex: "2026/05/tudo4mobile/2026-05-20_FT-2026-2841.pdf").</summary>
    public required string PdfRelativePath { get; set; }

    public int PdfBytesSize { get; set; }

    // Email metadata (vinda do n8n)
    public string? EmailMessageId { get; set; }
    public string? EmailSubject { get; set; }
    public string? EmailFrom { get; set; }
    public DateTime? EmailReceivedAt { get; set; }

    // Parser output (snapshot — mesmo que reprocesses, este valor não muda)
    public int? ParsedTotalCents { get; set; }
    public int? ParsedSubtotalCents { get; set; }
    /// <summary>JSON serializado dos items detectados (SupplierPdfItem array).</summary>
    public string? ParsedItemsJson { get; set; }
    public string? ParsedDocumentNumber { get; set; }
    public DateTime? ParsedDocumentDate { get; set; }
    /// <summary>None/Low/Medium/High — Bruno usa para priorizar revisão manual.</summary>
    public string? ParseConfidence { get; set; }
    /// <summary>
    /// Sprint 171: warnings da validação pós-parse (totais não batem, datas estranhas, etc).
    /// JSON array de strings. NULL se parsing limpo.
    /// </summary>
    public string? ParseWarningsJson { get; set; }

    public SupplierInvoiceImportStatus Status { get; set; } = SupplierInvoiceImportStatus.Pending;
    public string? RejectionReason { get; set; }
    public DateTime? ProcessedAt { get; set; }

    /// <summary>FK opcional para a <see cref="Despesa"/> criada quando Bruno aprova.</summary>
    public Guid? DespesaId { get; set; }
    public Despesa? Despesa { get; set; }

    public Guid? CreatedByApiKeyId { get; set; }
}

public enum SupplierInvoiceImportStatus
{
    /// <summary>Aguarda revisão manual do Bruno.</summary>
    Pending = 0,
    /// <summary>Aprovado — Despesa real criada.</summary>
    Approved = 1,
    /// <summary>Rejeitado pelo Bruno (duplicado mal-detectado, fatura errada, etc).</summary>
    Rejected = 2,
    /// <summary>Parser falhou — Bruno pode forçar reprocess ou aprovar com valores manuais.</summary>
    Failed = 3,
}
