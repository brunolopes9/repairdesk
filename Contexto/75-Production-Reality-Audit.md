# 75 — Production Reality Audit (full-stack pipeline)

**Data:** 2026-05-24
**Pedido Bruno:** ir uma a uma pelas 12 áreas de "full-stack production reality", verificar o que está implementado vs industry standard, e propor próximos passos.

**Contexto:** Mender (RepairDesk) está pré-beta. LopesTech é o único tenant em dogfooding local (Docker Compose). Sem produção ainda. Stack: .NET 10 + EF Core + SQL Server backend, React 19 + Vite + Tailwind frontend, Redis cache, R2 storage.

---

## Sumário maturidade (1=embrião, 5=production-grade)

| # | Área | Maturidade | Próximo passo crítico |
|---|---|---|---|
| 1 | Frontend / APIs / backend logic | 4 | Adicionar Sentry browser + monitoring frontend |
| 2 | Database & storage | 4 | Migrar SQL Server → Postgres (custo) ou validar Hetzner backups |
| 3 | Auth & permissions | 5 | Já fechado (Sprints 238-245) — sem acção |
| 4 | Hosting & development | 3 | Provisionar Hetzner VPS + Caddy (Doc 17 plano) |
| 5 | Cloud & compute | 2 | Mesmo VPS único — sem cloud autoscaling |
| 6 | CI/CD & version control | 4 | Adicionar branch protection + required reviews em main |
| 7 | Security & RLS | 5 | Doc 72+73+74 fecharam — sem acção |
| 8 | Rate limiting | 4 | Distribuído via Redis quando houver >1 instância |
| 9 | Caching & CDN | 3 | Frontend via Cloudflare CDN; output cache backend opcional |
| 10 | Load balancing & scaling | 1 | N/A até segundo cliente; planear quando vier |
| 11 | Error tracking & logs | 3 | Sentry backend + frontend (faltam) |
| 12 | Availability & recovery | 3 | Backup R2 OK; restore drill nunca feito |

---

## 1. Frontend / APIs / backend logic

### Estado actual
- **Frontend:** React 19 + Vite 7 + TypeScript strict + Tailwind v4 + React Query 5 + Recharts + Sonner. PWA com `vite-plugin-pwa` (Sprint 194). Build via nginx Alpine. SPA serve estáticos no porto 80.
- **APIs:** ASP.NET Core 10 (Controllers + ApiController convention). 35 controllers; matriz documentada em `Contexto/71-Roles-Matrix.md`. Swagger só em Development. Validação via FluentValidation. Error handling unificado em `ProblemDetailsExceptionMiddleware`.
- **Backend logic:** Clean Architecture (`Core` / `Services` / `DAL` / `Infrastructure` / `API` / `Common`). EF Core 10. 358 testes verdes (xUnit + WebApplicationFactory + FluentAssertions).

### Industry standard
✅ Tem testes E2E (Playwright Sprint 75), unit + integration (.NET).
✅ Tem typed API client (gerado manualmente em `frontend/src/lib/**/api.ts`).
⚠️ Não tem OpenAPI client codegen (Swagger só Dev).
⚠️ Frontend sem error boundary global visível ao utilizador (toast Sonner cobre erros API).

### Próximos passos
1. **P1 — Sentry browser SDK** no frontend (`@sentry/react`) para capturar erros não-tratados, performance traces (LCP/CLS/FCP), e replay sessions. **Não** rodar em dev. 30min setup + variáveis env.
2. **P2 — Error boundary global React** com fallback UI + envio para Sentry.
3. **P3 — OpenAPI codegen** quando 3º tenant aparecer (vale para SDK público dos lojistas).

---

## 2. Database & Storage

