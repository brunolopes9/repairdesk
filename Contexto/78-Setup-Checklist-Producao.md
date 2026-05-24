# 78 — Setup Checklist Produção

**Data:** 2026-05-24
**Audience:** Bruno. Lista verificável das acções operacionais (não código) para arrancar produção.

Tudo o que está documentado como "acção tua" nos Docs 70-77 está aqui consolidado por ordem de execução.

---

## Fase 1 — Antes de provisionar VPS (~30 min)

### 1.1 GitHub branch protection
- [ ] GitHub → Settings → Branches → Add rule para `main`
- [ ] ✅ Require status checks to pass before merging
  - [ ] `Backend (.NET 10)`
  - [ ] `Frontend build`
  - [ ] `Analyze csharp` (CodeQL)
  - [ ] `Analyze javascript-typescript` (CodeQL)
- [ ] ✅ Require linear history
- [ ] ✅ Do not allow bypassing the above settings
- [ ] ❌ Restrict who can push to matching branches (opcional — só Bruno é maintainer hoje)

### 1.2 CodeQL (só se repo for privado)
- [ ] Settings → Code security → Enable GitHub Advanced Security
- [ ] Custo: ~3 USD/utilizador/mês. Skip se repo for público.
- [ ] Sem isto, workflow `codeql.yml` falha com 403.

### 1.3 Sentry account
- [ ] Criar conta em https://sentry.io (free tier: 5k events/mês, suficiente para LopesTech)
- [ ] Criar projecto **RepairDesk Backend** (platform: `.NET Core`)
- [ ] Copiar DSN para 1Password como `SENTRY_DSN`
- [ ] Criar projecto **RepairDesk Frontend** (platform: `React`)
- [ ] Copiar DSN para 1Password como `VITE_SENTRY_DSN`

### 1.4 Cloudflare account + R2
- [ ] Criar conta Cloudflare (se não existe ainda)
- [ ] R2 → Create bucket `mender-prod-backups`
- [ ] **Activar versioning** no bucket — protege contra apagar por engano (Doc 76 §4)
- [ ] **Lifecycle rule**: transição a Glacier-tier após 90 dias
- [ ] API tokens → Create token "RepairDesk prod"
  - [ ] Permissions: R2 Storage Edit only no bucket acima
  - [ ] TTL: 1 ano
- [ ] Copiar `R2_ACCOUNT_ID`, `R2_ACCESS_KEY_ID`, `R2_SECRET_ACCESS_KEY` para 1Password

### 1.5 Domínio + DNS
- [ ] Registar `mender.pt` (registrar à tua escolha — Hostinger PT é barato)
- [ ] Adicionar zona em Cloudflare DNS (free plan)
- [ ] Nameservers do registrar → apontar para Cloudflare
- [ ] Aguardar propagação (~24h)

### 1.6 Secrets — gerar passwords production
Usa um gerador (1Password) — **NUNCA reusar passwords dev**:
- [ ] `DB_SA_PASSWORD` — 32 chars, evitar `$@!` que podem partir parsing shell
- [ ] `REDIS_PASSWORD` — 32 chars
- [ ] `JWT_SIGNING_KEY` — **64 chars exactos** (`openssl rand -base64 48 | head -c 64`)
- [ ] `SEED_ADMIN_PASSWORD` — 16+ chars, vais mudar no primeiro login mesmo
- [ ] `DPKEYS_BACKUP_PASSWORD` — 32+ chars; **NUNCA mudar** depois (irrecuperável)
- [ ] Guardar TODAS em 1Password vault "Mender Production"

---

## Fase 2 — Provisionar Hetzner VPS (~1 dia)

Ver `deploy/hetzner/README.md` para passos detalhados. Resumo:

### 2.1 Criar VPS
- [ ] Hetzner Cloud → New Project "mender-prod"
- [ ] Add Server:
  - Location: Helsinki (HEL1) ou Nuremberg (NBG1) — ambos EU
  - Image: Ubuntu 24.04
  - Type: **CX33** (8 GB RAM, 4 vCPU, 80 GB) — 6,49 EUR/mês sem IVA
  - Networking: IPv4 + IPv6
  - SSH key: adicionar a tua (cria local com `ssh-keygen` se não tens)
- [ ] Activar Backups (~20% do preço VPS = ~1,30 EUR/mês). **Recomendado.**
- [ ] Anotar IPv4 do VPS

