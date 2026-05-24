# 76 — Disaster Recovery Runbook

**Data:** 2026-05-24
**Audience:** Bruno (operador único actualmente). Documento prático, não académico.

**Objectivo:** quando o sistema cair / VPS arder / dados corromperem-se, este runbook leva-te do zero até produção restaurada em ~2-4 horas com o último backup.

---

## Recovery objectives

| Métrica | Target | Realista 2026 |
|---|---|---|
| RPO (perda de dados aceitável) | ≤ 24h | 24h (último backup às 03:00) |
| RTO (tempo até voltar) | ≤ 4h | 2-4h |
| MTTD (detectar incidente) | ≤ 15min | depende: monitoring P2 sprint 252 reduz para <5min |

Reduzir RPO para <1h exige transaction log shipping → futuro (SQL Server Standard ou Postgres).

---

## Inventário do que tens em backup off-VPS

| Recurso | Localização | Acesso |
|---|---|---|
| DB completo `.bak` | Cloudflare R2 `{bucket}/backups/{yyyy}/{mm}/RepairDesk-yyyymmdd-hhmm.bak` | `R2_ACCESS_KEY` + `R2_SECRET` |
| Fotos reparação | Cloudflare R2 `{bucket}/photos/...` | Idem |
| Facturas fornecedor PDFs | R2 `{bucket}/supplier-invoices/...` | Idem |
| DataProtection keys | **NÃO REPLICADO** — só no volume `dp_keys` no VPS | Ver §3 |
| Código + workflows | GitHub `brunolopes9/repairdesk` | `git clone` |
| Docker images | GHCR `ghcr.io/brunolopes9/repairdesk-{api,web}:vX.Y.Z` | `docker login` |
| Secrets prod (`.env`) | **NÃO no git**. Cópia em 1Password / file local | Manual |
| Cloudflare DNS | Cloudflare account | Login |
| Domínio | Registrar (verificar quem) | Login |

**Único ponto frágil:** DataProtection keys. Se o volume `dp_keys` desaparecer, **todos os secrets cifrados em DB tornam-se ilegíveis** (Moloni refresh token, Anthropic key per-tenant, OAuth state cache).

⚠️ **Acção P0 futura:** copiar `dp_keys` para R2 diariamente, encriptado com password offline. Ver Sprint futuro a criar.

---

## Cenários de DR

### 1. VPS reboot / docker crash (downtime <1h)
```bash
ssh <bruno>@<vps>
cd /opt/repairdesk
docker compose -f docker-compose.prod.yml ps
docker compose -f docker-compose.prod.yml restart api web
docker compose -f docker-compose.prod.yml logs --tail=200 api
```

Se health check `/api/health/ready` 200 → resolvido. Caso contrário: §2.

### 2. DB corrompido / fica unreachable
Sintomas: api logs `Cannot open database` / 500s sustained / connection timeouts.

```bash
ssh <bruno>@<vps>
cd /opt/repairdesk
# 1. Stop api+web para ninguém escrever durante restore
docker compose -f docker-compose.prod.yml stop api web

# 2. Encontrar último backup local
ls -lh ./backups/*.bak | tail -3

# 3. Se backup local fresco (<24h), usa-lo:
pwsh ./scripts/Restore-Drill.ps1 -BackupFile ./backups/RepairDesk-YYYYMMDD.bak
# Se OK → segue para passo 5

# 4. Se backup local stale ou corrompido, vai buscar a R2:
R2_ACCOUNT_ID=... R2_ACCESS_KEY_ID=... R2_SECRET_ACCESS_KEY=... \
R2_BUCKET=mender-prod-backups DB_SA_PASSWORD=... \
./scripts/restore-from-r2.sh backups/YYYY/MM/RepairDesk-YYYYMMDD-0300.bak RepairDesk

# 5. Start api+web
docker compose -f docker-compose.prod.yml up -d api web
docker compose -f docker-compose.prod.yml logs --tail=100 api

# 6. Smoke test
curl -s https://app.mender.pt/api/health/ready | jq .
curl -s https://app.mender.pt/api/health/db | jq .
```

**RPO esperado:** até 24h (último backup das 03:00).

### 3. VPS perdida completamente (ex.: Hetzner zone down, conta suspensa)
Este é o pior caso. Steps:

