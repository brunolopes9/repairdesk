# 54 — Webhooks para a loja online

Documento de referência para o projeto Next.js da loja em `~/Desktop/LopesTech/ecommerce` consumir eventos do RepairDesk em tempo real.

## TL;DR

O RepairDesk faz POST para um endpoint teu sempre que algo relevante acontece (garantia emitida, venda cancelada, reparação concluída, etc). Tu verificas o HMAC, processas, e respondes 2xx.

- **6 eventos** ligados (Sprint 103/105). `garantia.expirada` pendente (precisa de cron).
- **Assinatura:** `X-RepairDesk-Signature: sha256=<hex>` calculado em `HMAC-SHA256(secret, body)`.
- **Retry:** 5 tentativas com backoff `1m → 5m → 30m → 2h → 12h`. Depois marca `Failed`. Após 10 failures consecutivos, a subscription é auto-desactivada.
- **Idempotência:** `X-RepairDesk-Delivery: <guid>` único por tentativa. Persiste para evitar processar 2x.

## Setup

1. RepairDesk admin → **Definições → Webhooks** → Novo webhook
2. URL: `https://shop.lopestech.pt/api/webhooks/repairdesk` (HTTPS obrigatório)
3. Eventos: escolhe os que queres ouvir
4. Copia o **secret** mostrado uma vez (formato `whsec_<base64url>`)
5. Guarda no Vercel env: `REPAIRDESK_WEBHOOK_SECRET=whsec_...`

## Verificar assinatura (Next.js)

```ts
// app/api/webhooks/repairdesk/route.ts
import { createHmac, timingSafeEqual } from 'node:crypto';

export async function POST(req: Request) {
  const body = await req.text();  // RAW body antes de JSON.parse!
  const signature = req.headers.get('x-repairdesk-signature') ?? '';
  const deliveryId = req.headers.get('x-repairdesk-delivery') ?? '';
  const eventType = req.headers.get('x-repairdesk-event') ?? '';

  // Strip "whsec_" prefix do secret antes de HMAC
  const secret = process.env.REPAIRDESK_WEBHOOK_SECRET!.replace(/^whsec_/, '');
  const expected = 'sha256=' + createHmac('sha256', secret).update(body).digest('hex');

  if (!timingSafeEqual(Buffer.from(signature), Buffer.from(expected))) {
    return new Response('Invalid signature', { status: 401 });
  }

  // Idempotency — se já processámos este deliveryId, responde 200 e sai
  if (await db.webhookEvents.findOne({ deliveryId })) {
    return new Response('Already processed', { status: 200 });
  }

  const event = JSON.parse(body);
  await handleEvent(eventType, event.data);
  await db.webhookEvents.insert({ deliveryId, eventType, processedAt: new Date() });

  return new Response('OK', { status: 200 });
}
```

**Crítico:** verifica HMAC com o body **raw**, antes de qualquer parse. Reformatar JSON quebra a assinatura.

## Envelope (igual em todos os eventos)

```json
{
  "id": "0193a4b8-...",
  "event": "garantia.emitida",
  "tenantId": "0193a4b8-...",
  "createdAt": "2026-05-19T22:30:00Z",
  "data": { /* payload específico do evento, ver abaixo */ }
}
```

## Eventos disponíveis

### `garantia.emitida`
Quando uma venda é marcada como paga, garantia DL 84/2021 é auto-criada.

```json
{
  "garantiaId": "uuid",
  "slug": "abc123def456",
  "origem": "Venda",
  "vendaId": "uuid",
  "vendaNumero": 42,
  "clienteId": "uuid",
  "dataInicio": "2026-05-19T...",
  "dataFim": "2029-05-19T...",
  "diasGarantia": 1095
}
```

**Caso de uso loja:** envia email com PDF garantia ao cliente. Link: `https://app.lopestech.pt/g/{slug}` ou descarrega PDF em `https://api.lopestech.pt/api/public/warranty/{slug}/pdf`.

