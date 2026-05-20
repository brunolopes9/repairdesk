# Deploy RepairDesk em Hetzner Cloud → `app.lopestech.pt`

Este passo-a-passo executa o que está documentado em `Contexto/17-Hosting-Deployment.md`. Custo total: **~€15-16/mês** (Hetzner CCX13 + backups + Cloudflare gratuito).

## Pré-requisitos

- [ ] Conta Hetzner Cloud verificada ([accounts.hetzner.com](https://accounts.hetzner.com))
- [ ] Domínio `lopestech.pt` registado com acesso ao DNS (ou conta Cloudflare a apontar para esse domínio)
- [ ] Chave SSH pública gerada na máquina local do Bruno (instruções na secção 1)
- [ ] Docker images publicadas em `ghcr.io/brunolopes9/repairdesk-api:latest` e `repairdesk-web:latest` (TODO: ainda não há pipeline — alternativa em §5)

## 1. Gerar chave SSH (Windows 11)

Abre o **PowerShell** na tua máquina (não no servidor) e corre:

```powershell
ssh-keygen -t ed25519 -C "bruno-lopestech-hetzner-2026"
```

- **File location**: aceita o default (`Enter`) → fica em `C:\Users\Utilizador\.ssh\id_ed25519`
- **Passphrase**: recomendo pôr uma (não fica em plaintext no disco). Anota no teu gestor de passwords.

Ver a chave **pública** (para colar no Hetzner):

```powershell
Get-Content $env:USERPROFILE\.ssh\id_ed25519.pub
```

Output começa com `ssh-ed25519 AAAAC3Nza...` — copia tudo (uma linha).

## 2. Criar servidor no Hetzner

No Hetzner Cloud Console, **Projects → New Project** chamado `lopestech`. Dentro do projecto:

1. **Security → SSH Keys → Add SSH Key**
   - Cola o output do passo anterior
   - Nome: `bruno-windows-2026`

2. **Servers → Add Server**:
   | Campo | Valor |
   |---|---|
   | Location | **Falkenstein (fsn1)** — menor latência a Portugal |
   | Image | **Ubuntu 24.04** |
   | Type | **CCX13** (Shared vCPU não, **Dedicated vCPU** tab) — €13/mês, 2 vCPU AMD, 8GB RAM, 80GB NVMe |
   | Networking | IPv4 + IPv6 (defaults) |
   | SSH Keys | Selecciona `bruno-windows-2026` |
   | Volumes | (skip — 80GB do servidor chega para começar) |
   | Firewalls | (skip — vamos configurar UFW dentro do servidor) |
   | Backups | **Enable** (€2.60/mês — snapshot diário, 7 dias retention) |
   | Placement groups | (skip) |
   | Labels | `env=production`, `app=repairdesk` |
   | Name | `repairdesk-prod-01` |

3. **Create & Buy now**. Vai-te dar um **IPv4 público** (anota — vais precisar).

## 3. Configurar DNS — Cloudflare em frente

### 3.1 Adicionar `lopestech.pt` ao Cloudflare (se ainda não está)

1. [dash.cloudflare.com](https://dash.cloudflare.com) → **Add a Site**
2. Domínio: `lopestech.pt`
3. Plano: **Free**
4. Cloudflare vai mostrar 2 nameservers (ex: `bob.ns.cloudflare.com`, `lola.ns.cloudflare.com`)
5. No teu registar PT (.PT ou IONOS ou onde compraste), muda os nameservers para os do Cloudflare. Demora 1-24h a propagar.

### 3.2 DNS record para `app.lopestech.pt`

Dentro do Cloudflare, em **DNS → Records → Add Record**:
- Type: `A`
- Name: `app`
- IPv4 address: `<IP do Hetzner do passo 2>`
- Proxy status: **Proxied** (laranja) — activa DDoS protection + cache
- TTL: Auto

## 4. Provisionar o servidor — primeiro login

Da tua máquina Windows, em PowerShell:

```powershell
ssh root@<IP-DO-HETZNER>
```

A primeira ligação pede confirmação da fingerprint — escreve `yes`. Vais entrar no servidor como `root`.

**Corre o script de setup** (copia e cola o bloco inteiro):

```bash
curl -fsSL https://raw.githubusercontent.com/brunolopes9/RepairDesk/main/deploy/hetzner/01-setup-server.sh -o /tmp/setup.sh
bash /tmp/setup.sh
```

> **NOTA**: se o repo RepairDesk for privado, o `curl` falha. Alternativa: copia o ficheiro `01-setup-server.sh` manualmente com `scp` (instruções no fim deste README).

O script faz:
- Update do sistema
- Cria utilizador `deploy` (não-root, com sudo)
- Copia a tua chave SSH para o utilizador `deploy`
- Configura **UFW firewall** (só 22, 80, 443 abertos)
- Instala **Docker + Docker Compose plugin**
- Instala **fail2ban** (bloqueia tentativas SSH brute-force)
- Instala **Caddy** (HTTPS automático via Let's Encrypt)
- Desactiva login SSH como `root` (forçar a usar `deploy`)
- Reboot opcional

Após o script, **fecha a sessão SSH** e re-liga como `deploy`:

```powershell
ssh deploy@<IP-DO-HETZNER>
```

## 5. Deploy do RepairDesk

```bash
sudo mkdir -p /opt/repairdesk
sudo chown deploy:deploy /opt/repairdesk
cd /opt/repairdesk

# Clona o repo (lê secção "Repo privado" se for o caso)
git clone https://github.com/brunolopes9/RepairDesk.git .

# Cria o .env de produção (NÃO está no git)
cp deploy/hetzner/.env.production.template .env.production
nano .env.production  # preenche TUDO o que tem placeholder ⬜
```

Variáveis críticas no `.env.production`:
- `DB_SA_PASSWORD` — gerar com `openssl rand -base64 32`
- `REDIS_PASSWORD` — gerar com `openssl rand -base64 24`
- `JWT_SIGNING_KEY` — gerar com `openssl rand -base64 48` (mínimo 32 chars)
- `SEED_ADMIN_EMAIL` / `SEED_ADMIN_PASSWORD` — credenciais do admin inicial (Bruno)
- `Backup__R2__Bucket` — bucket Cloudflare R2 para backups (configurar antes — ver `Contexto/14-Storage-Fotos.md`)
- `IMAGE_TAG` — qual tag das images vai correr (ex: `latest` ou um SHA específico)

### 5.1 Opção A — usar images publicadas em ghcr.io (recomendado)

Se já tens pipeline CI a publicar `ghcr.io/brunolopes9/repairdesk-api:latest`:

```bash
# Login no GitHub Container Registry (precisa de PAT com read:packages)
echo $GHCR_PAT | docker login ghcr.io -u brunolopes9 --password-stdin

# Arranca tudo
docker compose -f docker-compose.prod.yml --env-file .env.production up -d
```

### 5.2 Opção B — build local no servidor (sem CI)

Se ainda não tens pipeline, build no próprio servidor:

```bash
# Build api + web localmente
docker compose -f docker-compose.yml build api web

# Tag para o nome esperado pelo prod.yml
docker tag repairdesk-api:latest ghcr.io/brunolopes9/repairdesk-api:local
docker tag repairdesk-web:latest ghcr.io/brunolopes9/repairdesk-web:local

# IMAGE_TAG=local no .env.production
docker compose -f docker-compose.prod.yml --env-file .env.production up -d
```

CCX13 tem 8GB RAM e 2 vCPU — build do .NET demora ~5-8 min, normal.

### 5.3 Verificar saúde

```bash
docker compose -f docker-compose.prod.yml ps
docker compose -f docker-compose.prod.yml logs -f api
```

Espera health check `healthy` em todos os 4 serviços (db, cache, api, web).

## 6. Configurar Caddy (HTTPS automático)

**Arquitectura** (já confirmada no `frontend/nginx.conf` linha 22):

```
internet → Cloudflare (proxied) → Caddy :443 → web container :8088 (nginx)
                                                  ├─ /         → React SPA
                                                  └─ /api/*    → api container :8080
```

O nginx do `web` container já faz proxy interno de `/api/` para `api:8080`. O Caddy só precisa de passar tudo ao `web`.

Substitui o stub:

```bash
sudo cp /opt/repairdesk/deploy/hetzner/Caddyfile.app.lopestech.pt /etc/caddy/Caddyfile
sudo caddy validate --config /etc/caddy/Caddyfile
sudo systemctl reload caddy
```

Caddy pede certificado Let's Encrypt automaticamente no primeiro request a `app.lopestech.pt` (HTTP-01 challenge via porta 80).

**Cloudflare SSL/TLS mode**: como vamos usar Cloudflare em modo "Proxied" (laranja), no dashboard Cloudflare → **SSL/TLS → Overview**, escolhe **Full (strict)**.

| Modo | Significado | Usar? |
|---|---|---|
| Flexible | Cloudflare → Servidor em HTTP plain. **Inseguro**. | ❌ |
| Full | Cloudflare aceita qualquer cert no servidor (incl. self-signed). | ❌ |
| **Full (strict)** | Cloudflare exige cert válido. Caddy serve Let's Encrypt → válido. | ✅ |

## 7. Testar

```powershell
# Da tua máquina Windows
curl https://app.lopestech.pt/api/health/ready
```

Esperado: `{"status":"Healthy"}` ou similar.

Browser: `https://app.lopestech.pt` → frontend deve carregar.

## 8. Configurar a loja para apontar para a nova API

No projecto loja Vercel:
- **Settings → Environment Variables**:
  - `REPAIRDESK_API_URL` = `https://app.lopestech.pt`
  - `REPAIRDESK_API_KEY` = chave válida (criar no RepairDesk depois do primeiro login admin)
  - `REPAIRDESK_WEBHOOK_SECRET` = `whsec_...` da subscription criada no RepairDesk
- **Redeploy** o último deploy.

## 9. Hardening pós-deploy (recomendado mas não bloqueante)

- [ ] Adicionar uptime monitor: [uptimerobot.com](https://uptimerobot.com) free → ping a `https://app.lopestech.pt/api/health/ready` cada 5min, alerta email/SMS se cair
- [ ] Configurar Cloudflare WAF rules: **Security → WAF → Rate limiting** (50 req/min por IP em `/api/*`)
- [ ] Activar **Cloudflare Bot Fight Mode** (free) — Security → Bots
- [ ] Subscrever Hetzner status emails
- [ ] Schedule manual: testar restore de backup mensalmente (ver `Contexto/18-Backup-DR.md`)

## Anexo: Repositório privado

Se o repo `RepairDesk` no GitHub é privado, há 2 opções:

**Opção A — SSH deploy key**:
```bash
# No servidor, gerar key dedicada
ssh-keygen -t ed25519 -f /home/deploy/.ssh/repairdesk_deploy -N ""
cat /home/deploy/.ssh/repairdesk_deploy.pub
```
Cola essa pub key em GitHub → `RepairDesk` repo → **Settings → Deploy keys → Add deploy key** (read-only).

Depois clona com SSH em vez de HTTPS:
```bash
GIT_SSH_COMMAND='ssh -i /home/deploy/.ssh/repairdesk_deploy' \
  git clone git@github.com:brunolopes9/RepairDesk.git /opt/repairdesk
```

**Opção B — PAT temporário**:
```bash
git clone https://<PAT>@github.com/brunolopes9/RepairDesk.git /opt/repairdesk
```
Pior, mas serve para 1.º deploy.

## Anexo: SCP de ficheiros locais para o servidor

Se preferires não clonar o repo e mandar ficheiros via SCP:

```powershell
# Da tua máquina Windows
scp -r "C:\Users\Utilizador\Desktop\LopesTech\RepairDesk" deploy@<IP>:/opt/repairdesk
```

## Anexo: Operação contínua

| Tarefa | Comando |
|---|---|
| Ver logs api | `docker compose -f docker-compose.prod.yml logs -f api` |
| Reiniciar api | `docker compose -f docker-compose.prod.yml restart api` |
| Update das images | `docker compose -f docker-compose.prod.yml pull && docker compose -f docker-compose.prod.yml up -d` |
| Backup manual SQL | Ver `Contexto/18-Backup-DR.md` |
| Restore SQL | Ver `Contexto/18-Backup-DR.md` |
| Snapshot Hetzner | Console Hetzner → Server → Snapshots → Take Snapshot |
