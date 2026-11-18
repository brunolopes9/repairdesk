namespace RepairDesk.Services.Loja;

/// <summary>
/// Stripe a fingir, só para os testes E2E (E2E__Enabled=true): não fala com a api.stripe.com nem mexe em dinheiro.
/// A «página de pagamento» é o próprio endereço de sucesso; a sessão tem o formato do ifthenpay, e o teste confirma o
/// pagamento chamando o callback do ifthenpay com a chave anti-phishing de teste (ou o webhook assinado do Stripe).
/// </summary>
public sealed class StripeCheckoutE2e : IStripeCheckout
{
    public Task<bool> ConfiguradoAsync(CancellationToken ct = default) => Task.FromResult(true);

    public Task<StripeSessao> CriarSessaoAsync(StripeSessaoPedido pedido, CancellationToken ct = default)
        => Task.FromResult(new StripeSessao(IfthenpayLojaCheckout.SessaoDe(pedido.Referencia), pedido.UrlSucesso));
}
