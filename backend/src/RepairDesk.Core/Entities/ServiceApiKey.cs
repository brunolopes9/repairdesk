using RepairDesk.Core.Abstractions;

namespace RepairDesk.Core.Entities;

/// <summary>
/// Chave de API para integrações externas (loja online, importadores, automações).
/// Diferente de JWT de utilizador: scope é o tenant inteiro, sem expiração natural,
/// revogável manualmente. Key armazenada como hash SHA256 — plain só é mostrada na criação.
/// </summary>
public class ServiceApiKey : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    /// <summary>Nome legível (ex: "Loja online produção", "Importador CSV mensal").</summary>
    public required string Name { get; set; }
    /// <summary>Prefixo visível para identificação na UI (ex: "rd_live_a1b2c3"). NÃO sensível.</summary>
    public required string KeyPrefix { get; set; }
    /// <summary>SHA256 hex do plain key (lookup-friendly, não-reversível).</summary>
    public required string KeyHash { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string? RevokedReason { get; set; }
}
