# 53 — External API SDK para a loja online

Documento de referência para integração entre `shop.lopestech.pt` (loja online de telemóveis refurbished, projeto Next.js em `~/Desktop/LopesTech/ecommerce`) e o RepairDesk backend (`api.lopestech.pt`).

## TL;DR

Copia `Contexto/sdk/repairdesk-client.ts` para o projeto Next.js da loja em `src/lib/repairdesk/client.ts` e usa **apenas em server-side** (route handlers, API routes, server actions). Nunca exponhas a API key no client.

## Setup na loja online

### 1. Criar API key no RepairDesk

1. Login no painel admin
2. Definições → Chaves de API → **Nova chave**
3. Nome: `Loja online produção`
4. Copia o plain key (mostrado UMA VEZ). Formato: `rd_live_xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx`

### 2. `.env.local` da loja Next.js

```
REPAIRDESK_API_URL=https://api.lopestech.pt
REPAIRDESK_API_KEY=rd_live_xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx
```

### 3. Singleton

```ts
// src/lib/repairdesk/index.ts
import { RepairDeskClient } from './client';

let _client: RepairDeskClient | null = null;
export function repairdesk() {
  _client ??= new RepairDeskClient({
    baseUrl: process.env.REPAIRDESK_API_URL!,
    apiKey: process.env.REPAIRDESK_API_KEY!,
  });
  return _client;
}
```

## Fluxos

### Fluxo 1 — Checkout após Stripe

```ts
// app/api/stripe/webhook/route.ts
const order = await repairdesk().checkout({
  cliente: {
    nome: session.customer_details!.name!,
    email: session.customer_details!.email!,
    nif: session.metadata?.nif ?? null,
  },
  items: session.line_items!.data.map(li => ({
    descricao: li.description,
    quantidade: li.quantity!,
    precoUnitarioCents: li.amount_subtotal,
    descontoCents: 0,
    ivaRate: 23,
    imei: (li.price?.product as any)?.metadata?.imei ?? null,
  })),
  paymentMethod: 4,
  emitirFatura: true,
});

await db.update(orders).set({
  repairdeskVendaId: order.vendaId,
  repairdeskGarantiaSlug: order.garantiaSlug,
  faturaPdfUrl: order.faturaPdfUrl,
}).where(eq(orders.id, sessionId));
```

### Fluxo 2 — Devolução 14d (DL 24/2014)

```ts
// app/api/orders/[id]/cancel/route.ts
const result = await repairdesk().cancelOrder(
  order.repairdeskVendaId,
  'Cliente exerceu direito 14d (DL 24/2014)',
);
await stripe.refunds.create({ payment_intent: order.stripePaymentIntentId });
```

### Fluxo 3 — Cross-sell de acessórios

```ts
// PDP iPhone — sugerir capas/carregadores
const acessorios = await repairdesk().listParts({ categoria: 11, pageSize: 6 });
```

**Pattern recomendado**: cache num KV/Redis com TTL 1h, não chamada por PDP.

### Fluxo 4 — Página "Os meus pedidos"

**Variante A** — polling por venda específica:
```ts
const status = await repairdesk().getOrder(order.repairdeskVendaId);
if (status.garantiaActiva) {
  // link shop.lopestech.pt/garantia/{status.garantiaSlug}
}
```

**Variante B** — histórico completo por NIF (recomendado para dashboard):
```ts
// app/conta/page.tsx — Server Component
const session = await getSession();
if (!session?.user.nif) redirect('/entrar');

const historico = await repairdesk().getHistoricoByNif(session.user.nif);
if (!historico) {
  return <EmptyState>Ainda não tens encomendas.</EmptyState>;
}

return (
  <>
    <h2>Compras ({historico.vendas.length})</h2>
    {historico.vendas.map(v => (
      <OrderRow key={v.id} numero={v.numero} total={v.totalCents}
        status={v.status} fatura={v.faturaPdfUrl} />
    ))}
    <h2>Garantias activas</h2>
    {historico.garantiasActivas.map(g => (
      <a key={g.slug} href={`https://app.lopestech.pt/g/${g.slug}`}>
        {g.equipamento} — {g.diasRestantes} dias restantes
      </a>
    ))}
  </>
);
```

**Vantagem da variante B:** zero replicação de orders na BD da loja.
RepairDesk continua single source. Login da loja só precisa do NIF do cliente
no `users_auth.nif` para o lookup.

## Error handling

```ts
import { RepairDeskError } from '@/lib/repairdesk/client';

try {
  await repairdesk().checkout(req);
} catch (e) {
  if (e instanceof RepairDeskError) {
    if (e.status === 401) { /* API key revogada */ }
    if (e.status === 422) { /* validação — code disponível em e.code */ }
    if (e.isTransient)    { /* o client já fez retry */ }
  }
  throw e;
}
```

## Segurança

- **Server-side only.** A API key dá acesso TOTAL ao tenant LopesTech. Nunca em componentes `'use client'`.
- **Não logues** o valor.
- **Rotação**: cria nova key, actualiza Vercel env, revoga antiga. UI mostra ambas até confirmares.

## Idempotência

| Endpoint | Idempotente? |
|---|---|
| `checkout` | ❌ — usa Stripe event.id como dedup na BD da loja |
| `getOrder` | ✅ (read) |
| `cancelOrder` | ✅ — 2x devolve mesmo estado |
| `lookup-or-create cliente` (interno do checkout) | ✅ por NIF |

## Endpoints (resumo)

| Método | Caminho | Para quê |
|---|---|---|
| POST | `/api/external/checkout` | Fechar venda atómica |
| GET | `/api/external/orders/{id}` | Estado venda |
| POST | `/api/external/orders/{id}/cancel` | Devolução 14d |
| GET | `/api/external/parts` | Catálogo acessórios |
| GET | `/api/external/clientes/{nif}/historico` | Histórico cliente (Os meus pedidos) |
| GET | `/api/external/garantias/{slug}` | Detalhe da garantia (não mascarado, integração trusted) |

Header: `X-Api-Key: rd_live_xxx` ou `Authorization: ApiKey rd_live_xxx`.

## Não disponível ainda

- Webhooks outbound (RepairDesk → loja) para eventos garantia/fatura
- Reservas de stock atómicas (anti-oversell)
- API keys com scopes restritos (key = acesso total ao tenant)

## Referências

- Implementação: `backend/src/RepairDesk.API/Controllers/ExternalController.cs`
- Service: `backend/src/RepairDesk.Services/External/ExternalCheckoutService.cs`
- Tests: `backend/tests/RepairDesk.Tests/External/ExternalCheckoutApiTests.cs`
- SDK code: `Contexto/sdk/repairdesk-client.ts`
- DL 24/2014: https://dre.pt/dre/legislacao-consolidada/decreto-lei/2014-58319850
- DL 84/2021: https://diariodarepublica.pt/dr/detalhe/decreto-lei/84-2021-172938301
