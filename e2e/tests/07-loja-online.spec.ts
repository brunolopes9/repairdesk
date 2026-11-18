import { createHmac } from 'node:crypto';
import { test, expect } from '../support/fixtures';

/**
 * Loja online (lopestech.pt) de ponta a ponta, sem dinheiro: o Stripe é o «StripeCheckoutE2e» (E2E__Enabled) e o
 * pagamento confirma-se com o webhook assinado com o segredo de teste — exatamente como o Stripe o envia.
 *
 * stock na compra → artigo → produto publicado → conta de cliente → checkout → webhook (e repetido) →
 * stock sai → página de seguimento → envio com nº de seguimento → «as minhas encomendas» → casos de erro.
 */
const SEGREDO = process.env.STRIPE_WEBHOOK_SECRET ?? 'whsec_e2e_teste';
const ESTADO = { AguardaPagamento: 0, Paga: 1, EmPreparacao: 2, Enviada: 3 } as const;

function assinar(payload: string): string {
  const t = Math.floor(Date.now() / 1000);
  const v1 = createHmac('sha256', SEGREDO).update(`${t}.${payload}`).digest('hex');
  return `t=${t},v1=${v1}`;
}

function eventoPago(sessao: string): string {
  return JSON.stringify({
    id: `evt_e2e_${Date.now()}`,
    type: 'checkout.session.completed',
    data: { object: { object: 'checkout.session', id: sessao, payment_status: 'paid', payment_intent: 'pi_e2e_1' } },
  });
}

const cliente = {
  nome: 'Cliente Loja E2E', email: 'loja.e2e@example.test', telefone: '912345678', nif: null,
  morada: 'Rua do Teste 1', codigoPostal: '3505-000', localidade: 'Viseu', notas: null,
};

