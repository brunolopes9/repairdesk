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
}
