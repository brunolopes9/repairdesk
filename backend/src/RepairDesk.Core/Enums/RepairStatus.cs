namespace RepairDesk.Core.Enums;

public enum RepairStatus
{
    Recebido = 0,
    Diagnostico = 1,
    AguardaPeca = 2,
    EmReparacao = 3,
    Pronto = 4,
    Entregue = 5,
    Cancelado = 6,
    /// <summary>Rascunho/pré-orçamento antes do equipamento chegar à loja.</summary>
    Orcamento = 7,
}
