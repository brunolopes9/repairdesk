namespace RepairDesk.Core.Enums;

/// <summary>Sprint 656: ciclo de uma encomenda da loja online (lopestech.pt/loja).</summary>
public enum EncomendaLojaEstado
{
    /// <summary>Criada no checkout; o cliente está (ou esteve) na página de pagamento do Stripe.</summary>
    AguardaPagamento = 0,
    /// <summary>O Stripe confirmou o pagamento — a venda foi criada e o stock saiu.</summary>
    Paga = 1,
    EmPreparacao = 2,
    /// <summary>Entregue à transportadora (Asfalto Largo), com nº de seguimento.</summary>
    Enviada = 3,
    Entregue = 4,
    Cancelada = 5,
    /// <summary>O cliente não pagou dentro do prazo da sessão do Stripe.</summary>
    Expirada = 6,
}
