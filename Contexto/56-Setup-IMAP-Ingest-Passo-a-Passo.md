# 56 — Setup IMAP Ingest passo-a-passo

**Para Bruno.** Tempo total: ~25 minutos. Não precisas de escrever código.

## O que vai acontecer no fim

Tu envias um email com PDF de fatura para `facturas@lopestech.pt` (ou qualquer
caixa que escolhas), e em ≤2 minutos aparece automaticamente em `/importacoes`
no RepairDesk pronto para aprovar.

---

## PASSO 1 — Arrancar o n8n (1 min)

Já está no `docker-compose.yml` mas com profile "automation" para não arrancar
por defeito. Para o ligar:

```bash
# 1. Copia variáveis novas do .env.example para o teu .env
#    (N8N_BASIC_AUTH_USER, N8N_BASIC_AUTH_PASSWORD, N8N_ENCRYPTION_KEY)
#    Gera uma encryption key forte:
openssl rand -base64 32
#    e cola em N8N_ENCRYPTION_KEY no .env

# 2. Arranca tudo (RepairDesk + n8n)
docker compose --profile automation up -d

# 3. Verifica
docker compose ps
# Deves ver: repairdesk-api, repairdesk-web, repairdesk-db, repairdesk-cache, repairdesk-n8n
```

Abre **http://localhost:5678** — n8n pede basic auth. Login com `N8N_BASIC_AUTH_USER` +
`N8N_BASIC_AUTH_PASSWORD` do `.env`. Primeira vez vai pedir-te para criar um
"owner account" — usa o teu email + password à escolha (diferente do basic auth).

---

## PASSO 2 — Criar API key no RepairDesk com scope `ingest` (2 min)

1. Abre **http://localhost** → login Bruno
2. Vai a **Definições → API Keys**
3. Clica **"Nova API Key"**
4. Preenche:
   - **Nome**: `n8n IMAP automation`
   - **Scopes**: marca só ✅ **`ingest`** (não marques read nem write — princípio least-privilege)
5. Clica **"Criar"**
6. ⚠️ **COPIA A KEY AGORA** — só é mostrada uma vez. Deve começar com `rd_live_...`
7. Guarda temporariamente num bloco de notas — vais precisar no passo 5

---

## PASSO 3 — Preparar caixa de email Gmail (5 min)

Vamos usar uma caixa Gmail dedicada para receber facturas. Podes usar a tua
caixa pessoal mas é mais limpo separar.

### 3.1. Activar 2FA na conta Google

Necessário para conseguir gerar App Password. Se já tens 2FA, salta este passo.

1. https://myaccount.google.com/security
2. Procura "Verificação em duas etapas" → ligar

### 3.2. Gerar App Password

Gmail não aceita a tua password normal em IMAP — precisas de **App Password**.

1. https://myaccount.google.com/apppasswords
2. App name: `RepairDesk n8n`
3. Clica **"Create"**
4. **Copia a password de 16 chars** (sem espaços) — só é mostrada uma vez

### 3.3. Criar labels no Gmail

Vai ao Gmail → barra lateral esquerda → **"Criar nova etiqueta"**:

- `Fornecedores` (etiqueta-pai)
- Dentro de `Fornecedores`, cria `Tudo4Mobile` (e outras conforme precises)
- Cria também `Fornecedores/_FALHA` para emails que falharem o parser

### 3.4. (Opcional) Criar regra para auto-etiquetar

Gmail → engrenagem → "Ver todas as definições" → **Filtros e endereços bloqueados** → **Criar novo filtro**:

- **De**: `*@tudo4mobile.com`
- Tem anexo: ✅
- → **Aplicar etiqueta**: `Fornecedores/Tudo4Mobile`
- → **Marcar como lida**: ✗ (deixa não-lida para n8n apanhar)

---

## PASSO 4 — Configurar credenciais no n8n (3 min)

Abre n8n em http://localhost:5678 → barra esquerda **"Credentials"** → **"Add credential"**.

### 4.1. Credencial Gmail IMAP

- Procura **"IMAP"** → seleciona
- **Name**: `Gmail Faturas`
- **User**: o teu email Gmail (ex: `bruno@lopestech.pt`)
- **Password**: a App Password de 16 chars do passo 3.2
- **Host**: `imap.gmail.com`
- **Port**: `993`
- **SSL/TLS**: ✅
- Clica **"Save"** → n8n vai testar a ligação

Se der erro: confirma que 2FA está activo + estás a usar App Password (não a password Gmail normal).

### 4.2. Credencial RepairDesk API key

- "Add credential" → procura **"Header Auth"**
- **Name**: `RepairDesk Ingest`
- **Header Name**: `X-Api-Key`
- **Header Value**: a key `rd_live_...` que copiaste no passo 2.6
- **Save**

