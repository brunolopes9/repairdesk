using RepairDesk.Core.Abstractions;

namespace RepairDesk.Core.Entities;

/// <summary>
/// Sprint 659: conta de cliente da loja online (lopestech.pt/loja). Separada dos utilizadores do Mender (staff):
/// outra tabela, outro login, sem acesso a nada do Mender. Palavra-passe com o PasswordHasher do ASP.NET Identity.
/// </summary>
public class ClienteLoja : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public required string Nome { get; set; }
    /// <summary>Em minúsculas; único por empresa.</summary>
    public required string Email { get; set; }
    public string? Telefone { get; set; }
    public required string PasswordHash { get; set; }
    public int FalhasLogin { get; set; }
    public DateTime? BloqueadoAte { get; set; }
    /// <summary>SHA-256 do token de «esqueci a palavra-passe» (o token só vai no email).</summary>
    public string? ResetTokenHash { get; set; }
    public DateTime? ResetExpiraEm { get; set; }
    public DateTime? UltimoLoginEm { get; set; }
}

/// <summary>Sessão de cliente da loja: o token em claro só existe no browser; aqui guarda-se o hash.</summary>
public class ClienteLojaSessao : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid ClienteId { get; set; }
    public ClienteLoja? Cliente { get; set; }
    public required string TokenHash { get; set; }
    public DateTime ExpiraEm { get; set; }
}