### Estado actual
- **DB:** SQL Server 2022 Express em container (`db_data` volume). Migrations EF Core gerenciadas. `DbInitializer` corre `MigrateAsync` no startup.
- **Storage de fotos/PDFs:** `IPhotoStorage` interface com 2 providers — `LocalFileSystemPhotoStorage` (`/data/photos`) e R2 via S3 SDK (Sprint 169). Bucket privado, signed URLs para download.
- **Backups:** `BackupHostedService` (Sprint 167) faz `BACKUP DATABASE` SQL Server diário às 03h → `./backups/*.bak` + upload R2. Retention 30d local + 90d R2 (Sprint 167 + 231b catch-up).

### Industry standard
✅ Backups automáticos com retention dual (local + cloud).
✅ Storage abstraction permite swap de provider.
⚠️ SQL Server Express tem limite 10 GB por DB. Suficiente para LopesTech mas teremos que migrar para Standard ou Postgres quando 5+ tenants.
⚠️ **Restore drill nunca foi feito.** Backups existem mas nunca foram testados.

### Próximos passos
1. **P0 — Restore drill mensal**: comando manual `restore-backup.ps1` que testa o último .bak em DB de staging. Faltam 30min para automatizar.
2. **P1 — Postgres migration plan** quando atingir 8 GB DB ou 5 tenants. Avaliar Supabase vs DIY. EF Core 10 suporta ambos.
3. **P2 — R2 lifecycle policies** para auto-archive backups antigos para Glacier-tier.

---

## 3. Auth & Permissions

### Estado actual (já auditado Doc 70 + 72)
✅ JWT 15min + Refresh 7d com rotation + idle timeout 30d (Sprint 241)
✅ Lockout 5/15min + rate-limit `auth-strict` 5/15min (Sprint 233)
✅ Multi-auth JWT + ApiKey via PolicyScheme "Multi"
✅ `[Authorize(Roles="Admin")]` em 30+ endpoints write críticos (Sprints 243-245)
✅ Matriz roles documentada + snapshot test (`Contexto/71-Roles-Matrix.md`)
✅ 297+ testes auth (Sprint 242 cross-tenant + 245 cross-role)
✅ Refresh revoke on password change/deactivate
✅ Cookie SameSite=Strict em Prod, Secure dinâmico via scheme

### Próximos passos
- Sem acção crítica. Doc 72 Fase D (roles granulares Tech/Cashier/ReadOnly) adiada para tenant com 3+ users.

---

## 4. Hosting & Development

### Estado actual
- **Development:** Docker Compose local (`docker-compose.yml`). MS SQL Server, Redis, API, web em containers. Volumes persistentes. n8n opcional via profile.
- **Production target:** Hetzner Cloud VPS EU + Docker Compose + Caddy + Cloudflare DNS/proxy (Doc 17). Há `deploy/hetzner/01-setup-server.sh` + `Caddyfile.app.lopestech.pt` + `docker-compose.bind-host.yml`.
- **CI/CD images:** GHCR images `repairdesk-api` + `repairdesk-web` publicadas via tag `v*.*.*` (workflow `deploy-production.yml`).

### Industry standard
✅ Infrastructure-as-code parcial (scripts shell + Caddyfile no git).
✅ Imagens versionadas com semantic versioning.
⚠️ **Não há VPS provisionado ainda.** Tudo corre local.
⚠️ Sem Terraform/Pulumi para reprodução automática.

### Próximos passos
1. **P0 — Provisionar Hetzner VPS** (CX33 8GB ~6,49€/mês). Seguir Doc 17 §4. Estimativa: 1 dia para 1ª produção.
2. **P0 — DNS `app.mender.pt` apontando para VPS** via Cloudflare. Caddy gera Let's Encrypt automático.
3. **P1 — Documentar disaster-recovery runbook** em `Contexto/76-DR-Runbook.md`: como restaurar do zero com último backup.
4. **P2 — Terraform/OpenTofu module** para VPS + DNS quando houver 2º ambiente (staging).

---

## 5. Cloud & Compute

### Estado actual
- **Compute:** zero. Tudo local em desktop Bruno.
- **Cloud services em uso:** Cloudflare R2 (storage), Anthropic API (LLM features), Moloni/InvoiceXpress (billing), AT Portal Finanças (NIF lookup).
- **Sem:** AWS, Azure, GCP, Kubernetes, Lambda, Cloud Run.