### 2.2 DNS apontar
- [ ] Cloudflare DNS → A record `app.mender.pt` → IP do VPS
- [ ] **Proxy status: OFF** inicialmente (Caddy precisa de acesso directo para Let's Encrypt HTTP-01)
- [ ] Verificar `dig app.mender.pt` resolve correctamente

### 2.3 Setup inicial servidor
```bash
ssh root@<vps-ip>
# Correr o script de setup
curl -fsSL https://raw.githubusercontent.com/brunolopes9/repairdesk/main/deploy/hetzner/01-setup-server.sh | bash
# Vai instalar: docker, docker compose plugin, ufw, fail2ban, criar user "deploy"
```

### 2.4 Clone repo + .env
```bash
sudo -u deploy bash
cd /opt
git clone https://github.com/brunolopes9/repairdesk.git
cd repairdesk
cp deploy/hetzner/.env.example .env
nano .env
# Preencher TODAS as variáveis com valores do 1Password
```

### 2.5 Caddy + HTTPS automático
```bash
# Copiar Caddyfile para /etc/caddy/Caddyfile.app.mender.pt
sudo cp deploy/hetzner/Caddyfile.app.lopestech.pt /etc/caddy/Caddyfile
sudo sed -i 's/app.lopestech.pt/app.mender.pt/g' /etc/caddy/Caddyfile
# Caddy gera Let's Encrypt automático
sudo systemctl reload caddy
```

### 2.6 Pull images + start
```bash
docker login ghcr.io -u brunolopes9 -p <GHCR_PAT>
docker compose -f docker-compose.prod.yml pull
docker compose -f docker-compose.prod.yml up -d
# Aguardar ~2min para health checks ficarem green
docker compose -f docker-compose.prod.yml ps
```

### 2.7 Activar Cloudflare proxy
- [ ] Cloudflare DNS → app.mender.pt → toggle proxy status para **ON** (laranja)
- [ ] Espera 1 min, valida `curl -I https://app.mender.pt` devolve 200 + headers Cloudflare
- [ ] SSL/TLS → Encryption mode: **Full (strict)** (Caddy tem cert válido)

### 2.8 Smoke test
- [ ] `https://app.mender.pt` carrega
- [ ] Login com `bruno@...` + `SEED_ADMIN_PASSWORD`
- [ ] Sistema força mudança de password (Sprint 233)
- [ ] Navegar `/dashboard` → carrega sem erros
- [ ] Verificar headers em DevTools: `Strict-Transport-Security` + `X-Content-Type-Options` + `Content-Security-Policy` presentes

---

## Fase 3 — Hardening operacional (~30 min)

### 3.1 Cron mensal restore drill
```bash
# No VPS como user deploy
crontab -e
# Adicionar:
0 4 1 * * cd /opt/repairdesk && pwsh scripts/Restore-Drill.ps1 > /var/log/restore-drill.log 2>&1
0 5 1 * * cd /opt/repairdesk && pwsh scripts/Restore-DpKeys.ps1 --validate-only > /var/log/dp-keys-drill.log 2>&1
```

(`-ValidateOnly` implementado Sprint 254b — decrypt + verifica tar válido SEM extrair. Encontra o último backup R2 automaticamente.)

### 3.2 Better Stack Uptime (free tier)
- [ ] Criar conta https://betterstack.com/uptime
- [ ] Monitor 1: `https://app.mender.pt/api/health/live` — every 60s — alerta email se 2 fails
- [ ] Monitor 2: `https://app.mender.pt/api/health/ready` — every 5min — alerta se ≥ 1 fail
- [ ] Status page público opcional: `status.mender.pt` (sub-domínio extra)

### 3.3 GHCR PAT rotation
- [ ] Criar PAT classic com scope `read:packages` apenas
- [ ] Expiry: 90 dias
- [ ] Calendario: lembrete repetir a cada 80 dias

---

## Fase 4 — Pós-launch (semanas seguintes)

### 4.1 Rotação inicial de secrets (após confirmação que tudo funciona)
- [ ] Mudar `SEED_ADMIN_PASSWORD` via UI (force change já obriga, mas re-fazer)
- [ ] Confirmar que `.env` no VPS tem permissões `600` e owner `deploy`

### 4.2 Backup verification
- [ ] Após 24h de produção, ver se backup automático correu (`./backups/` no host)
- [ ] Após 25h, correr `Restore-Drill.ps1` manualmente uma vez para confirmar
- [ ] Login R2 → verificar que `dp-keys-YYYYMMDD-0330.tar.aes` está lá

### 4.3 Sentry sanity check
- [ ] Triggar erro propositado: `/api/health/live` com header malformado
- [ ] Verificar que aparece no Sentry dashboard
- [ ] Configurar alerta: se houver > 10 errors/hora, email Bruno

### 4.4 Compliance (RGPD)
- [ ] `/dpa` acessível e válido (Sprint 174)
- [ ] `/sub-processors` lista todos (Anthropic, Cloudflare, Moloni, Hetzner)
- [ ] Cookie banner aparece no primeiro acesso (Sprint 174)

---

## Quando isto estiver tudo ✅

Produção arranca em **~1 dia + 4h de checklist** este documento.

Custo mensal estimado:
- Hetzner CX33 + backups: ~8 EUR
- Cloudflare R2 storage: 1-3 EUR (depende volume fotos)
- Cloudflare CDN: free tier
- Sentry: free tier
- Better Stack: free tier
- **Total: ~10-15 EUR/mês** para o 1º tenant

[[reference-docker-setup]] [[project-lopestech-roadmap]]
