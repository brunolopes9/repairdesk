namespace RepairDesk.Core.Enums;

/// <summary>
/// Sprint 575: a que atividade pertence a venda. A loja (informática: reparações, produtos, software) é o que o
/// Mender gere; o trading (rewards de prop firms, série TRADING no Moloni) entra no IVA e nas estimativas de
/// IRS/Segurança Social, mas fica fora das listas, totais e resultado da loja.
/// </summary>
public enum VendaAtividade
{
    Loja = 0,
    Trading = 1,
}
