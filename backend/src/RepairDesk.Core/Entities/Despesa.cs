using RepairDesk.Core.Abstractions;
using RepairDesk.Core.Enums;

namespace RepairDesk.Core.Entities;

public class Despesa : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public required string Descricao { get; set; }
    public DespesaCategoria Categoria { get; set; } = DespesaCategoria.Outro;
    public int ValorCents { get; set; }
    public DateTime Data { get; set; } = DateTime.UtcNow;
    public string? Fornecedor { get; set; }
    public string? NumeroEncomenda { get; set; }
    public string? Notas { get; set; }

    public Guid? TrabalhoId { get; set; }
    public Trabalho? Trabalho { get; set; }

    public Guid? ReparacaoId { get; set; }
    public Reparacao? Reparacao { get; set; }
}
