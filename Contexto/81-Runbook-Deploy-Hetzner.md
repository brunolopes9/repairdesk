# 81 — Runbook Deploy Hetzner → `app.lopestech.pt`

Bruno: este é o passo-a-passo concreto para tu correres. Sigue por ordem.
Tempo estimado: 60-90 minutos no total (a maior parte é esperar builds/DNS).

## Estado actual (já feito pelo Claude do shop)

- ✅ Servidor Hetzner Cloud `mender-prod` (CPX32, Ubuntu 24.04)
- ✅ IP público: `178.105.100.96` · IPv6: `2a01:4f8:c015:fe4::1`
- ✅ Backups Hetzner ON (diários)
- ✅ Docker 29.5.2 + Compose v5.1.4 instalados
- ✅ Firewall Hetzner Cloud: só TCP 22/80/443 + ICMP
- ✅ SSH key ed25519 do Bruno (com passphrase)

## O que falta (faz tudo o que está abaixo)

---

## Passo 1 — Completar setup do servidor (Caddy, fail2ban, user deploy)

Da tua máquina Windows, SSH para o servidor como root:

```powershell
ssh root@178.105.100.96
```

(Pede passphrase da tua chave ed25519.)

No servidor, corre o resto do setup (Docker já existe, vai detectar e saltar):

```bash
curl -fsSL https://raw.githubusercontent.com/brunolopes9/repairdesk/main/deploy/hetzner/01-setup-server.sh -o /tmp/setup.sh
bash /tmp/setup.sh
```

