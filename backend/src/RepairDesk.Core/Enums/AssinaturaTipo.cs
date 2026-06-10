namespace RepairDesk.Core.Enums;

/// <summary>Sprint 551 (Doc 80/93): momento em que a assinatura do cliente foi recolhida.</summary>
public enum AssinaturaTipo
{
    /// <summary>Ao deixar o equipamento — aceita os termos do comprovativo de entrada.</summary>
    Entrada = 0,

    /// <summary>Ao levantar o equipamento — confirma a entrega (recibo de entrega).</summary>
    Entrega = 1,
}
