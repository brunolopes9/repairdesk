namespace RepairDesk.Services.Loja;

/// <summary>
/// Stripe a fingir, só para os testes E2E (E2E__Enabled=true): não fala com a api.stripe.com nem mexe em dinheiro.
/// A «página de pagamento» é o próprio endereço de sucesso; o teste confirma o pagamento enviando o webhook
/// assinado com o segredo de teste (Stripe__WebhookSecret), tal como o Stripe faria.
/// </summary>
public sealed class StripeCheckoutE2e : IStripeCheckout
{
    public bool Configurado => true;

    public Task<StripeSessao> CriarSessaoAsync(StripeSessaoPedido pedido, CancellationToken ct = default)
        => Task.FromResult(new StripeSessao($"cs_e2e_{pedido.EncomendaId:N}", pedido.UrlSucesso));
}
