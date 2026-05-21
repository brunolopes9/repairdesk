# 61 — Segurança de Webhooks (Sprint 161)

**Pedido Bruno (2026-05-21):** "Webhooks tem que ter segurança, assinaturas e formas de
verificar que vem do sitio real. Se nao qualquer hacker consegue mexer na loja."

## Modelo de ameaça

| Ameaça | Defesa |
|---|---|
| **Atacante envia POST falso para a loja** com payload inventado | HMAC-SHA256 com secret partilhado — só o RepairDesk consegue gerar signature válida. |
| **Atacante intercepta webhook real** (ex: MitM em rede pública) e reenviA várias vezes | Sprint 161a: assinatura inclui timestamp + tolerance window 5 min. |
| **Atacante captura payload em log/cache** e reenviA tempo depois | Mesma defesa: timestamp tolerance. |
| **Timing attack** para descobrir secret bit-a-bit | `timingSafeEqual` no SDK + `HMACSHA256` em .NET (constant-time). |
| **Webhook chega 2× legitimamente** (retry depois timeout) | Sprint 161d: `X-RepairDesk-Delivery` UUID + `WebhookDeliveryDeduper` no consumer. |
| **Secret comprometido** (leak Git, log) | Botão "Rotate secret" na UI (não implementado — Sprint 162 futuro). Por agora, apagar+recriar subscription. |
| **HTTPS sem TLS** — payload em claro | Sprint 161b: enforced HTTPS na criação (excepto localhost dev). |

## Headers enviados pelo RepairDesk

```http
POST https://lopestech-shop.vercel.app/api/webhooks/repairdesk
User-Agent: RepairDesk-Webhook/1.0
Content-Type: application/json; charset=utf-8
X-RepairDesk-Event: phones.atualizado
X-RepairDesk-Delivery: 9c5a8e4f-...    # UUID, idempotency key
X-RepairDesk-Timestamp: 1716296400      # Unix seconds — NOVO (Sprint 161a)
X-RepairDesk-Signature: sha256=abc...   # HMAC(secret, "{timestamp}.{body}")
```

## Algoritmo de assinatura (Sprint 161a — BREAKING)

**Antes (Sprint 102):** `signature = HMAC-SHA256(secret, body)`
**Agora (Sprint 161a):** `signature = HMAC-SHA256(secret, "{timestamp}.{body}")`

Stripe-style. Garante que cada signature é "single-use" no sentido de que o timestamp
expira em 5 min, mesmo que o atacante intercepte.

### Implementação backend

`backend/src/RepairDesk.API/Webhooks/WebhookDeliveryHostedService.cs`:

```csharp
var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
req.Headers.Add("X-RepairDesk-Timestamp", timestamp.ToString());
req.Headers.Add("X-RepairDesk-Signature",
    SignHmac(sub.Secret, $"{timestamp}.{delivery.PayloadJson}"));
```

### Implementação consumer (loja)

Usa `Contexto/sdk/webhook-verify.ts`:

```ts
const result = verifyWebhookSignature(rawBody, sig, ts, secret);
if (!result.ok) return new Response(`Invalid: ${result.reason}`, { status: 401 });
```

`result.reason` pode ser:
- `missing_signature` / `missing_timestamp` — atacante mal-formado
- `timestamp_invalid` — não é Unix epoch
- `timestamp_too_old` — > 5 min antigo (replay)
- `timestamp_too_new` — > 60 s futuro (clock skew suspeito)
- `signature_mismatch` — payload manipulado ou wrong secret

## Idempotency (Sprint 161d)

Cada delivery tem `X-RepairDesk-Delivery: <UUID>`. Se o consumer vê 2× o mesmo UUID,
**não** deve processar 2×. Helper:

```ts
const dedupe = new WebhookDeliveryDeduper();
const deliveryId = req.headers.get('x-repairdesk-delivery') ?? '';
if (dedupe.hasSeen(deliveryId)) return new Response('OK', { status: 200 });
// ... process ...
dedupe.markSeen(deliveryId);
```

Para produção sério, substituir o `Map` em memória por Redis ou tabela DB.

## HTTPS enforcement (Sprint 161b)

`WebhookSubscriptionService.ValidateInput` rejeita URLs HTTP excepto para:
- `localhost`, `127.0.0.1`, `::1`, `host.docker.internal` (dev)

Mensagem: `"URL tem de ser HTTPS (excepto localhost para dev). Sem TLS o payload viaja em claro."`

## Body normalization (gotcha)

O HMAC assina o **raw body UTF-8**. Se o consumer faz `JSON.parse(body)` + `JSON.stringify(obj)` antes de verificar, as signatures **não batem** porque:
- Whitespace JSON difere
- Ordem de keys pode mudar
- Numbers podem ser reformatados (`1.0` → `1`)

**Regra absoluta:** verificar signature ANTES de `JSON.parse`. Consumer recebe `req.text()` (raw), valida, e SÓ DEPOIS parse.

Documentado no helper TS + neste doc.

## Rotação de secret (Sprint 162 futuro)

Hoje: apagar + recriar subscription. Não é zero-downtime.

Futuro:
- Botão "Rotate" gera novo secret
- 2 secrets activos por 24h (old + new)
- Consumer tenta verificar com new, fallback para old
- Após 24h, old é apagado
- Caso comprometimento: "Rotate now" descarta old imediatamente

## Migração do outro Claude (loja)

A mudança do Sprint 161a é **BREAKING**. O outro Claude tem que actualizar handler:

**Antes:**
```ts
const sig = req.headers.get('x-repairdesk-signature');
if (!verifyWebhookSignature(body, sig, secret)) return 401;
```

**Agora:**
```ts
const sig = req.headers.get('x-repairdesk-signature');
const ts = req.headers.get('x-repairdesk-timestamp');
const result = verifyWebhookSignature(body, sig, ts, secret);
if (!result.ok) return 401;
```

Mensagem para o outro Claude: "Sprint 161a webhook signature breaking change.
Agora assino timestamp+body (Stripe-style). Header novo `X-RepairDesk-Timestamp`.
Usa SDK `verifyWebhookSignature(body, sig, ts, secret)` em vez da versão antiga.
Tolerance default 5min."

## Comparação com referências

| Mecanismo | Stripe | GitHub | RepairDesk (Sprint 161) |
|---|---|---|---|
| HMAC-SHA256 | ✅ | ✅ | ✅ |
| Timestamp signing | ✅ | ❌ | ✅ |
| Multiple secrets (rotation) | ✅ | ❌ | ❌ (Sprint 162) |
| Idempotency key | ✅ (request-id) | ✅ (delivery-uuid) | ✅ (delivery-uuid) |
| Tolerance window | 5 min | — | 5 min |
| Constant-time compare | ✅ | ✅ | ✅ |

Estamos perto de paridade Stripe — falta apenas rotation (Sprint 162).
