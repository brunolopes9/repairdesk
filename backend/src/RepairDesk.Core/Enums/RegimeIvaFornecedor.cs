namespace RepairDesk.Core.Enums;

/// <summary>
/// Regime de IVA das compras a um fornecedor. Define a taxa de IVA por defeito das linhas de
/// compra (SPEC compras §2.1) e se há autoliquidação.
/// </summary>
public enum RegimeIvaFornecedor
{
    /// <summary>Fornecedor português — fatura com IVA (23%), IVA dedutível.</summary>
    Nacional = 0,

    /// <summary>
    /// Fornecedor de outro país da UE, compra com o NIF/VAT PT — fatura a 0% e o adquirente
    /// autoliquida (liquida e deduz o mesmo valor: efeito 0 €, mas declarado).
    /// </summary>
    UeAutoliquidacao = 1,

    /// <summary>Fornecedor fora da UE (importação) — IVA pago na alfândega.</summary>
    ForaUe = 2,

    /// <summary>
    /// Sprint 576: fornecedor de outro país da UE que cobrou o IVA do país dele (ex.: Inter Movil, 21% espanhol).
    /// Esse IVA não se deduz em Portugal nem há autoliquidação: o custo é o total pago.
    /// </summary>
    UeComIvaLocal = 3,
}
