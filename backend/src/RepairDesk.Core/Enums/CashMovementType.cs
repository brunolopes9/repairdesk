namespace RepairDesk.Core.Enums;

/// <summary>
/// Sprint 300 (Doc 80 Pillar A.1): tipos de movimento de caixa.
/// AmountCents é sempre positivo; o sinal vem do Type.
/// </summary>
public enum CashMovementType
{
    /// <summary>Entrada de cliente (venda, pagamento reparação).</summary>
    PagamentoCliente = 0,

    /// <summary>Reforço de caixa — operador mete dinheiro do banco/cofre.</summary>
    Reforco = 1,

    /// <summary>Sangria — operador tira dinheiro para banco/cofre/despesa.</summary>
    Sangria = 2,

    /// <summary>Despesa paga em dinheiro da caixa (ex.: pagar café, papel).</summary>
    DespesaCaixa = 3,

    /// <summary>Troco — dado a cliente. Subtrai do saldo dinheiro.</summary>
    Troco = 4,

    /// <summary>Ajuste manual com justificação (não corre cálculo automático).</summary>
    AjusteManual = 5,
}

public enum DailyClosingStatus
{
    Open = 0,
    Closed = 1,
    /// <summary>Reabriu por engano e foi fechado pela 2ª vez — para audit.</summary>
    Reopened = 2,
}
