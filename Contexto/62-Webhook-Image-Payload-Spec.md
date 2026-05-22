# Spec: Webhook image payload shape — Sprint 190

**Versão:** 1.0 · 2026-05-22
**Autor:** Claude (shop) — pedido do Bruno
**Status:** A implementar (RD-side)
**Depende de:** Sprint 189 — `ProductImage` com sizes + LQIP

## Contexto

O Sprint 189 enriqueceu `ProductImage` na BD com:

- `url480w`, `url1024w`, `url2048w` — WebP CDN em 3 sizes
- `blurDataUrl` — LQIP base64 inline (~50-200 bytes)
- `width`, `height` — aspect ratio (zero CLS)
- `optimizedAt` — `NULL` para imagens legacy ainda não processadas

Falta evoluir o **payload dos webhooks `phones.adicionado` / `phones.atualizado`** para o shop conseguir consumir estes campos. Hoje o `shopImagesCurated[]` no payload é `string[]` simples.

## Shape proposta — `shopImagesCurated[]`

### Tolerância back-compat

O shop **DEVE** continuar a aceitar a shape antiga (`string[]`) durante a transição porque há produtos com `optimizedAt = NULL`. Quando uma imagem ainda não foi processada, manda só `url`.

### Novo formato

```json
{
  "shopImagesCurated": [
    {
      "url": "https://cdn.app.lopestech.pt/products/<uuid>/main.webp",
      "url480w": "https://cdn.app.lopestech.pt/products/<uuid>/main-480w.webp",
      "url1024w": "https://cdn.app.lopestech.pt/products/<uuid>/main-1024w.webp",
      "url2048w": "https://cdn.app.lopestech.pt/products/<uuid>/main-2048w.webp",
      "blurDataUrl": "data:image/webp;base64,UklGRkIAAABXRUJQVlA4IDYAAACwAQCdASoEAAQAAcAhJZQAA3AA/v3AgAA=",
      "width": 2048,
      "height": 2048,
      "alt": "iPhone 14 Pro 128GB Roxo — frente",
      "order": 0
    },
    {
      "url": "https://cdn.app.lopestech.pt/products/<uuid>/back.webp",
      "url480w": null,
      "url1024w": null,
      "url2048w": null,
      "blurDataUrl": null,
      "width": null,
      "height": null,
      "alt": "iPhone 14 Pro 128GB Roxo — traseira",
      "order": 1
    }
  ]
}
```

### Field reference

| Campo | Tipo | Obrigatório | Notas |
|---|---|---|---|
| `url` | string | ✅ | URL canónico da imagem original |
| `url480w` | string \| null | – | WebP 480px largura · null se não processada |
| `url1024w` | string \| null | – | WebP 1024px largura · null se não processada |
| `url2048w` | string \| null | – | WebP 2048px largura · null se não processada (retina) |
| `blurDataUrl` | string \| null | – | LQIP base64 (`data:image/webp;base64,…`) inline, max ~500 chars |
| `width` | number \| null | – | Largura original em px |
| `height` | number \| null | – | Altura original em px |
| `alt` | string | recomendado | Alt text PT-PT. Falta = shop gera de `productTitle` |
| `order` | number | – | Ordem de display, 0 = primeira |

### Back-compat string array

Continua a ser aceite enquanto há legacy `optimizedAt = NULL`:

```json
{
  "shopImagesCurated": [
    "https://cdn.app.lopestech.pt/products/<uuid>/main.webp",
    "https://cdn.app.lopestech.pt/products/<uuid>/back.webp"
  ]
}
```

Quando isto chega, o shop trata como `[{ url: "..." }]` com todos os campos opcionais a `null`.

## Tipo Zod no shop (referência)

Vou expandir `src/server/products/repairdesk-payload-schema.ts`:

```ts
const imageObjectSchema = z.object({
  url: z.string().url(),
  url480w: z.string().url().nullable().optional(),
  url1024w: z.string().url().nullable().optional(),
  url2048w: z.string().url().nullable().optional(),
  blurDataUrl: z.string().startsWith("data:image/").nullable().optional(),
  width: z.number().int().positive().nullable().optional(),
  height: z.number().int().positive().nullable().optional(),
  alt: z.string().max(200).optional(),
  order: z.number().int().nonnegative().optional(),
});

// Accept legacy string[] OR new object[]
const imageItem = z.union([z.string().url(), imageObjectSchema]);
const shopImagesCurated = z.array(imageItem).max(20);
```