```bash
# 1. Provisionar nova VPS Hetzner (qualquer região EU disponível)
#    Seguir Doc 17 §4 setup-server.sh

# 2. Configurar DNS no Cloudflare:
#    Apontar app.mender.pt para IP da nova VPS
#    TTL 60s para propagar rápido

# 3. Clonar repo na nova VPS
ssh root@<nova-vps>
cd /opt
git clone https://github.com/brunolopes9/repairdesk.git
cd repairdesk

# 4. Recuperar .env de prod (1Password / cópia offline)
#    CRITICAL: tem que conter exactamente os mesmos JWT_SIGNING_KEY,
#    senão sessions actuais falham E DataProtection não consegue ler
#    qualquer key cifrada (Moloni, etc).

# 5. Recuperar DataProtection keys (se cópia existir em R2 encriptada)
mkdir -p ./data/dp-keys
# ... fetch + decrypt dp-keys-YYYYMMDD.tar.gz.gpg de R2 ...

# 6. Pull image versão actual em produção
docker login ghcr.io -u brunolopes9
docker compose -f docker-compose.prod.yml pull

# 7. Restore último .bak da R2
R2_BUCKET=mender-prod-backups ./scripts/restore-from-r2.sh \
  backups/YYYY/MM/RepairDesk-YYYYMMDD-0300.bak RepairDesk

# 8. Start
docker compose -f docker-compose.prod.yml up -d

# 9. Aguardar Caddy emitir Let's Encrypt cert (~30s)
docker logs caddy --tail 50

# 10. Smoke test
curl -sv https://app.mender.pt/api/health/live
```

**RPO:** 24h. **RTO:** 2-4h (1h de Hetzner provisioning + 1h restore).

### 4. Storage R2 down ou bucket apagado
Fotos param de carregar (`<img>` falham) mas a app continua a funcionar — não é catastrófico.

```bash
# Se foram tu/Bruno que apagaste o bucket por engano:
# R2 tem versioning desactivado por default — fica perdido.
# ⚠️ Acção: activar versioning + lifecycle rules no R2 ANTES de prod.

# Se foi falha temporária Cloudflare:
# Esperar — RTO depende deles. Status: https://www.cloudflarestatus.com/
```

⚠️ **Acção P1:** activar R2 versioning antes de produção (Sprint 251 setup).

### 5. Dados corrompidos por bug (ex.: migration partiu schema)
Trata-se de uma **regressão lógica**, não falha física. Steps:

```bash
# 1. Identificar último estado bom — git log das migrations
docker compose exec api dotnet ef migrations list

# 2. Se a migration X partiu coisas, NÃO fazer "remove migration" em prod.
#    Em vez disso: restore último .bak ANTES da migration ter corrido.
#    Encontra o backup .bak imediatamente anterior ao deploy:
ls -lh ./backups/*.bak

# 3. Stop, restore, manter API parada
docker compose -f docker-compose.prod.yml stop api
pwsh ./scripts/Restore-Drill.ps1  # confirma que .bak alvo é restaurável
# ... restaurar mesmos passos cenário §2 ...

# 4. Fix o código em local + PR + deploy nova versão
#    O deploy production workflow já vai correr migrations no startup
```

---

## Procedimentos preventivos (PRÉ-incidente)

### Diário (automático)
- 03:00 — `BackupHostedService` corre `BACKUP DATABASE` → `./backups/` + upload R2
- Health checks Better Stack pingam `/api/health/live` a cada 60s (Sprint 252 P2)

### Mensal (manual ou cron)
```bash
# Dia 1 às 04:00, no VPS:
0 4 1 * * cd /opt/repairdesk && pwsh scripts/Restore-Drill.ps1 \
  > /var/log/restore-drill.log 2>&1

# Output → email Bruno se exit != 0
```

### Trimestral (planeado, com calendário)
- **Tabletop DR exercise**: ler este runbook + simular cenário §3 (sem fazer realmente — só validar que o conhecimento existe e os secrets estão acessíveis).
- Verificar `1Password` tem actualmente:
  - `.env` produção
  - DataProtection backup password
  - Hetzner API token (para criar nova VPS via API se necessário)
  - Cloudflare API token
  - GHCR PAT para `docker pull`

### Anual
- Rotar `JWT_SIGNING_KEY` (causa logout global mas refresh de prática).
- Rotar `DB_SA_PASSWORD`, `REDIS_PASSWORD`.
- Auditar quem tem SSH ao VPS.

---

## Contactos / escalation

Single operator: Bruno (bruno.miguel.martins.lopes@gmail.com).

Quando Mender tiver 5+ tenants pagantes, configurar:
- Status page público (status.mender.pt) — Better Stack tem free tier
- Suporte: email + SLA escrita

---

## Lições aprendidas (atualizar à medida que incidentes ocorrem)

_(empty)_ — primeiro incidente real ainda não aconteceu (em produção).

---

[[reference-docker-setup]] [[project-lopestech-roadmap]] [[feedback-destructive-ops-confirm]]
