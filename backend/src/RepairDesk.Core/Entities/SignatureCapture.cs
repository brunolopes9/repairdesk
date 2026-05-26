using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Enums;

namespace RepairDesk.Core.Entities;

/// <summary>
/// Sprint 344: assinatura digital recolhida em tablet/canvas. Guarda imagem PNG
/// (base64 data URL inline) + nome do assinante + timestamp + IP para valor
/// evidencial (proof of delivery / autorização de reparação).
/// </summary>
public class SignatureCapture : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }

    /// <summary>Reparação à qual a assinatura está ligada.</summary>
    public Guid ReparacaoId { get; set; }
    public Reparacao? Reparacao { get; set; }

    public SignatureType Tipo { get; set; }

    /// <summary>
    /// Imagem PNG em formato data URL base64 (<c>data:image/png;base64,...</c>).
    /// Tipicamente 20-100KB. Mantida inline na BD para simplicidade — sem
    /// dependência de R2 / disco. Migrar para storage se passar de 1MB.
    /// </summary>
    public required string ImagemDataUrl { get; set; }

    /// <summary>Nome do assinante (cliente ou representante). Pode diferir do cliente da reparação.</summary>
    public required string AssinanteNome { get; set; }

    /// <summary>Contacto opcional do assinante (telemóvel / email) para tracking.</summary>
    public string? AssinanteContacto { get; set; }

    public DateTime SignedAt { get; set; } = DateTime.UtcNow;

    /// <summary>IP de origem — para audit trail evidencial.</summary>
    public string? RemoteIp { get; set; }

    /// <summary>User do staff que recolheu a assinatura (quem segurou o tablet).</summary>
    public Guid? CapturedByUserId { get; set; }
    public AppUser? CapturedByUser { get; set; }
}