### `garantia.anulada`
Admin anulou manualmente.

```json
{
  "garantiaId": "uuid",
  "slug": "abc123",
  "origem": "Venda" | "Reparacao",
  "vendaId": "uuid" | null,
  "reparacaoId": "uuid" | null,
  "motivo": "Equipamento devolvido em 14d"
}
```

### `venda.criada`
Sempre que uma venda é criada (rascunho inclusive).

```json
{
  "vendaId": "uuid",
  "vendaNumero": 42,
  "clienteId": "uuid",
  "origem": "Balcao" | "Online" | "Importacao",
  "totalCents": 30000,
  "status": "EmAberto"
}
```

### `venda.paga`
Estado mudou para `Paga` (stock decrementado, garantia emitida em seguida).

```json
{
  "vendaId": "uuid",
  "vendaNumero": 42,
  "clienteId": "uuid",
  "totalCents": 30000,
  "paymentMethod": "Cartao",
  "data": "2026-05-19T..."
}
```

### `venda.cancelada`
Admin cancelou a venda (Sprint 54 one-click). Fatura é anulada + stock revertido.

```json
{
  "vendaId": "uuid",
  "vendaNumero": 42,
  "clienteId": "uuid",
  "totalCents": 30000,
  "invoiceNumber": "FT 2026/123" | null
}
```

**Caso de uso loja:** processa refund Stripe + arquiva pedido.

### `reparacao.concluida`
Reparação foi marcada como Entregue.

```json
{
  "reparacaoId": "uuid",
  "reparacaoNumero": 7,
  "clienteId": "uuid",
  "equipamento": "iPhone 12",
  "imei": "356938035..." | null,
  "precoFinalCents": 5000,
  "entregueEm": "2026-05-19T..."
}
```

**Caso de uso loja:** se o IMEI bate com um item vendido na loja, envia notificação ao cliente que a reparação ao abrigo da garantia foi concluída.

### `garantia.expirada` (pendente)
Cron diário vai publicar isto quando `DataFim < now` e ainda não notificado. Sprint futuro.

## Test local

1. Usa [webhook.site](https://webhook.site) para obter um endpoint público temporário (gera URL com UUID)
2. Cria webhook no RepairDesk apontando para esse URL
3. Marca uma venda como paga (`POST /api/vendas/{id}/marcar-paga`)
4. Em webhook.site vês o POST chegar com headers e body

Para teste com a tua loja Next.js local: usa [ngrok](https://ngrok.com) ou `cloudflared tunnel` para expor `localhost:3000` ao RepairDesk.

## Debug

Painel `/definicoes/webhooks` mostra últimas 50 entregas por subscription:
- Status (Pending/Delivered/Failed)
- Código HTTP do receptor
- Erro (se falhou)
- Payload completo
- Botão **Retry** para reagendar entregas Failed

## Limites e considerações

- **Timeout HTTP:** 15s por entrega. Se a tua handler demora mais, responde 202 e processa async.
- **Auto-disable:** 10 failures consecutivos → subscription DisabledAt. Reactivas em Definições → Webhooks → Editar → Active.
- **Sem reprocess automático em massa:** se a tua loja esteve offline, podes ir manualmente reentregar failures. Cron de reprocess está fora do MVP.
- **Sem replay de eventos antigos:** se subscreves agora, só recebes a partir daqui (não há "from beginning").

## Referências

- Implementação:
  - `backend/src/RepairDesk.Services/Webhooks/` (entity + publisher + service)
  - `backend/src/RepairDesk.API/Webhooks/WebhookDeliveryHostedService.cs` (processor)
- Eventos publicados: `VendaService`, `GarantiaService`, `ReparacaoService`
- DL 84/2021 (garantia 3 anos): https://diariodarepublica.pt/dr/detalhe/decreto-lei/84-2021-172938301
