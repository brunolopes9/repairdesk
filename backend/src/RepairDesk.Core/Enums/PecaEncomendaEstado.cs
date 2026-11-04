namespace RepairDesk.Core.Enums;

/// <summary>
/// Sprint 594: peça de uma reparação que ainda não está em stock (ex.: "Ecrã iPad 9", custa ~30 €, vende-se a 60 €).
/// Fica na reparação com o custo estimado (o lucro já se vê); quando chega, a linha passa a usar o lote do stock.
/// </summary>
public enum PecaEncomendaEstado
{
    /// <summary>Ainda é preciso encomendar.</summary>
    PorEncomendar = 0,
    /// <summary>Já se encomendou — à espera que chegue.</summary>
    Encomendada = 1,
}
