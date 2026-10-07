namespace RepairDesk.Core.Enums;

/// <summary>
/// Doc 94 Fase 4: estados de uma venda (reparação, serviço ou produto). Orçamento não mexe no
/// stock; ao sair de Orçamento as unidades saem dos lotes; Cancelada devolve-as.
/// </summary>
public enum VendaEstado
{
    Orcamento = 0,
    AEsperaPeca = 1,
    Pronta = 2,
    /// <summary>Entregue e paga — a data da venda (<c>Venda.Data</c>) é este momento.</summary>
    Entregue = 3,
    Cancelada = 4,
    /// <summary>Orçamento aceite, trabalho a decorrer ("Em reparação" nas reparações). Valor 5 por compatibilidade.</summary>
    EmCurso = 5,
}
