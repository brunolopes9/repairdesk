namespace RepairDesk.Core.Entities;

/// <summary>
/// Sprint 606: o utilizador também tem acesso a outra empresa (além da sua, <see cref="AppUser.TenantId"/>).
/// O dono da plataforma usa a mesma conta para a LopesTech e para a Cortex Automation, e troca de
/// empresa no Mender. Não é tenant-scoped: liga uma pessoa a uma empresa, fora de qualquer tenant.
/// </summary>
public class AcessoEmpresa
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid TenantId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