### Industry standard
Para SaaS B2B early-stage, **single VPS é correcto.** Cloud autoscaling é over-engineering enquanto não houver mais que ~10 tenants.

### Próximos passos
1. **P0 — Acima (área 4) — VPS Hetzner**.
2. **P2** — quando 20+ tenants: separar DB para Hetzner Managed Postgres OU migrar API para Hetzner Robot dedicated server. Não antes.
3. **P3 — Multi-region** só faz sentido a partir de 100+ tenants.

---

## 6. CI/CD & Version Control

### Estado actual
- **Git:** GitHub `brunolopes9/repairdesk`. Branch `main` activa. Branches `codex/sprint-*` para Codex Cloud worktrees.
- **CI:** `.github/workflows/ci.yml` — backend (.NET test), frontend (build), em PRs e push para main.
- **E2E:** `e2e.yml` — Playwright em PRs (Sprint 75).
- **Deploy:** `deploy-staging.yml` + `deploy-production.yml` — build + push GHCR images via tag.
- **Conventional commits parciais** — mensagens semi-estruturadas (Sprint N: descrição).
- **No-verify protection:** memory rule (feedback `codex_bugs_recorrentes`).

### Industry standard
✅ CI em PRs.
✅ E2E em CI.
✅ Imagens versionadas + immutable tags.
⚠️ **Sem branch protection rules** (qualquer push para main passa).
⚠️ Sem required reviews (Bruno é o único reviewer).
⚠️ Sem CODEOWNERS.
⚠️ Sem SAST/SCA automatizado (CodeQL/Dependabot).

### Próximos passos
1. **P1 — Branch protection em `main`**: require CI pass + linear history + no force-push. Setup via GitHub UI ~5min.
2. **P1 — Dependabot** para frontend (npm) + backend (NuGet). YAML simples em `.github/dependabot.yml`. Já há PRs Dependabot abertos (visto na review anterior).
3. **P2 — CodeQL** scan em PRs (`github/codeql-action`) para detectar SQL injection, XSS, CWE patterns. Free para repos públicos; ~$3/mo para privados.
4. **P3 — Conventional Commits enforced** via commitlint + pre-commit hook.

---

## 7. Security & RLS

### Estado actual (já auditado Doc 70, 72, 73, 74)
✅ Security headers (Doc 74 Sprint 249): CSP, HSTS, X-Frame-Options, COOP/CORP, Permissions-Policy
✅ CORS apertado para origens explícitas (Doc 74)
✅ RLS via `HasQueryFilter` + 8 cross-tenant tests
✅ File upload validation via magic bytes (Doc 73)
✅ Path traversal protection via `FileNameSanitizer`
✅ Data leak prevention: ProblemDetails não vaza stack em Prod
✅ Secrets cifrados via DataProtection com persisted keys

### Próximos passos
- Sem acção crítica imediata. Todas as 4 sub-áreas do pedido Bruno foram fechadas hoje.
- **P2** — pen test externo antes de lançamento beta público com clientes a sério.

---

## 8. Rate Limiting

### Estado actual
- ASP.NET Core RateLimiter middleware (Program.cs:348+).
- Políticas:
  - `auth-strict` — 5 login attempts/15min por IP
  - `external-apikey` — 120 req/min por chave (Sprint 80)
  - `public-portal` — 60 req/min por IP (Sprint 241 H1.4)
- **In-process partitions** — não distribuído.

### Industry standard
✅ Cobre os hot spots (auth, public, external).
⚠️ Quando houver 2+ instâncias da API, in-process partitions tornam-se inúteis (cada instância tem o seu próprio contador).