test('loja online: conta, checkout, pagamento, stock, seguimento e envio', async ({ api }) => {
  await api.completeOnboarding();

  // 1) Stock e produto publicado
  const loteId = await api.createLote('E2E Cabo USB-C 60W', 3, 4.5);
  const artigo = await api.dono<{ id: string }>('POST', '/artigos', {
    nome: 'E2E Cabo USB-C 60W', categoria: null, precoVendaCents: 890, codigoBarras: null, codigoFabricante: null,
    localizacao: null, notas: null, ativo: true, loteIds: [loteId],
  });
  const produto = await api.dono<{ id: string; slug: string }>('POST', '/loja/produtos', {
    titulo: 'E2E Cabo USB-C 60W', marca: 'Rixus', modelo: null, categoria: 4, estado: 0, grau: null, garantiaMeses: 36,
    regimeIva: 0, origem: 0, fornecedorId: null, disponibilidade: 0, preco: 8.9, custo: null, cor: null, ramGb: null,
    armazenamentoGb: null, descricao: 'Cabo de teste E2E.', specs: [], slug: 'e2e-cabo-usb-c-60w', seoTitulo: null,
    seoDescricao: null, artigoId: artigo.id,
  });
  await api.dono('POST', `/loja/produtos/${produto.id}/publicar`, {});
  const { slug: loja } = await api.dono<{ slug: string }>('GET', '/automacoes/intake-widget');
  const base = `/public/loja/${loja}`;

  // 2) Conta de cliente
  const reg = await api.publico('POST', `${base}/conta/registar`, {
    nome: cliente.nome, email: cliente.email, telefone: cliente.telefone, password: 'Teste-E2E-2026!', aceitaTermos: true,
  });
  expect(reg.status(), await reg.text()).toBe(200);
  const sessao = ((await reg.json()) as { token: string }).token;
  const comSessao = { 'X-Loja-Sessao': sessao };

  // 3) Erros que o checkout tem de recusar (nada é criado)
  const pedido = (extra: Record<string, unknown> = {}) => ({
    linhas: [{ slug: produto.slug, quantidade: 2 }], ...cliente, aceitaTermos: true, website: null, ...extra,
  });
  expect((await api.publico('POST', `${base}/checkout`, pedido({ aceitaTermos: false }))).status()).toBe(400);
  expect((await api.publico('POST', `${base}/checkout`, pedido({ website: 'http://spam' }))).status()).toBe(400);
  expect((await api.publico('POST', `${base}/checkout`, pedido({ codigoPostal: '3505' }))).status()).toBe(400);
  const demasiado = await api.publico('POST', `${base}/checkout`, pedido({ linhas: [{ slug: produto.slug, quantidade: 5 }] }));
  expect(demasiado.status()).toBe(400);
  expect(await demasiado.text()).toContain('stock');
  expect((await api.publico('POST', `/public/loja/loja-que-nao-existe/checkout`, pedido())).status()).toBe(404);

  // 4) Checkout válido (com sessão de cliente)
  const co = await api.publico('POST', `${base}/checkout`, pedido(), comSessao);
  expect(co.status(), await co.text()).toBe(200);
  const { referencia, urlPagamento } = (await co.json()) as { referencia: string; urlPagamento: string };
  const t = new URL(urlPagamento).searchParams.get('t');
  expect(t, 'o link de seguimento tem de trazer o token').toBeTruthy();

  const seguir = () => api.publico('GET', `${base}/encomendas/${referencia}?t=${encodeURIComponent(t!)}`);
  let estado = (await (await seguir()).json()) as { estado: number; total: number; portes: number; email?: string };
  expect(estado.estado).toBe(ESTADO.AguardaPagamento);
  expect(estado.total).toBeCloseTo(2 * 8.9 + 5, 2); // preço do servidor + 5 € de portes CTT
  expect((await api.publico('GET', `${base}/encomendas/${referencia}?t=errado`)).status()).toBe(404);
  expect(await api.stockDoLote(loteId)).toBe(3); // ainda não pago: o stock não sai

  // 5) Pagamento: webhook assinado; assinatura falsa é recusada; o mesmo evento repetido não paga duas vezes
  const encomendas = await api.dono<{ items?: { id: string; referencia: string }[] } | { id: string; referencia: string }[]>('GET', '/loja/encomendas');
  const lista = Array.isArray(encomendas) ? encomendas : encomendas.items ?? [];
  const enc = lista.find((e) => e.referencia === referencia);
  expect(enc, 'a encomenda aparece na lista do dono').toBeTruthy();
  const sessaoStripe = `cs_e2e_${enc!.id.replace(/-/g, '')}`;
  const payload = eventoPago(sessaoStripe);

  const falso = await api.publico('POST', '/public/loja/stripe/webhook', JSON.parse(payload), { 'Stripe-Signature': 't=1,v1=abc' });
  expect(falso.status()).toBe(400);

  for (let i = 0; i < 2; i++) {
    const wh = await api.request.post(`${process.env.E2E_API_URL ?? 'http://localhost:5080/api'}/public/loja/stripe/webhook`, {
      headers: { 'Stripe-Signature': assinar(payload), 'Content-Type': 'application/json' }, data: payload,
    });
    expect(wh.status(), await wh.text()).toBe(200);
  }
  estado = (await (await seguir()).json()) as typeof estado;
  expect(estado.estado).toBe(ESTADO.Paga);
  expect(estado.email).toBe(cliente.email); // vai para o convite do Google Avaliações do Consumidor
  expect(await api.stockDoLote(loteId)).toBe(1); // saíram 2 — e só uma vez, apesar do webhook repetido

  // 6) Envio com nº de seguimento → aparece na página pública
  await api.dono('PUT', `/loja/encomendas/${enc!.id}/estado`, { estado: ESTADO.EmPreparacao, numeroSeguimento: null });
  await api.dono('PUT', `/loja/encomendas/${enc!.id}/estado`, { estado: ESTADO.Enviada, numeroSeguimento: 'RR123456789PT' });
  const enviada = (await (await seguir()).json()) as { estado: number; numeroSeguimento: string | null };
  expect(enviada.estado).toBe(ESTADO.Enviada);
  expect(enviada.numeroSeguimento).toBe('RR123456789PT');

  // 7) «As minhas encomendas» na conta do cliente
  const minhas = await api.publico('GET', `${base}/conta/encomendas`, undefined, comSessao);
  expect(minhas.status()).toBe(200);
  expect(((await minhas.json()) as { referencia: string }[]).map((e) => e.referencia)).toContain(referencia);
  expect((await api.publico('GET', `${base}/conta/encomendas`)).status()).toBeGreaterThanOrEqual(400);
});
