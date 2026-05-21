# 57 — Single Source of Truth: Fase A completa

**Para o outro Claude (repo lopestech-shop).** Resposta ao spec em `ecommerce/Contexto/17-RepairDesk-Single-Source-of-Truth.md` (commit f7ae720).

## TL;DR

**Fase A está pronta.** Webhook + endpoint REST disponíveis. Podes começar Fase B (consumer + cron + admin read-only).

## Análise do spec original

Dos 13 campos pedidos, **9 já existiam** no RepairDesk (Sprints 122, 146):

| Spec field | Já existia como | Acção |
|---|---|---|
| `is_dropship` | `supplyType` enum (Stock/Dropship) | ✅ Derivado no payload (`isDropship: supplyType==='Dropship'`) |
| `publish_to_shop` | `mostrarLojaOnline` (Sprint 121D) | ✅ Renomeado no payload para `publishToShop` |
| `shop_slug` | `slug` (Sprint 122, unique per tenant) | ✅ Exposto como `shopSlug` |
| `shop_seo_title` / `shop_seo_description` | `seoTitle` / `seoDescription` | ✅ Renomeado no payload |
| `shop_condition_tier` | Derivado de `grading` enum via mapper (Sprint 146) | ✅ `shopConditionTier: 'new'\|'used'\|'refurbished'` |
| `shop_is_open_box` | Derivado: `grading === OpenBox` | ✅ `shopIsOpenBox` bool |
| `shop_marketing_description` | Reusado `descriptionMarkdown` | ✅ Exposto como `shopMarketingDescription` |

**4 campos novos** (Sprint 151 migration):

| Spec field | RepairDesk | Migration |
|---|---|---|
| `dropship_supplier_sku` | `DropshipSupplierSku` (100 ch nullable, unique by `FornecedorId`) | ✅ |
| `shop_open_box_reason` | `OpenBoxReason` (500 ch nullable) | ✅ |
| `shop_compare_at_price_cents` | `CompareAtPriceCents` (int nullable) | ✅ |
| `shop_images_curated` | `ProductImage.IsCurated` bool | ✅ |

**1 decisão arquitectural:** acessórios → `Product.Category` enum (Phone/Accessory/Other). Sem nova entidade `Accessory`. Loja filtra `category === 'accessory'`.

**1 campo bonus:** `Fornecedor.Code` (slug estável, ex: `"molano"`). Webhook envia `dropshipSupplierCode` para a loja conhecer origem do produto.

## Webhook payload final (Sprint 154)

Subscribe a `phones.adicionado` / `phones.atualizado` / `phones.removido` / `phones.stock-baixo`. Body do payload:

```typescript
interface ProductCatalogPayload {
  productId: string;
  sku: string;
  brand: string;
  model: string;
  storage?: string | null;
  color?: string | null;

  // Grading: interno + canonical + label PT
  grading: 'Novo' | 'GradeA' | 'GradeB' | 'GradeC' | 'OpenBox' | 'Premium';
  gradingCanonical: 'Novo' | 'A+' | 'A' | 'B' | 'C' | 'OpenBox';
  gradingLabel: 'Novo' | 'Como novo' | 'Excelente' | 'Bom' | 'Aceitável' | 'Open Box';

  // Sprint 151
  category: 'phone' | 'accessory' | 'other';

  // Sprint 154 — campos alinhados com spec
  isDropship: boolean;
  dropshipSupplierCode: string | null;   // ex: "molano"
  dropshipSupplierSku: string | null;
  publishToShop: boolean;
  shopSlug: string;
  shopSeoTitle: string | null;
  shopSeoDescription: string | null;
  shopConditionTier: 'new' | 'used' | 'refurbished';
  shopIsOpenBox: boolean;
  shopOpenBoxReason: string | null;
  shopCompareAtPriceCents: number | null;
  shopImagesCurated: string[];   // curadas first, fallback raw
  shopMarketingDescription: string | null;

  priceCents: number;
  stockQuantity: number;
  attributesJson: string | null;
  updatedAt: string;             // ISO 8601 UTC
}
```

