using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Enums;

namespace RepairDesk.Core.Entities;

/// <summary>
/// Foto associada a uma reparação. Binário guardado em IPhotoStorage,
/// metadados em DB.
/// </summary>
public class ReparacaoFoto : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid ReparacaoId { get; set; }
    public Reparacao? Reparacao { get; set; }

    /// <summary>Chave de armazenamento (path lógico). Ex: "tenants/{T}/reparacoes/{R}/{Guid}.jpg".</summary>
    public required string StorageKey { get; set; }
    public required string FileName { get; set; }
    public required string ContentType { get; set; }
    public long Size { get; set; }

    public FotoTipo Tipo { get; set; } = FotoTipo.Antes;
    public int Ordem { get; set; }
    public string? Legenda { get; set; }

    /// <summary>Se true, é visível no portal cliente público (`Antes` e `Depois` por default; `Durante` opcional).</summary>
    public bool VisivelNoPortal { get; set; }
}
