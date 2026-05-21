# 55 — Importação automática de facturas de fornecedor via n8n IMAP

**Sprint 147+148+149.** Bruno tinha workflow manual: abrir email da Tudo4Mobile,
descarregar PDF, escrever Despesa à mão. Esta integração automatiza tudo —
n8n trata transporte (IMAP/email), RepairDesk trata negócio (parsing + Despesa).

## Visão geral

```
┌─────────────┐         ┌──────────────┐         ┌─────────────────┐
│ Gmail caixa │ IMAP    │   n8n        │ HTTP    │ RepairDesk API  │
│faturas@.pt  │ ──────► │ workflow     │ ──────► │ /api/external/  │
└─────────────┘ poll    │ (1 por tenant│ POST    │ supplier-       │
                        └──────────────┘ HMAC    │ invoices/ingest │
                                                 └────────┬────────┘
                                                          │ parser
                                                          ▼
                                                ┌─────────────────────┐
                                                │SupplierInvoiceImport│  Status=Pending
                                                │ (rascunho)          │  ou =Failed
                                                └────────┬────────────┘
                                                         │
                                            Bruno revê + Aprova em /importacoes
                                                         │
                                                         ▼
                                                ┌─────────────────────┐
                                                │  Despesa real       │
                                                └─────────────────────┘
```

## Setup do tenant

### 1. Criar API key com scope `ingest`

Em **Definições → API Keys**:
- Nome: `n8n IMAP automation`
- Scopes: ✓ `ingest` (não dá leituras de catálogo/orders — princípio least-privilege)
- Copiar a key **imediatamente** (plain só mostrada na criação)

### 2. Criar caixa de email dedicada

Recomendado um inbox separado para evitar mexer no email pessoal:
- Gmail: `facturas@dominio-tenant.com` ou pasta `Faturas` com filtro automático
- Outlook 365: equivalente com regras

### 3. Configurar n8n

Self-hosted no mesmo Docker Compose (gratuito) ou n8n Cloud.

## Workflow n8n (JSON importável)

Cola o JSON abaixo em n8n → "Import from File".

```json
{
  "name": "RepairDesk — IMAP Supplier Invoices",
  "nodes": [
    {
      "parameters": {
        "mailbox": "INBOX/Fornecedores",
        "options": {
          "customEmailConfig": "[\"UNSEEN\"]"
        },
        "downloadAttachments": true
      },
      "name": "IMAP Trigger",
      "type": "n8n-nodes-base.emailReadImap",
      "position": [240, 300],
      "credentials": { "imap": "Gmail Faturas" }
    },
    {
      "parameters": {
        "conditions": {
          "string": [
            { "value1": "={{$binary.attachment_0.fileName}}", "operation": "endsWith", "value2": ".pdf" }
          ]
        }
      },
      "name": "Se tem PDF anexo",
      "type": "n8n-nodes-base.if",
      "position": [460, 300]
    },
    {
      "parameters": {
        "method": "POST",
        "url": "https://repairdesk.lopestech.pt/api/external/supplier-invoices/ingest",
        "authentication": "headerAuth",
        "sendBody": true,
        "specifyBody": "json",
        "jsonBody": "={\n  \"pdfBase64\": \"{{$binary.attachment_0.data}}\",\n  \"emailMeta\": {\n    \"messageId\": \"{{$json[\"messageId\"]}}\",\n    \"subject\": \"{{$json[\"subject\"]}}\",\n    \"from\": \"{{$json[\"from\"]}}\",\n    \"receivedAt\": \"{{$json[\"date\"]}}\"\n  }\n}",
        "options": { "retry": { "enabled": true, "maxRetries": 3, "waitBetweenRetries": 5000 } }
      },
      "name": "POST RepairDesk ingest",
      "type": "n8n-nodes-base.httpRequest",
      "position": [680, 220],
      "credentials": { "httpHeaderAuth": "RepairDesk API key (ingest scope)" }
    },
    {
      "parameters": {
        "operation": "move",
        "mailbox": "INBOX/Fornecedores",
        "destinationMailbox": "=Fornecedores/{{$json[\"fornecedorNameRaw\"] || 'Desconhecido'}}/{{$now.format('yyyy-MM')}}"
      },
      "name": "Move to label processado",
      "type": "n8n-nodes-base.emailReadImap",
      "position": [900, 220]
    },
    {
      "parameters": {
        "operation": "move",
        "mailbox": "INBOX/Fornecedores",
        "destinationMailbox": "INBOX/Fornecedores/_FALHA"
      },
      "name": "Move to _FALHA",
      "type": "n8n-nodes-base.emailReadImap",
      "position": [680, 420]
    }
  ],
  "connections": {
    "IMAP Trigger": { "main": [[{ "node": "Se tem PDF anexo", "type": "main", "index": 0 }]] },
    "Se tem PDF anexo": {
      "main": [
        [{ "node": "POST RepairDesk ingest", "type": "main", "index": 0 }],
        [{ "node": "Move to _FALHA", "type": "main", "index": 0 }]
      ]
    },
    "POST RepairDesk ingest": { "main": [[{ "node": "Move to label processado", "type": "main", "index": 0 }]] }
  }
}
```

