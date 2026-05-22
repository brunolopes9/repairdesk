# Spec: Shop ↔ RD AI Bridge

**Versão:** 1.0 · 2026-05-22
**Autor:** Claude (shop) a pedido do Bruno
**Status:** A implementar (RD-side)

## Contexto

A loja online (`lopestech-shop`) já tem 2 features AI prontas client-
side:

1. **LopesTech Assistant** — cliente escreve em linguagem natural
   ("iPhone 14 128GB roxo até 500€") e a UI converte em filtros +
   navega ao /pesquisa ou /loja com filtros aplicados.
2. **Lens (image search)** — cliente tira foto / faz upload, vision
   model identifica produto, navega à página filtrada.

Hoje (commit `28aed1f` no shop) ambos chamam Anthropic API
**directamente** com a key na env var `ANTHROPIC_API_KEY` do Vercel.

**Bruno prefere** que estas chamadas passem pelo RepairDesk para:

- Single source of truth para Anthropic credentials (já têm `LlmUsage`
  entity + `LlmUsageTracker`)
- Contabilizar tokens por tenant — o RD já o faz para casos internos
  (parser de faturas, alt-text generation)
- Rate-limiting central por tenant
- Mais fácil rotar key depois (1 sítio, não 2)

## Endpoints a implementar (RD-side)

Ambos devem viver em `RepairDesk.API/Controllers/ExternalController.cs`
(mesmo ficheiro que já tem `/api/external/products`, `/garantias`, etc).
**Mesmo auth** que os outros endpoints external — Bearer token contra
o `REPAIRDESK_API_KEY` do tenant.

### 1. `POST /api/external/ai-assistant`

Conversão linguagem natural → filtros estruturados.

**Request body** (JSON):

```json
{
  "query": "iPhone 14 128GB roxo abaixo de 500€"
}
```

**Validação esperada:**

- `query`: string, trim, 2-500 chars.

**Headers:**

```
Authorization: Bearer <REPAIRDESK_API_KEY>
Content-Type: application/json
```

**Response 200 OK** (JSON):

```json
{
  "ok": true,
  "filters": {
    "searchQuery": "iPhone 14",
    "category": "phone",
    "brand": ["apple"],
    "storage": ["128GB"],
    "color": ["roxo"],
    "priceMin": null,
    "priceMax": 500
  },
  "explanation": "iPhones 14 128GB cor roxo até 500 €.",
  "url": "/loja/apple?attr.storage=128GB&attr.color=roxo&attr.priceMax=50000"
}
```

**Notes:**

- `url` é o caminho **relativo** da loja (sem domain). O shop monta o
  base URL.
- `priceMin`/`priceMax` em **euros inteiros** no JSON (o shop multiplica
  por 100 para cêntimos antes de aplicar).
- `brand` é array (pode ser vazio) com slugs lowercase.
- `category` ∈ `{phone, accessory, audio, tablet, laptop, other}` ou
  ausente.
- `explanation` é PT-PT, 1 frase curta para mostrar ao cliente.

**Response 4xx/5xx:**

```json
{
  "ok": false,
  "error": "Query muito curta"
}
```

**HTTP codes:**

- 400 — validação Zod failure
- 401 — bearer inválido
- 429 — rate-limited (sugerimos 20/min por tenant)
- 503 — `ANTHROPIC_API_KEY` não configurada
- 500 — erro interno

### 2. `POST /api/external/ai-image-search`

Identificação visual de produto a partir de imagem.

**Request:** `multipart/form-data`

- Campo `image`: ficheiro (jpeg/png/webp/gif, max 5 MB)

**Headers:**

```
Authorization: Bearer <REPAIRDESK_API_KEY>
```

**Response 200 OK:**

```json
{
  "ok": true,
  "searchQuery": "iPhone 14 Pro Roxo",
  "brand": "apple",
  "model": "iPhone 14 Pro",
  "category": "phone",
  "explanation": "Identifiquei um iPhone 14 Pro roxo.",
  "url": "/loja/apple?q=iPhone+14+Pro+Roxo"
}
```

**Vision-specific notes:**

- Vision tokens ~30× custo de texto. Sugerimos **rate-limit 10/min** por
  tenant.
- Imagem deve ser **base64-encoded para Anthropic** mas o RD recebe
  multipart e converte internamente — o shop manda binário, não base64.

## System prompts a usar

### Assistant