### Próximos passos
1. **P2 — Redis-backed rate limiter** quando escalar para múltiplas instâncias. Library `aspnetcore-rate-limit-redis` ou implementação custom com Redis INCR + TTL.
2. **P2 — Rate limit por tenant** (não só por IP) para LLM endpoints — uma API key comprometida pode esgotar quota.
3. **P3 — Quota mensal por feature** (Sprint 167b já fez isto para LLM).

---

## 9. Caching & CDN

### Estado actual
- **Redis** disponível mas pouco usado: `IDistributedCache` em `TenantBillingSettingsService` + `AtNifLookupService`.
- **Frontend cache HTTP:** nginx servindo estáticos com headers básicos. Cache-Control não optimizado.
- **CDN:** nenhuma. Frontend serve directo de nginx no VPS futuro.
- **Output cache:** zero. Cada request bate na DB.

### Industry standard
⚠️ Falta CDN para assets frontend — TTFB 200-500ms desnecessário para clientes longe do VPS EU.
⚠️ Falta cache em endpoints read-heavy: GET /api/dashboard, GET /api/relatorios/*.
⚠️ React Query já cacheia client-side (5min stale), mas cada utilizador autenticado abre cold cache.

### Próximos passos
1. **P1 — Cloudflare em proxy mode** para `app.mender.pt`. Caching de assets estáticos automático. Free tier. 1h setup.
2. **P2 — `[OutputCache]` em endpoints públicos read-only** (portal cliente público). 30s TTL — não é problema se ligeiramente stale.
3. **P2 — Response compression** middleware (gzip/brotli) em ASP.NET Core. 5min add.
4. **P3 — Redis cache para queries hot**: AT NIF lookup já faz; expandir para `GET /api/clientes/{id}` se profiling mostrar gargalo.

---

## 10. Load Balancing & Scaling

### Estado actual
- **Single instance VPS** planeado. Sem load balancer.
- Caddy serve como reverse proxy + TLS termination, **não como LB** (uma upstream só).

### Industry standard
N/A enquanto for 1 tenant. Standard para SaaS B2B early-stage é vertical scaling primeiro (CX33 → CX43 → dedicated server) antes de horizontal.

### Próximos passos
1. **P3 — Quando 10+ tenants:** considerar 2 instâncias API atrás de Caddy/Hetzner LB. Requer rate-limiter distribuído (área 8) + DataProtection com IXmlRepository partilhado (já há R2/Redis stores oficiais).
2. **P3 — DB read replica** quando dashboard/relatórios começarem a lockar leitura. SQL Server Standard Edition tem AlwaysOn; ou migrar para Postgres + Patroni.
3. **N/A — Kubernetes** só faz sentido a 50+ tenants ou com 24/7 deploy team.

---

## 11. Error Tracking & Logs

### Estado actual
- **Serilog estruturado** com Console + File sinks. Enrichers: CorrelationId + TenantId.
- **EF Core logs** em Warning level (Sprint 233 G1 Codex Frente 1).
- **Metrics endpoint** `/metrics` (Sprint 19 Monitoring) — basic auth + IP whitelist. Prometheus-compatible.
- **Sem error tracking** — quando crashar em prod, info fica só nos logs locais.
- **Sem APM** (Application Insights, Datadog, New Relic).

### Industry standard
⚠️ Sentry/Honeybadger é tablestakes para SaaS desde o primeiro cliente real. Ausência aqui é um gap real.
⚠️ Logs estruturados existem mas sem agregador central — quando houver 2+ instâncias, perde-se a vista global.

### Próximos passos
1. **P0 — Sentry backend (`Sentry.AspNetCore`) + frontend (`@sentry/react`)**. Free tier 5k events/mês cobre easily LopesTech. Setup 1h ambos.
2. **P1 — Log aggregation** quando produção arrancar: Better Stack Logtail (free 1GB/mês) ou Grafana Loki self-hosted. Pipe Serilog → HTTPS endpoint.
3. **P2 — Uptime monitoring**: Better Stack Uptime ou UptimeRobot a fazer ping `/api/health/live` a cada 60s. Alerta SMS/email se 2 fails consecutivos.
4. **P2 — Custom dashboards** Prometheus → Grafana Cloud free tier. Já há `/metrics` exposto.

---

## 12. Availability & Recovery

### Estado actual
- **Backups SQL** automáticos diários (Sprint 167) + sync R2.
- **Backups fotos** — replicados implícitamente via R2 storage (não há backup adicional).
- **Health checks** em vários níveis: `/api/health/live` (basic), `/ready` (DB+Redis), `/db`, `/storage`, `/backup` (alerta se backup > 24h stale).
- **No multi-AZ** — single VPS.
- **No DR plan documentado.**

### Industry standard
✅ Backups multi-tier (local + cloud).
✅ Health checks granulares.
⚠️ Restore drill nunca feito (área 2 P0).
⚠️ RPO (Recovery Point Objective) implícito: 24h (último backup). Para um SaaS pago, devia ser <1h.
⚠️ RTO (Recovery Time Objective) indefinido. Estimativa: 2-4h se Bruno disponível.

### Próximos passos
1. **P0 — Restore drill mensal** (já listado área 2).
2. **P0 — Doc 76 DR Runbook**: passos numerados para `Sistema down → restaurar do zero`. Inclui DNS swap, env vars, secrets, restore .bak.
3. **P1 — Backups incrementais SQL Server** (transaction log shipping) para reduzir RPO de 24h para <1h. SQL Server Express **não suporta** TLS shipping — outra razão para considerar Postgres.
4. **P2 — Hot standby DB** quando 5+ tenants pagantes — VPS secundária com replication.
5. **P2 — SLA público** quando começar a cobrar: 99.5% uptime (3.6h/mês downtime tolerado) é razoável para SaaS PT B2B.

---

## Prioridades — 4 semanas até beta paga

Ordenado por blast radius / esforço:

### Sprint 250 (esta semana, ~4h)
1. Sentry backend + frontend (área 11 P0) — 1h
2. Cloudflare CDN proxy para `app.mender.pt` (área 9 P1) — 30min DNS + cache rules
3. Branch protection main + Dependabot (área 6 P1) — 15min GitHub UI
4. Response compression (área 9 P2) — 5min code
5. Restore drill scripted (área 2 + 12 P0) — 2h

### Sprint 251 (próxima, ~6h)
6. Provisionar Hetzner VPS CX33 + Caddy + DNS (área 4 P0) — 1 dia
7. Deploy production primeira vez via `deploy-production.yml` — 2h
8. Doc 76 DR Runbook (área 12 P0) — 1h

### Sprint 252 (entre sprint 250+251 e beta)
9. CodeQL scan (área 6 P2) — 30min
10. Output cache portal público (área 9 P2) — 1h
11. Uptime monitoring Better Stack (área 11 P2) — 30min

### Sprint 253+ (após beta)
12. Redis-backed rate limiter (área 8 P2) — 4h
13. Log aggregation Better Stack/Loki (área 11 P1) — 2h
14. OpenAPI codegen para SDK público (área 1 P3) — 4h

---

## Resumo / decisões deliberadas

**Não implementar agora (anti-patterns para esta fase):**
- ❌ Kubernetes / cluster anything
- ❌ Multi-region
- ❌ Postgres migration (até atingir limite SQL Server Express ou 5+ tenants)
- ❌ Read replicas / hot standby
- ❌ Service mesh
- ❌ Microservices

**Implementar antes de cobrar a 1º tenant:**
- ✅ Sentry (sem isto vais a cego em prod)
- ✅ Restore drill (sem isto, backups são teatro)
- ✅ Hetzner VPS provisionado
- ✅ Branch protection main
- ✅ Cloudflare em frente

**Implementar antes de 10 tenants:**
- ✅ Uptime monitoring
- ✅ Log aggregation
- ✅ Output cache
- ✅ Redis-backed rate limiter

[[reference-docker-setup]] [[project-lopestech-roadmap]] [[feedback-codex-bugs-recorrentes]]
