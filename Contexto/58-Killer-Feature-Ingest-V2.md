# 58 — Killer Feature: Ingest universal de faturas de fornecedor (V2)

**Decisão Bruno (2026-05-21):** evoluir Sprint 147-150 (IMAP simples) para sistema completo.
Aprovadas decisões arquitecturais A/B/C/D.

## Visão final

Bruno tem 3 emails (`contacto@lopestech.pt`, `bruno.miguel.martins.lopes@gmail.com`,
`bruno-lopes9@hotmail.com`). Fornecedores enviam faturas:
1. **PDF anexo** (Utopya, Tudo4Mobile às vezes)
2. **CSV anexo** (Molano, Utopya 2.º email com 1.csv)
3. **Corpo HTML do email** (Tudo4Mobile típico — sem anexo, fatura inline)
4. **Foto/scan papel** (alguns fornecedores)

Sistema deve:
- Detectar fornecedor (FROM email + regex)
- Extrair dados (parser específico → genérico → OCR → LLM fallback)
- Matching SKU (tabela mapeamento + fuzzy)
- **NÃO actualizar automaticamente** — cria rascunho, Bruno aprova, escolhe destino
- Mostrar SEMPRE erros para Bruno corrigir manualmente

## Arquitectura final

```
┌────────────────────────────────────────────────────────────────┐
│  n8n (1 workflow por caixa email do tenant)                    │
│                                                                │
│  IMAP Trigger → Detect fornecedor (FROM regex map) →           │
│  Extract attachments + bodyHtml → HTTP POST RepairDesk         │
└────────────────────────┬───────────────────────────────────────┘
                         │
                         ▼
┌────────────────────────────────────────────────────────────────┐
│  POST /api/external/supplier-invoices/ingest                   │
│  Body: { supplierCode?, attachments[], emailMeta, bodyHtml? }  │
│                                                                │
│  Pipeline em cascata (1.ª que retorna confidence>=High vence): │
│   a) Parser PDF específico (Tudo4Mobile, Utopya, Molano)       │
│   b) Parser CSV genérico (header detection)                    │
│   c) Parser body HTML (HtmlAgilityPack tables)                 │
│   d) OCR Claude Vision (PDF imagem ou foto)                    │
│   e) LLM fallback (Claude Sonnet com prompt estruturado)       │
└────────────────────────┬───────────────────────────────────────┘
                         │
                         ▼
            SKU Matching (Sprint 157)
            ┌──────────────────────────────┐
            │ 1. Tabela mapping explícita  │
            │    (supplier_sku → part_id)  │
            │ 2. Fuzzy match nome (Lev≤3)  │
            │ 3. Sugere top 3 + "Criar novo"│
            └──────────────┬───────────────┘
                           │
                           ▼
            SupplierInvoiceImport criado em PENDING
                           │
                Bruno revê em /importacoes
                           │
                           ▼
            ┌──────────────────────────────────────┐
            │  Modal de aprovação (Sprint 158):    │
            │   Para CADA linha de item:           │
            │    [✓] Match: HUA-P20L-LCD           │
            │     OU                                │
            │    [...] Criar novo Part: ___         │
            │                                       │
            │   Para o documento todo:              │
            │    Destino:                           │
            │     ○ Stock (Part) — aumenta inventário│
            │     ○ Produto loja (capas, películas) │
            │     ○ Imputar a Reparação #N (peça   │
            │       encomendada para job específico)│
            │     ○ Despesa overhead (transporte,  │
            │       material consumível)            │
            │                                       │
            │    [Aprovar + executar]              │
            └──────────────────────────────────────┘
```

## Decisões aprovadas Bruno

### A — Parsers híbridos (regex + LLM fallback)
- **Regex/heurística** para fornecedores conhecidos (Tudo4Mobile Sprint 124+134, Utopya, Molano)
- **LLM Claude Sonnet** com prompt estruturado quando regex falha (~0.5¢/fatura)
- **Custo estimado:** 100 faturas/mês = ~50 cêntimos LLM