Isto:
- Cria user `deploy` (sudo sem password) + copia tua SSH key
- Instala Caddy (HTTPS automático via Let's Encrypt)
- Instala fail2ban
- Hardening SSH (desactiva root login e password auth)
- Tunes para SQL Server (vm.max_map_count) e Redis

No fim, **sai e re-liga como `deploy`**:

```powershell
ssh deploy@178.105.100.96
```

⚠️ **Atenção**: depois disto, root login fica desactivado. Se a sessão `deploy` não funcionar, tens reflog para recuperar via Hetzner Console (rescue mode).

---

## Passo 2 — Gerar todos os secrets

Na tua máquina Windows (não no servidor), abre PowerShell:

```powershell
# Helper para gerar secrets
function NewSecret {
    param([int]$Bytes = 32)
    [Convert]::ToBase64String((1..$Bytes | ForEach-Object { Get-Random -Maximum 256 }))
}

Write-Host "DB_SA_PASSWORD     = $(NewSecret 24)"
Write-Host "REDIS_PASSWORD     = $(NewSecret 18)"
Write-Host "JWT_SIGNING_KEY    = $(NewSecret 36)"
Write-Host "SEED_ADMIN_PASSWORD = $(NewSecret 16)"
Write-Host "DPKEYS_BACKUP_PASS = $(NewSecret 24)"
Write-Host "API_KEY_SHOP       = $(NewSecret 32)"
Write-Host "WEBHOOK_SECRET_SHOP = $(NewSecret 24)"
```

**Guarda os 7 valores no teu gestor de passwords (1Password).** Não os percas — perder `DPKEYS_BACKUP_PASS` significa que o backup das DataProtection keys fica inútil.

---

## Passo 3 — Configurar Cloudflare R2 (storage)

1. Abre [dash.cloudflare.com](https://dash.cloudflare.com) → R2 Object Storage
2. **Create bucket**: `repairdesk-photos-prod` (location: Auto)
3. **Create bucket**: `repairdesk-backups-prod` (bucket separado para backups)
4. **API → Create API Token**:
   - Permissions: **Admin Read & Write**
   - Buckets: ambos (ou "All buckets in account")
5. Copia o **Access Key ID**, **Secret Access Key**, **Account ID** (mostrado no topo do dashboard R2)
6. Guarda os 3 no gestor de passwords

---

## Passo 4 — Apontar DNS Cloudflare

No Cloudflare → `lopestech.pt` → **DNS → Records → Add**:

| Campo | Valor |
|---|---|
| Type | `A` |
| Name | `app` |
| IPv4 | `178.105.100.96` |
| Proxy | **DNS only** (cinzento) durante setup — muda para Proxied (laranja) depois de verificares HTTPS |
| TTL | Auto |

Confirma propagação:
```powershell
nslookup app.lopestech.pt
```
Deve mostrar `178.105.100.96`. Pode demorar 5-15 min.

**Depois disto**, em Cloudflare → SSL/TLS → Overview → **Full (strict)**.

---

## Passo 5 — Clonar repo + preencher .env.production

No servidor (`deploy@...`):

```bash
sudo mkdir -p /opt/repairdesk
sudo chown deploy:deploy /opt/repairdesk
cd /opt/repairdesk

# Clone — repo é público, HTTPS funciona
git clone https://github.com/brunolopes9/repairdesk.git .

# Copia template + edita
cp deploy/hetzner/.env.production.template .env.production
nano .env.production
```

Preenche **TODOS** os ⬜ placeholders com os valores que geraste no Passo 2 e 3.
Confirma especialmente:

```bash
# Mender domínio (override default do compose que é app.mender.pt)
JWT_ISSUER=https://app.lopestech.pt
JWT_AUDIENCE=https://app.lopestech.pt
CORS_ORIGIN_PRIMARY=https://app.lopestech.pt
FRONTEND_BASE_URL=https://app.lopestech.pt

# Image tag — vais usar tag git criada no Passo 7
IMAGE_TAG=v0.1.0
IMAGE_NAMESPACE=brunolopes9/repairdesk

# AT cert — Bruno tem em finanças/ local; copiar via scp depois
AT_CERT_PATH=/run/secrets/at/cert.pem
AT_KEY_PATH=/run/secrets/at/key.pem
AT_KEY_PASSWORD=...
```

Save (Ctrl+O, Enter, Ctrl+X).

**Permissões restritivas**:
```bash
chmod 600 .env.production
```

---

## Passo 6 — Copiar certificado AT para o servidor

Da tua máquina Windows, copia o cert AT (em `finanças/secrets/`) para o servidor:

```powershell
# Cria directório no servidor
ssh deploy@178.105.100.96 "sudo mkdir -p /opt/repairdesk/secrets/at && sudo chown deploy:deploy /opt/repairdesk/secrets/at"

# Copia cert + key
scp "C:\Users\Utilizador\Desktop\LopesTech\finanças\secrets\cert.pem" deploy@178.105.100.96:/opt/repairdesk/secrets/at/
scp "C:\Users\Utilizador\Desktop\LopesTech\finanças\secrets\key.pem" deploy@178.105.100.96:/opt/repairdesk/secrets/at/
```

(Ajusta paths se forem diferentes; vê em `finanças/` local.)

No servidor:
```bash
chmod 600 /opt/repairdesk/secrets/at/*.pem
```

---

## Passo 7 — Configurar secrets GitHub Actions

No GitHub → `brunolopes9/repairdesk` → **Settings → Secrets and variables → Actions → New repository secret**:

| Nome | Valor |
|---|---|
| `PROD_SSH_HOST` | `178.105.100.96` |
| `PROD_SSH_USER` | `deploy` |
| `PROD_SSH_KEY` | conteúdo de `C:\Users\Utilizador\.ssh\id_ed25519` (chave PRIVADA) |
| `PROD_SSH_PORT` | `22` |
| `PROD_DEPLOY_PATH` | `/opt/repairdesk` |
| `PROD_API_URL` | `https://app.lopestech.pt` |

Para a chave SSH:
```powershell
Get-Content $env:USERPROFILE\.ssh\id_ed25519 | Set-Clipboard
```
(Cola no secret. Inclui as linhas `-----BEGIN/END OPENSSH PRIVATE KEY-----`.)

---

## Passo 8 — Primeiro deploy (tag v0.1.0)

Da tua máquina Windows, no repo local:

```powershell
cd C:\Users\Utilizador\Desktop\LopesTech\RepairDesk
git tag v0.1.0
git push origin v0.1.0
```

O workflow `.github/workflows/deploy-production.yml` arranca automaticamente:
1. Build de `repairdesk-api:v0.1.0` + `repairdesk-web:v0.1.0`
2. Push para `ghcr.io/brunolopes9/repairdesk/...`
3. SSH para servidor + `docker compose pull && up -d`
4. Smoke test: `curl https://app.lopestech.pt/api/health/ready`

Acompanha em **Actions** tab no GitHub. ~10-15 min total.

---

## Passo 9 — Configurar Caddy (HTTPS Let's Encrypt)

Quando o deploy acabar, no servidor:

```bash
sudo cp /opt/repairdesk/deploy/hetzner/Caddyfile.app.lopestech.pt /etc/caddy/Caddyfile
sudo caddy validate --config /etc/caddy/Caddyfile
sudo systemctl reload caddy
```

Caddy obtém certificado Let's Encrypt automaticamente no primeiro request.

**Testa**:
```powershell
# Da tua máquina Windows
curl https://app.lopestech.pt/api/health/ready
```

Esperado: `{"status":"Healthy"}` ou similar (HTTP 200).

Browser: `https://app.lopestech.pt` deve carregar a página de login Mender.

---

## Passo 10 — Login admin + criar API key para shop

1. Abre `https://app.lopestech.pt`
2. Login com `SEED_ADMIN_EMAIL` + `SEED_ADMIN_PASSWORD` (do `.env.production`)
3. **MUDA a password admin imediatamente** (Definições → Empresa → User)
4. Vai a **Definições → API Keys → Criar**:
   - Nome: `shop-lopestech-prod`
   - Scopes: external products + webhooks
5. **Copia a chave** (mostrada UMA VEZ apenas) — guarda no gestor de passwords como `API_KEY_SHOP_REAL`
6. Vai a **Definições → Webhooks → Criar subscription**:
   - URL: `https://lopestech-shop.vercel.app/api/webhooks/repairdesk` (confirma URL real com shop Claude)
   - Eventos: `phones.adicionado`, `phones.atualizado`, `phones.removido`
   - HMAC secret: gerado automaticamente — **copia** como `WEBHOOK_SECRET_SHOP_REAL`

---

## Passo 11 — Dar credenciais ao shop

No Vercel → projecto `lopestech-shop` → **Settings → Environment Variables**:

| Nome | Valor |
|---|---|
| `REPAIRDESK_API_URL` | `https://app.lopestech.pt` |
| `REPAIRDESK_API_KEY` | valor de `API_KEY_SHOP_REAL` do Passo 10 |
| `REPAIRDESK_WEBHOOK_SECRET` | valor de `WEBHOOK_SECRET_SHOP_REAL` do Passo 10 |

**Redeploy** o último deploy do shop.

O cron do shop começa a sincronizar com a API real do Mender.

---

## Passo 12 — Validação end-to-end

```powershell
# 1. API responde
curl https://app.lopestech.pt/api/health/ready

# 2. External products devolve algo
curl https://app.lopestech.pt/api/external/products -H "X-Api-Key: <API_KEY_SHOP_REAL>"

# 3. Webhook fire — fazer alteração a um produto no /produtos e ver
#    deliveries em /webhooks/deliveries
```

Verifica que o shop apanha os 79 produtos no próximo cron (~1-6h depois).

---

## Anexo: Cloudflare proxy (laranja)

Depois de confirmares que HTTPS funciona com Caddy directo (Passo 9), volta ao Cloudflare DNS e muda o registo A `app` para **Proxied (laranja)** — activa DDoS protection + cache global.

---

## Anexo: O que faço quando algo falha

| Sintoma | Acção |
|---|---|
| SSH não conecta após setup-server | Hetzner Console → rescue mode, refaz `/etc/ssh/sshd_config` |
| Caddy `cert obtain failed` | Confirma DNS aponta para 178.105.100.96 + porta 80 aberta na firewall Hetzner |
| API container `healthcheck failed` | `docker compose logs api` — provavelmente connection string ou JWT key inválida |
| Smoke test `connection refused` | Caddy ainda não tem cert — espera 30s + retry |
| 502 Bad Gateway | Container web morreu ou nginx config — `docker compose ps`, `logs web` |

---

## Custos mensais estimados

| Item | Custo |
|---|---|
| Hetzner CPX32 + backups | ~€16/mês |
| Cloudflare R2 (até 10GB grátis) | €0/mês inicialmente |
| Cloudflare DNS + WAF | €0/mês (free tier) |
| Domínio `lopestech.pt` | ~€10/ano |
| **Total** | **~€16-20/mês** |