### Credenciais a configurar manualmente em n8n

**"Gmail Faturas"** (IMAP):
- Host: `imap.gmail.com`
- Port: `993`
- User: `facturas@dominio.com`
- Password: **App Password** (Gmail só aceita app passwords, não a password normal — gera em https://myaccount.google.com/apppasswords)
- TLS: ✓

**"RepairDesk API key (ingest scope)"** (HTTP Header Auth):
- Name: `X-Api-Key`
- Value: a key copiada no passo 1

## Comportamento esperado

| Cenário | Status retornado | Acção n8n |
|---|---|---|
| PDF novo, parser OK | `Pending` | Move email para label `Fornecedores/Tudo4Mobile/2026-05` |
| Mesmo PDF reenviado | `Pending` + `wasDuplicate=true` | Move para label, NÃO duplica registo |
| Parser falhou (None) | `Failed` | Bruno revê manualmente; ainda assim move para label |
| API key expirou / sem scope | HTTP 403 | n8n retry 3× depois falha → email fica em INBOX |
| Moloni/AT/etc indisponível | HTTP 503 | n8n retry com exponential backoff |

## Storage filesystem

PDFs ficam em (host machine):
```
{SUPPLIER_INVOICES_HOST_PATH ?: ./data/supplier-invoices}/
└── {tenantId}/
    └── 2026/
        └── 05/
            └── tudo4mobile/
                ├── 2026-05-20_FT-2026-2841.pdf
                └── 2026-05-22_FT-2026-2843.pdf
```

Para sync automático com Dropbox/Drive (contabilista):
```bash
# docker-compose.override.yml
services:
  api:
    volumes:
      - ~/Dropbox/RepairDesk/faturas-fornecedor:/data/supplier-invoices
```

## Export ZIP trimestral

Em **/importacoes** → "Export trimestral", escolhe datas e descarrega ZIP com
estrutura `ano/mês/fornecedor/fatura.pdf` (sem o tenant prefix). Pronto para
entregar ao contabilista.

## Limites e edge cases

- **PDF > 25MB**: rejeitado preventivamente (PDFs reais de fornecedor são < 1MB)
- **Mais que 1 PDF anexo no email**: o workflow só processa o primeiro. Ajustar
  workflow se Bruno tiver fornecedores que enviem múltiplos PDFs por email.
- **Email sem PDF**: o IF node desvia para `_FALHA`, Bruno vê na inbox
- **Imagem JPG/PNG**: não suportado — workflow filtra `.pdf`. Se Bruno quiser
  suporte OCR de fotos de facturas em papel, é sprint adicional (Tesseract + parser)
- **Parser unknown supplier**: status fica `Failed`, mas o PDF é guardado em
  `desconhecido/`. Bruno aprova manualmente e o sistema "aprende" o fornecedor
  quando criar a `Despesa.Fornecedor`

## Próximos sprints possíveis

- **OCR fallback** para imagens (Tesseract.NET em worker .NET)
- **Webhook outbound** `supplier-invoice.pending` para Bruno receber push notification
- **Parser plugins** — sistema declarativo para Bruno adicionar regex/extractors
  de novos fornecedores sem código C# (Sprint 124+134 são hardcoded Tudo4Mobile)
- **Auto-aprovar** quando confidence=High E total bate com encomenda existente
