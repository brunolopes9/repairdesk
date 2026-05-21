# 60 — Pipeline automático de imagens SEO

**Pedido Bruno (2026-05-21):** quer fazer upload de fotos de produtos (mesmo mal formatadas,
PNG grandes, tamanhos errados) e o sistema **automaticamente optimiza** para SEO + performance
+ marketing + motores de busca.

## O que isto resolve

Bruno faz fotos no telemóvel ou recebe imagens raw do Molano. Hoje:
- Imagens são URLs externas (R2/CDN)
- Sem optimização → site lento, fica mal no Google Lighthouse
- Sem alt text → mau SEO, mau acessibilidade
- 1 só resolução → mobile carrega 4K desnecessariamente

Depois:
- Upload arbitrary image → pipeline automático produz 3 resoluções WebP + AVIF
- Alt text gerado automaticamente via Claude Vision
- Loja recebe URLs optimizadas + structured data JSON-LD
- Google Lighthouse 95+ Performance, SEO

## Pipeline

```
Bruno upload bigfoto.png (3MB, 4032×3024)
                │
                ▼
┌──────────────────────────────────────┐
│  RepairDesk: ProductImageService     │
│                                      │
│  1. ImageSharp resize × 3:           │
│     - 480w mobile (24KB WebP)        │
│     - 1024w tablet (110KB WebP)      │
│     - 2048w desktop (380KB WebP)     │
│  2. AVIF version cada resolução (-30%)│
│  3. Upload R2/Cloudflare CDN         │
│  4. Claude Vision alt text:          │
│     "iPhone 14 Pro Max preto vista   │
│      frontal ecrã ligado mostrando   │
│      apps Apple"                     │
│  5. ProductImage entity actualizada: │
│     - originalUrl (raw, kept)        │
│     - urls480w/1024w/2048w (webp+avif│
│     - altText                         │
│     - blurDataUrl (LQIP base64)      │
└──────────────┬───────────────────────┘
               │
               ▼
┌──────────────────────────────────────┐
│  Webhook payload (Sprint 154 evolution)│
│                                      │
│  shopImagesCurated: [{               │
│    sizes: {                          │
│      "480w": { webp, avif },         │
│      "1024w": { webp, avif },        │
│      "2048w": { webp, avif }         │
│    },                                │
│    alt: "iPhone 14 Pro Max preto...",│
│    blur: "data:image/jpeg;base64..." │
│  }, ...]                             │
└──────────────┬───────────────────────┘
               │
               ▼
┌──────────────────────────────────────┐
│  Loja online (outro Claude)          │
│   - <picture> com srcset 3 sizes    │
│   - <img loading="lazy">             │
│   - Schema.org structured data JSON-LD│
│   - LQIP blur placeholder            │
└──────────────────────────────────────┘
```

## SEO structured data (responsabilidade da loja)

```json
{
  "@context": "https://schema.org",
  "@type": "Product",
  "name": "iPhone 14 Pro Max 256GB Preto Recondicionado",
  "image": [
    "https://cdn.lopestech.pt/p/abc/480w.webp",
    "https://cdn.lopestech.pt/p/abc/1024w.webp",
    "https://cdn.lopestech.pt/p/abc/2048w.webp"
  ],
  "description": "...",
  "sku": "APL-IP14PM-256-BLK-GA",
  "brand": { "@type": "Brand", "name": "Apple" },
  "offers": {
    "@type": "Offer",
    "price": "789.00",
    "priceCurrency": "EUR",
    "availability": "https://schema.org/InStock",
    "itemCondition": "https://schema.org/RefurbishedCondition"
  },
  "aggregateRating": { "@type": "AggregateRating", "ratingValue": "4.8", "reviewCount": "127" }
}
```

## Custos

| Item | Custo |
|---|---|
| Claude Vision alt text (~0.5¢/imagem) | 100 imagens = 50 cêntimos |
| Cloudflare R2 storage (10€/TB/mês) | 10000 imagens = ~5 GB = 5 cêntimos/mês |
| Cloudflare R2 egress | **0€** (CDN gratuito) |
| ImageSharp (NuGet) | Grátis |
| **Total** | **<1€/mês para 100 produtos** |

## Stack

- **Backend RepairDesk:**
  - `SixLabors.ImageSharp` (NuGet, MIT-licenced)
  - Upload endpoint actual já existe — só ganha pipeline
  - Cloudflare R2 já configurado (Sprint 14)
- **Frontend admin:** drag-drop existente; mostra progress bar de optimização
- **Loja:** consome URLs + alt + blur via webhook

## Roadmap implementação

| Sprint | Entrega | Esforço |
|---|---|---|
| **180** | ProductImage ganha campos urls480w/1024w/2048w/blurDataUrl/altText + migration | 0.5 dia |
| **181** | ImageOptimizationService (ImageSharp resize multi-format) | 1.5 dias |
| **182** | Claude Vision alt text generator | 1 dia |
| **183** | UI drag-drop em /produtos ficha + progress bar | 1.5 dias |
| **184** | Webhook payload evolution com sizes object | 0.5 dia |
| **185** | Cron re-optimize batch (1× para produtos antigos) | 1 dia |

**Total: ~6 dias.**

## Não-objectivos (por agora)

- Editor de imagem inline (crop, rotate) — Bruno usa Photoshop/CapCut antes do upload se precisar
- Multi-tenant CDN custom — todos tenants partilham bucket Lopes Cloudflare R2
- Watermark automático — futuro

## Próximo

Implementar depois da killer feature ingest V2. Pipeline imagens é independente mas
beneficia de já ter ProductService maduro.
