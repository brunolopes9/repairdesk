namespace RepairDesk.Core.Enums;

/// <summary>
/// Sprint 438 (Doc 91 follow-up): canal pelo qual o pedido entrou na inbox.
/// Widget é o default (pedidos via /pedido/{slug}). Os outros canais existem
/// para staff registar leads que entram por outro lado e quer ter na mesma inbox.
/// </summary>
public enum RepairRequestOrigem
{
    Widget = 0,
    Telefone = 1,
    Email = 2,
    WhatsApp = 3,
    BalcaoFisico = 4,
    Outro = 5,
}