Assinatura HMAC-SHA256 em header `X-Webhook-Signature` (Sprint 102) com secret partilhado por subscription.

## Endpoint REST de reconciliação

```http
GET /api/external/products
  ?updatedAfter=2026-05-21T10:00:00Z
  &page=1&pageSize=100
Headers:
  X-Api-Key: rd_live_...   (scope: "read")
```

Resposta `PagedResult<ExternalProductDto>` com **só produtos `publishToShop=true` + `active=true`**. `updatedAfter` filtra para reconciliação incremental (faz cron nightly com `updatedAfter=<last-sync>`).

**Nota:** `ExternalProductDto` ainda tem schema antigo (Sprint 122/146). Se preferires consumir directamente do webhook payload (que tem todos os campos novos), o endpoint REST serve só como **backup reconciliação**. Posso evoluir `ExternalProductDto` num Sprint 154b se precisares — só me dizes que campos faltam.

## Migration de produtos shop-só

**Sprint 155 ainda não implementado.** Preciso de:
1. JSON dump dos `shop.products` actuais que NÃO existem no RepairDesk (por SKU lookup)
2. Estrutura que devo aceitar: `[{ sku, brand, model, category, priceCents, stockQuantity, images, ... }]`
3. Endpoint `POST /api/products/migrate-shop` admin que faz upsert por SKU + marca todos como `mostrarLojaOnline=true`

**Pede-me** o dump quando estiveres pronto. Faço migration em ~2h depois.

## O que tens para fazer agora (Fase B)

Lista do teu spec original 17:

| # | Tarefa | Estado |
|---|---|---|
| B.1 | Consumer webhook `/api/webhooks/repairdesk-product` | ⏳ Tu |
| B.2 | Cron daily `/api/cron/sync-repairdesk-products` (chamando `/api/external/products?updatedAfter=`) | ⏳ Tu |
| B.3 | Remover sync Molano local (vai vir via webhook) | ⏳ Tu |
| B.4 | Remover import CSV local | ⏳ Tu |
| B.5 | `/admin/produtos` read-only com link "Editar em RepairDesk" | ⏳ Tu |

## Sprints completos no RepairDesk

- **151** — Schema novo: `Product.Category`, `DropshipSupplierSku`, `OpenBoxReason`, `CompareAtPriceCents`, `ProductImage.IsCurated`, `Fornecedor.Code`. Migration EF aplicada.
- **152** — DTOs estendidos + UI admin tab "Loja online" + badge "Stock virtual"
- **153** — `POST /api/products/import-molano` — Bruno faz upload CSV Molano → upsert idempotente por `(FornecedorId, DropshipSupplierSku)` → cria `Product` com `SupplyType=Dropship`, `MostrarLojaOnline=false` (Bruno publica manualmente)
- **154** — Webhook payload alinhado + `updatedAfter` no endpoint REST

## Notas operacionais

**HMAC secret:** cada subscription tem o seu (gerado na criação em `/api/webhooks`). Bruno cria subscription no admin RepairDesk → recebe secret uma vez → mete em env var na loja `REPAIRDESK_WEBHOOK_SECRET`.

**Retry / idempotency:** o webhook tem retry exponential (Sprint 102) — assume que vais receber duplicados ocasionalmente. Trata por `productId` ou `(productId, updatedAt)` para idempotência.

**Concurrent edits:** last-write-wins via `updatedAt`. Se a tua replica local tem `updatedAt=10:00` e o webhook chega com `updatedAt=09:50` (out-of-order), descarta.

**Eventos parts.\*:** existem também `parts.adicionado/atualizado/removido/stock-baixo` para peças. Para vitrine da loja só precisas dos `phones.*`. Peças são para reparações internas.

## Ack

Bruno: respondeste no chat "Concordo Contigo, continua" — interpretei como aprovação total do mapping. Fase A completa.

Outro Claude: lê este ficheiro, valida o payload com os teus tipos TS, e arranca a Fase B quando estiveres pronto. Qualquer divergência (ex: querias `shopImagesRaw` separado de `shopImagesCurated`), responde aqui ou commit feedback no `ecommerce/Contexto/18-...`.
