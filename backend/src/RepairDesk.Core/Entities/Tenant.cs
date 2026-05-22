using RepairDesk.Core.Enums;

namespace RepairDesk.Core.Entities;

public class Tenant : BaseEntity
{
    public required string Name { get; set; }
    public string? LegalName { get; set; }
    public string? Nif { get; set; }
    public string? Address { get; set; }
    public string? PostalCode { get; set; }
    public string? Locality { get; set; }
    public string? Country { get; set; } = "PT";
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Website { get; set; }
    public string? Iban { get; set; }
    public string? CaePrincipal { get; set; }
    public string? CaeSecundarios { get; set; } // CSV de códigos: "47401,58290,95101,95102"
    public RegimeFiscal RegimeFiscal { get; set; } = RegimeFiscal.IsentoArt53;
    public string? TermosCondicoes { get; set; }
    public string? LogoUrl { get; set; }
    public string? PrimaryColor { get; set; }
    public bool IsActive { get; set; } = true;
    public bool OnboardingCompletado { get; set; }

    // Garantia (defaults aplicados quando uma reparação é entregue)
    public int GarantiaDiasDefault { get; set; } = 90;
    public string? GarantiaCoberturaDefault { get; set; }
    public string? GarantiaExclusoesDefault { get; set; }

    // Garantia em Vendas — DL 84/2021 obriga 3 anos para consumo (1095 dias).
    // Refurbished pode ser reduzido até 18 meses (540 dias) só com acordo expresso.
    // Sprint 127: granularidade por CondicaoArtigo. Default flat (Novo) + 3 condições
    // adicionais. Tenant pode subir todas para 1095 se quiser política comercial uniforme
    // (ex: LopesTech dá 3 anos a refurbished como estilo iServices, acordo em /termos).
    public int GarantiaVendaDiasDefault { get; set; } = 1095;
    public int GarantiaVendaOpenBoxDias { get; set; } = 730;          // 2 anos — entre novo e refurbished
    public int GarantiaVendaRecondicionadoDias { get; set; } = 540;   // 18m mínimo legal DL 84/2021
    public int GarantiaVendaUsadoDias { get; set; } = 540;            // 18m mínimo legal
    public string? GarantiaVendaCoberturaDefault { get; set; }
    public string? GarantiaVendaExclusoesDefault { get; set; }

    // Google Reviews funil (mostrado ao cliente quando avalia 4-5 estrelas)
    public string? GoogleReviewUrl { get; set; }

    public TenantBillingSettings? BillingSettings { get; set; }

    // Sprint 167b: plano SaaS + quota LLM mensal.
    // Free=100 chamadas/mês, Pro=1000, Enterprise=ilimitado (usa key própria).
    // Tenant sem plano definido → Free por defeito (gracefully).
    public TenantPlan Plan { get; set; } = TenantPlan.Free;
    /// <summary>Quota override per-tenant. NULL → usa default do plano (100/1000/ilimitado).</summary>
    public int? LlmQuotaMonthly { get; set; }

    // Sprint 172: Anthropic API key BYOK opcional. NULL = usa central key Bruno.
    // Preenchido = tenant paga Anthropic directo (zero quota RepairDesk).
    public string? AnthropicApiKeyCipherText { get; set; }
    public DateTime? AnthropicValidatedAt { get; set; }

    // Sprint 173: email forwarding ingest per-tenant (RGPD-clean).
    public string? IngestEmailSlug { get; set; }

    // Sprint 175: retention policy por tipo de SupplierInvoiceImport.
    // Defaults conservadores PT (CIRS art. 123 obriga arquivo fiscal 10 anos).
    // Cron diário às 3h apaga PDFs raw + soft-delete entity quando expira.
    // Metadata estruturada (items JSON, totais, IVA) mantém-se sempre — é o
    // "accounting vault" do tenant.
    /// <summary>Dias após criação para apagar imports Rejected. NULL = nunca.</summary>
    public int? RetentionRejectedDays { get; set; } = 15;
    /// <summary>Dias após criação para apagar imports Failed (parsing falhou). NULL = nunca.</summary>
    public int? RetentionFailedDays { get; set; } = 30;
    /// <summary>Dias após aprovação para apagar PDF raw (metadata fica). NULL = permanente (recomendado para PT).</summary>
    public int? RetentionApprovedPdfDays { get; set; } = null;
}

public enum TenantPlan
{
    Free = 0,
    Pro = 1,
    Enterprise = 2,
}