---

## PASSO 5 — Importar o workflow (2 min)

1. n8n → barra esquerda **"Workflows"** → **"Add workflow"** → **"Import from File"**
2. Cola o JSON do workflow (está em `Contexto/55-Supplier-Invoice-Ingest-n8n.md`,
   secção "Workflow n8n (JSON importável)")
3. Após importar, o workflow tem nodes com credenciais por configurar:
   - Clica no node **"IMAP Trigger"** → no campo **"Credential"** seleciona `Gmail Faturas`
   - Clica no node **"POST RepairDesk ingest"** → no campo **"Credential"** seleciona `RepairDesk Ingest`
   - Confirma que **URL** do node POST aponta para `http://api:8080/api/external/supplier-invoices/ingest`
     (containers comunicam por nome interno na rede `repairdesk-net`)

4. **Save** o workflow no canto superior direito

---

## PASSO 6 — Activar + testar (2 min)

1. No topo do workflow, toggle **"Active"** = ON
2. Envia para `bruno@lopestech.pt` um email **com PDF anexo** (ex: a fatura
   Tudo4Mobile mais recente)
3. Espera ~30s (Gmail IMAP polling)
4. Vai a **http://localhost/importacoes** no RepairDesk
5. Deves ver a fatura na lista com:
   - **Fornecedor**: `Tudo4Mobile` (extraído pelo parser)
   - **Documento**: número FT
   - **Total**: valor extraído
   - **Confidence**: badge `Alta` / `Média` / `Falhou`
6. Clica **PDF** (📄) para confirmar que está bem guardado
7. Clica **Aprovar** (✓) → modal → confirma valores → **"Aprovar + criar Despesa"**
8. Vai a **/despesas** → confirma que a Despesa apareceu com o valor certo

---

## PASSO 7 — Verificar storage no host (30s)

Os PDFs ficam organizados em filesystem para o contabilista. Verifica:

```bash
ls -R ./data/supplier-invoices/
# Output esperado:
# ./data/supplier-invoices/{tenant-id}/2026/05/tudo4mobile/2026-05-20_FT-2026-2841.pdf
```

Para sincronizar com Dropbox/Drive (contabilista acede directamente):

```bash
# 1. Move conteúdo actual para Dropbox
mv ./data/supplier-invoices ~/Dropbox/RepairDesk/faturas-fornecedor

# 2. Edita .env
SUPPLIER_INVOICES_HOST_PATH=~/Dropbox/RepairDesk/faturas-fornecedor

# 3. Recria api
docker compose up -d api
```

---

## PASSO 8 — Export ZIP trimestral (quando precisares)

Em **/importacoes** → secção "Export trimestral":
- Escolhe datas (ex: 01/04/2026 a 30/06/2026)
- Clica **"Descarregar ZIP"**
- Envia ZIP por email/Drive ao contabilista

O ZIP contém só as facturas **Aprovadas** (rejeitadas/pending não interessam).

---

## Troubleshooting

| Sintoma | Causa | Fix |
|---|---|---|
| Email fica em INBOX, n8n não processa | Workflow não está Active | Toggle "Active" no canto superior direito |
| n8n: "Authentication failed" no IMAP | App Password errada ou 2FA não activo | Re-gerar App Password em myaccount.google.com/apppasswords |
| Email processado mas erro 403 no /importacoes | API key sem scope `ingest` | Recriar key em Definições → API Keys com scope correcto |
| Fatura aparece com Confidence "Falhou" | Parser não reconhece fornecedor novo | Aprovar manualmente; consultar com Bruno se precisas de parser específico |
| `wasDuplicate: true` no toast | Mesmo PDF processado antes | Normal — protege contra reenvios. SHA256 dedupe automático |
| n8n perde credentials após restart | Falta volume `./data/n8n` | Verifica `docker compose ps` que n8n tem volume montado |

---

## Custos operacionais

| Componente | Custo |
|---|---|
| n8n self-hosted | **0€** (open source) |
| Gmail | **0€** (caixa pessoal) ou Google Workspace 6€/mês se domínio próprio |
| RepairDesk API | já incluído |
| Disco PDFs | ~50KB por fatura. 1000 faturas/ano = 50MB. Negligível. |
| **Total** | **0€/mês** |

---

## Próximos fornecedores

Hoje só Tudo4Mobile tem parser específico (Sprint 124+134). Para adicionar
Molano, MTK, etc:
1. Bruno envia um PDF exemplo para mim
2. Eu escrevo extractors regex específicos no `SupplierPdfParser`
3. Deploy → o mesmo workflow n8n trata tudo automaticamente

Alternativa futura: parser declarativo onde Bruno define regex via UI sem código
(seria Sprint 151+, scope maior).