### B — SKU matching híbrido
- **Tabela `SkuMapping(tenantId, supplierCode, supplierSku) → partId`** populada por aprovações Bruno
- **Fuzzy match Levenshtein** (token-based) com top 3 sugestões quando sem match exacto
- **"Criar novo Part"** sempre disponível como fallback
- Bruno aprova → SkuMapping aprende automaticamente para próxima fatura

### C — OCR via Claude Vision
- Para foto fatura papel: Bruno faz upload → Claude Vision extrai estruturado
- Já temos infra Anthropic (Sprint 144 retry config)
- Custo: ~0.5¢/imagem
- Fallback: Tesseract self-hosted (futuro)

### D — n8n + UI espelho
- **Credenciais IMAP ficam em n8n** (não na BD RepairDesk — security)
- **RepairDesk tem UI espelho** em `/definicoes/automacoes`:
  - Lista workflows n8n activos por tenant
  - Status (último email processado, sucesso/erro)
  - Link "Abrir no n8n" para edição
- Multi-tenant: cada tenant tem o seu próprio n8n self-hosted OU central com workflow per tenant

## Multi-tenant + multi-email

Cada tenant pode ter N caixas de email. Recomendação:
- **1 workflow n8n por caixa** (simplifica isolamento)
- Tenant configura no n8n: credenciais IMAP + fornecedores conhecidos
- RepairDesk só recebe webhook ingest (não precisa de credenciais)

## SKUs internos canónicos

Bruno deve definir um padrão de SKU interno para a sua loja. Convenção sugerida:
- **Telemóveis:** `{BRAND}-{MODEL}-{STORAGE}-{COLOR}-{GRADE}` ex: `APL-IP15-256-BLK-GA`
- **Peças:** `{TYPE}-{BRAND}-{MODEL}` ex: `LCD-HUA-P20L`, `BAT-IP11`
- **Acessórios:** `ACC-{TYPE}-{BRAND}-{MODEL}` ex: `ACC-CASE-IP15-BLK`

Tabela `SkuMapping` faz a tradução `MOL-2021408-LCD` ↔ `LCD-HUA-P20L`.

## Roadmap implementação

| Sprint | Entrega | Esforço |
|---|---|---|
| **157** | Entidade `SkuMapping` + fuzzy matcher (Levenshtein/Jaro-Winkler) | 1.5 dias |
| **158** | UI revisão estruturada com escolha destino (stock/produto/reparação/despesa) | 2 dias |
| **159** | Parser body HTML (HtmlAgilityPack tables) + parser CSV genérico | 1.5 dias |
| **160** | Parser Utopya PDF + multi-anexo handling | 2 dias |
| **161** | Claude Vision OCR para foto papel | 1.5 dias |
| **162** | LLM fallback (Claude Sonnet) para PDFs mal formatados | 2 dias |
| **163** | UI `/definicoes/automacoes` com lista workflows n8n | 1.5 dias |
| **164** | Webhook events `supplier-invoice.pending` para n8n notificar | 1 dia |

**Total: ~13 dias úteis** (3 semanas).

## Casos de não-cobertura

- **Fornecedores sem fatura** (compras "por fora"): Bruno faz inserção manual em `/importacoes`
  → "Criar manualmente" com items, fornecedor "Anónimo"
- **Faturas em idiomas exóticos:** LLM fallback cobre os principais (PT, EN, FR, ES)
- **Faturas com IVA estrangeiro** (Utopya FR): backend tem que tratar IVA intracomunitário —
  Sprint futuro, scope fiscal complexo

## Próximas decisões Bruno

Antes de arrancar Sprint 157 preciso:
1. **API key Anthropic** — Bruno cria conta em [console.anthropic.com](https://console.anthropic.com), gera key, mete em `.env` como `ANTHROPIC_API_KEY=sk-ant-...`
2. **Convenção SKU própria** — Bruno aprova convenção acima ou propõe outra
3. **Primeiro fornecedor a estender** (Utopya body HTML? Molano CSV automation?)
