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

    /// <summary>
    /// Sprint 176: flag para distinguir COGS (peça consumida em reparação) de OpEx
    /// (despesas operacionais reais: rent, água, ferramentas, …).
    ///
    /// true → criada automaticamente quando peça do stock vai para reparação. NÃO conta
    ///         como despesa OpEx no relatório IVA (já está contada via PartMovimento).
    /// false (default) → despesa operacional pura. Conta para IVA dedutível OpEx + Dashboard.
    ///
    /// Conceitos separados (ChatGPT validou):
    ///   Stock = inventário (PartMovimento)
    ///   COGS  = peças consumidas em reparações (Despesa IsCogs=true) — não OpEx!
    ///   OpEx  = despesas operacionais reais (Despesa IsCogs=false)
    /// </summary>
    public bool IsCogs { get; set; }

    /// <summary>
    /// Sprint 308: despesas recorrentes (renda, EDP, software SaaS).
    /// </summary>
    public bool IsRecorrente { get; set; }

    /// <summary>
    /// Periodicidade em meses para recorrencia. Valores esperados: 1, 3 ou 12.
    /// </summary>
    public int? PeriodicidadeMeses { get; set; }

    /// <summary>
    /// Sprint 525: compra a fornecedor intra-UE (autoliquidação). Quando true, NÃO conta como IVA
    /// dedutível normal no Relatório IVA (reverse charge = efeito zero; IVA estrangeiro não é
    /// dedutível em PT). Carimbado na aprovação de fatura a partir de Fornecedor.IntraUe.
    /// </summary>
    public bool ReverseCharge { get; set; }
}