## DB schema na shop

A shop hoje guarda `products.images` como `text[]` (URLs). Para
suportar sizes + LQIP preciso de uma nova table `product_images`:

```sql
CREATE TABLE product_images (
  id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  product_id uuid NOT NULL REFERENCES products(id) ON DELETE CASCADE,
  display_order integer NOT NULL DEFAULT 0,
  url text NOT NULL,
  url_480w text,
  url_1024w text,
  url_2048w text,
  blur_data_url text,
  width integer,
  height integer,
  alt text,
  optimized_at timestamptz,
  created_at timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX product_images_product_order_idx
  ON product_images(product_id, display_order);
```

`products.images` (text[]) mantém-se como cache de URLs primários para
queries simples (search, sitemap, og:image fallback). `product_images`
é a tabela canónica com a metadata rica.

## Sync logic (shop-side)

Quando o webhook chega:

```ts
// Pseudo-code
async function syncImages(productId: string, items: ImageItem[]) {
  // Strip everything for this product
  await db.delete(productImages).where(eq(productImages.productId, productId));

  // Insert new
  for (const [order, item] of items.entries()) {
    const obj = typeof item === "string" ? { url: item } : item;
    await db.insert(productImages).values({
      productId,
      displayOrder: order,
      url: obj.url,
      url480w: obj.url480w ?? null,
      url1024w: obj.url1024w ?? null,
      url2048w: obj.url2048w ?? null,
      blurDataUrl: obj.blurDataUrl ?? null,
      width: obj.width ?? null,
      height: obj.height ?? null,
      alt: obj.alt ?? null,
      optimizedAt: obj.url480w ? new Date() : null,
    });
  }

  // Also update the legacy products.images cache
  await db
    .update(products)
    .set({ images: items.map((it) => (typeof it === "string" ? it : it.url)) })
    .where(eq(products.id, productId));
}
```

## UI consumption

`ProductCard` + product-detail page passam de:

```tsx
<Image src={product.images[0]} alt={product.title} fill />
```

Para (quando temos sizes):

```tsx
<Image
  src={img.url}
  alt={img.alt ?? product.title}
  width={img.width ?? 800}
  height={img.height ?? 800}
  placeholder={img.blurDataUrl ? "blur" : "empty"}
  blurDataURL={img.blurDataUrl}
  sizes="(max-width: 640px) 480px, (max-width: 1024px) 1024px, 2048px"
/>
```

Next.js já gera `srcset` automaticamente quando recebe `sizes` — só
precisamos do `width`/`height`/`blurDataUrl` para ele activar
optimizações.

## Performance esperada

| Métrica | Antes | Depois |
|---|---|---|
| LCP (mobile) | ~2.5s | **~1.2s** |
| CLS | 0.05 | **0** |
| Mobile data per page | ~800 KB | **~250 KB** |
| Google Shopping rank | depende | **+ favorecido** (Google penaliza CLS + LCP > 2.5s) |

## Open question — alt text source

Hoje o webhook payload não tem `alt` no `shopImagesCurated`. Bruno
provavelmente quer escrever uma vez no RD (parte do produto) e propagar.
Posso assumir:

1. **Por enquanto**: shop gera alt do `productTitle` quando ausente
2. **Futuro**: novo campo `productImage.alt` na BD do RD, propagado no webhook

A `AnthropicAltTextService` já existente no RD pode auto-gerar quando
não há alt manual.

## Implementation order (suggestion)

1. **RD-side**: emit novo formato no webhook (Sprint 190?). Manda
   webhook a 1 produto de teste para o shop poder validar.
2. **Shop-side** (eu faço):
   - Zod schema update + tests
   - DB migration (add `product_images` table)
   - Sync logic
   - UI components
3. **Backfill**: re-emitir webhooks para todos os produtos optimizados
   (botão no admin RD?)

## Refs

- Sprint 189: ProductImage entity + endpoint `/api/products/{id}/images/upload`
- Shop AI Bridge (já em prod): `Contexto/61-Shop-AI-Bridge-Spec.md`
- Shop webhook handler hoje: `lopestech-shop/src/app/api/webhooks/repairdesk/route.ts`
- Shop Zod schema: `lopestech-shop/src/server/products/repairdesk-payload-schema.ts`