```
És o assistant de compras da LopesTech, uma loja portuguesa de telemóveis recondicionados em Viseu.

Cliente escreve em linguagem natural o que procura. A tua tarefa: extrair filtros estruturados.

Categorias disponíveis (slug exacto): "phone", "accessory", "audio", "tablet", "laptop", "other"
Marcas comuns (lowercase): apple, samsung, xiaomi, oppo, huawei, realme, motorola, lopestech, morelio
Storage típico: "64GB", "128GB", "256GB", "512GB", "1TB"
Cores típicas: preto, branco, azul, roxo, dourado, rosa, vermelho, verde

Responde APENAS com JSON no formato { filters: { searchQuery, category, brand[], storage[], color[], priceMin, priceMax }, explanation }.

Se a pergunta não é sobre produtos da loja (suporte, reparações, garantias, etc), responde com filters: {} e explanation: "Para esse assunto fala connosco no WhatsApp ou contacto@lopestech.pt."

NUNCA inventes marcas/modelos. Se o cliente menciona algo que não tens a certeza, usa apenas searchQuery.
```

### Image search

```
És o assistant visual da LopesTech, uma loja portuguesa de telemóveis recondicionados.

Vês uma imagem que o cliente carregou. Identifica:
1. Categoria do produto (phone, accessory, audio, tablet, laptop, other)
2. Marca (apple, samsung, xiaomi, oppo, huawei, etc — lowercase)
3. Modelo específico se conseguires identificar
4. Cor visível se for relevante

Responde APENAS com JSON: { searchQuery, category, brand, model, explanation }.

Se a imagem não for de um produto reconhecível ou for inadequada:
{ searchQuery: "", explanation: "Não consegui identificar um produto na imagem. Tenta uma foto mais nítida do dispositivo." }

NUNCA inventes marcas.
```

## Model recomendado

`claude-haiku-4-5-20251001` para ambos.

- Mais barato (~5× cheaper que Sonnet)
- Latência ~500-800ms text, ~1.5-2.5s vision
- Quality suficiente para extracção estruturada

`max_tokens`: 512 chega para ambos.

## LlmUsage tracking

Aproveitar o `LlmUsageTracker` já existente (`Documents/`). Adicionar
2 novos `LlmUsageKind`:

- `ShopAssistant` — text-only
- `ShopImageSearch` — vision

Para cada call gravar:

- `tenantId` (do bearer token)
- `kind` (qual dos 2)
- `inputTokens`, `outputTokens` (vêm na response Anthropic)
- `costEur` (calcular do pricing Haiku 4.5)
- `createdAt`

Permite ao Bruno ver `/admin/llm-usage` quanto está a gastar nestas
features.

## Shop-side: o que muda quando isto estiver pronto

O shop tem 2 ficheiros que precisam de refactor:

- `src/server/ai/assistant.ts` — função `askAssistant(query)`
- `src/server/ai/image-search.ts` — função `searchByImage(base64, mediaType)`

Mudança: o `fetch` para `https://api.anthropic.com/v1/messages` passa a
ser `fetch` para `${process.env.REPAIRDESK_API_URL}/api/external/ai-...`
com `Authorization: Bearer ${process.env.REPAIRDESK_API_KEY}`.

A response shape do RD **deve coincidir 1:1** com a actual `askAssistant`
return type para minimizar o refactor:

- `{ ok: true, filters, explanation, url }`
- `{ ok: false, error }`

Se mudares a shape, avisa-me e eu adapto o caller.

## Quando avisar

Quando os 2 endpoints estiverem em produção (responder com a key real
do tenant default), avisa que faço o refactor de ~10 min.

## Fallback

Se por qualquer motivo o endpoint RD estiver offline, o shop **NÃO**
volta a fazer fallback directo a Anthropic (segurança — não queremos
deixar a key tornar a deploy se um dia rotares). O AssistantPanel
mostra apenas "Assistant temporariamente indisponível".

## Refs

- Shop commit que criou Anthropic-direct: `7214e7c` (assistant) +
  `28aed1f` (image)
- Shop code paths: `src/server/ai/`, `src/components/layout/search-overlay.tsx`
- RD internal Anthropic usage (referência): `RepairDesk.Services/Documents/AnthropicSupplierParser.cs`, `RepairDesk.Services/Products/AnthropicAltTextService.cs`
- RD LlmUsage entity já existe: `RepairDesk.Core/Entities/LlmUsage.cs`
