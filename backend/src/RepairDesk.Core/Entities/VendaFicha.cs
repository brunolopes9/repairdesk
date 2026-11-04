using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Enums;

namespace RepairDesk.Core.Entities;

/// <summary>
/// Sprint 595: ficha de entrada de uma reparação — o que vai na ordem de reparação (2 vias) e que o cliente
/// assina (no balcão ou pelo link): IMEI, senha de desbloqueio, se permite testes, estado físico, acessórios
/// entregues e a checklist de testes à entrada e à saída. Uma por venda.
/// </summary>
public class VendaFicha : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid VendaId { get; set; }
    public Venda? Venda { get; set; }

    public string? Imei { get; set; }

    public SenhaTipo SenhaTipo { get; set; }
    /// <summary>
    /// A senha, cifrada (ISecretProtector). Nunca sai no portal nem na via do cliente; apaga-se quando a
    /// reparação é entregue ou cancelada (RGPD — só é precisa enquanto o equipamento está na loja).
    /// </summary>
    public string? SenhaCifrada { get; set; }

    /// <summary>O cliente deixa testar o equipamento (null = não respondido).</summary>
    public bool? PermiteTestes { get; set; }
    public string? EstadoFisico { get; set; }
    public string? Acessorios { get; set; }

    /// <summary>JSON: [{ "chave": "ecra", "entrada": "ok|falha|nt", "saida": "ok|falha|nt" }].</summary>
    public string ChecklistJson { get; set; } = "[]";
}
