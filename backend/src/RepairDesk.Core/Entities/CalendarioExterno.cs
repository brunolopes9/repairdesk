using RepairDesk.Core.Abstractions;

namespace RepairDesk.Core.Entities;

/// <summary>
/// Sprint 619 (Doc 98 F4): calendário de fora (Google, Outlook, iCloud) que o utilizador quer ver no Mender.
/// Liga-se pelo endereço iCal secreto — só leitura, sem acesso à conta. O endereço é um segredo: fica cifrado
/// (<see cref="UrlCifrada"/>) e a API só devolve o domínio.
/// </summary>
public class CalendarioExterno : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public required string Nome { get; set; }
    public required string UrlCifrada { get; set; }
    public required string Dominio { get; set; }
    public bool Ativo { get; set; } = true;
    public DateTime? UltimaLeitura { get; set; }
    public string? UltimoErro { get; set; }
}
