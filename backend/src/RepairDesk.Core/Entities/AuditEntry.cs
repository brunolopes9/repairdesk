using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Enums;

namespace RepairDesk.Core.Entities;

public class AuditEntry : ITenantEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid? AppUserId { get; set; }
    public AppUser? AppUser { get; set; }
    /// <summary>
    /// Quando a acção foi executada por integração externa (loja online, importador),
    /// AppUserId é null e isto referencia a chave que autenticou — para que a UI
    /// possa mostrar "Loja online produção" em vez de "Integração externa" anónimo.
    /// </summary>
    public Guid? ServiceApiKeyId { get; set; }
    public ServiceApiKey? ServiceApiKey { get; set; }
    public AuditAction Action { get; set; }
    public required string EntityType { get; set; }
    public Guid? EntityId { get; set; }
    public string? ChangesJson { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
