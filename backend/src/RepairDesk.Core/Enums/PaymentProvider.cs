namespace RepairDesk.Core.Enums;

/// <summary>
/// Sprint 303: provider de pagamento que executa a transacção. Distinto do
/// <see cref="PaymentMethod"/> (que é o instrumento: dinheiro, cartão, MBWay).
/// </summary>
public enum PaymentProvider
{
    /// <summary>Pagamento manual sem provider (dinheiro, cartão físico, transferência manual).</summary>
    Manual = 0,

    /// <summary>Provider mock para dev — aprova automaticamente. Nunca usar em produção.</summary>
    Mock = 1,

    /// <summary>IFTHENPAY — gateway PT para MBWay e referências Multibanco.</summary>
    Ifthenpay = 2,
}
