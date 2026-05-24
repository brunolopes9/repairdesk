# Auditoria Segurança — Autenticação & Autorização

**Data:** 2026-05-24
**Pedido:** análise sistema auth/authz, ownership, tokens, endpoints administrativos, princípio menor privilégio + plano refactor.

---

## Sumário executivo

| Área | Estado | Severidade gaps |
|---|---|---|
| Autenticação JWT | ✅ Sólido | — |
| Refresh token rotation | ✅ Sólido | — |
| Lockout + rate limit login | ✅ Sprint 233 G1.4 | — |
| Multi-tenant isolation | ✅ Sólido (HasQueryFilter global) | — |
| Role-based admin checks | ⚠️ Parcial | Média |
| Ownership/resource ACL | ❌ Inexistente (single-org per tenant) | Baixa |
| MFA / 2FA | ❌ Inexistente | Baixa (Bruno único user hoje) |
| Idle timeout refresh | ❌ Inexistente | Média |
| Audit log auth events | ⚠️ Só Login | Média |
| Endpoints públicos enumeração | ⚠️ Sem rate limit por slug | Média |

**Veredicto:** base sólida — o que falta é polish defensivo, não falhas críticas.

---

## 1. Análise de autenticação

### O que está bem

| Componente | Local | Estado |
|---|---|---|
| JWT `ValidateLifetime=true` | `ConfigureJwtBearerOptions.cs:36` | ✅ |
| `ClockSkew = 30s` (não default 5min) | `ConfigureJwtBearerOptions.cs:37` | ✅ Reduz janela de token expirado aceite |
| AccessToken 15min | `appsettings.json:42` | ✅ Curto |
| RefreshToken 7 dias | `appsettings.json:43` | ✅ Razoável |
| **Refresh rotation** | `RefreshTokenService.cs:46-51` | ✅ Cada refresh emite novo + revoga antigo |
| Logout revoga refresh + apaga cookie | `AuthController.cs:88-99` | ✅ |
| SameSite=Lax cookie | `AuthController.cs:160` | ✅ Anti-CSRF |
| Lockout 5 tentativas/15min | `Program.cs:106` | ✅ Identity-level |
| Rate limit IP-level | Sprint 233 | ✅ auth-strict 5/15min por IP |
| Password seed force change | Sprint 233 G1.1 | ✅ |
| Login audit log | `AuthController.cs:66` | ✅ `AuditAction.Login` |
| Multi-auth (JWT + ApiKey) | `Program.cs:117-131` | ✅ PolicyScheme "Multi" |

### Gaps identificados

| # | Gap | Severidade | Esforço |
|---|---|---|---|
| A1 | Sem audit log de **falha de login** (só sucesso) | Média | 30min |
| A2 | Sem audit log de **logout** | Baixa | 15min |
| A3 | Sem idle timeout (refresh válido 7 dias mesmo sem uso) | Média | 1-2h |
| A4 | Sem 2FA/MFA opcional | Baixa | 8-12h |
| A5 | Refresh cookie HttpOnly não verificado | Baixa | 5min verificar |
| A6 | Login response uniforme — pode permitir enumeration | Baixa | confirmar `invalid_credentials` é genérico |

---

## 2. Análise de autorização

### Cobertura de `[Authorize]` por Controller

```
✅ AUTHENTICATED (34/34 controllers tocados):
   AtController, AuthController, AutomacoesController, BillingOAuthController,
   ClientesController, DashboardController, DespesasController, DiagnosticoController,
   EquipmentFieldTemplatesController, ExternalController, ExternalSupplierInvoicesController,
   FornecedoresController, FotosController (misto), GarantiasController, LlmUsageController,
   PartsController, PriceTableController, ProductsController, RelatoriosController,
   ReparacoesController, ServiceApiKeysController, SupplierInvoicesController,
   TenantPreferencesController, TenantSettingsController, TrabalhosController,
   VendasController, WebhooksController, WhatsAppNotificationsController,
   AuditController, BackupsController

🌐 [AllowAnonymous] por design (4):
   PublicPortalController — /p/{slug} portal cliente público
   EmailIngestController — webhook com shared-secret no header
   E2eController — só DEV/Testing + feature flag + X-E2E-Key header
   FotosController parcial — /export-content e /portal/fotos/{id} para acesso via slug

⚠️ SEM ATRIBUTO (1):
   HealthController — herda policy global (verificar comportamento)
```

### Endpoints com `[Authorize(Roles = "Admin")]`

```
ClientesController       — 2 (export/delete RGPD)
EquipmentFieldTemplatesController — 5 (CRUD templates)
AuditController          — 1 (export)
BackupsController        — 1 (restore/list)
GarantiasController      — 1 (Anular — Sprint 198)
PartsController          — 2 (orphan-movimentos GET+POST — Sprint 214)
WebhooksController       — 1 (subscription mgmt)
```

### Gaps de autorização

| # | Gap | Severidade | Esforço |
|---|---|---|---|
| B1 | `HealthController` sem atributo explícito (`[AllowAnonymous]`) — k8s/docker probes podem 401 | **Alta** se prod | 5min |
| B2 | DELETE endpoints **não-admin** (ex: `ReparacoesController.Delete`, `VendasController.Cancelar`) — qualquer user logado consegue apagar | **Alta** | 1-2h |
| B3 | `ProductsController` admin endpoints (`migrate-shop`, `import-molano`, `csv/import-with-mapping`) sem `[Authorize(Roles="Admin")]` — qualquer user pode importar/migrar | **Alta** | 30min |
| B4 | `ServiceApiKeysController` create/delete sem Admin role | **Alta** | 30min |
| B5 | `TenantSettingsController` write sem Admin role | **Alta** | 30min |
| B6 | `WebhooksController` create/delete falta `[Authorize(Roles="Admin")]` consistente | Média | 30min |
| B7 | `FornecedoresController` write sem Admin role | Média | 30min |
| B8 | `FotosController` DELETE sem Admin role (Bruno solo OK, multi-user risco) | Baixa | 15min |

---

## 3. Ownership / resource ACL

**Modelo Mender:** multi-tenant single-org-per-tenant. Todos os utilizadores **dentro do mesmo tenant** vêem os mesmos recursos. Não há "este cliente é teu" vs "este cliente é do outro técnico".

Logo, "ownership" reduz-se a **tenant isolation**, que está protegido por:

- `AppDbContext.HasQueryFilter` global por TenantId (`AppDbContext.cs:119`)
- `_repo.FindByIdAsync(id, ct)` aplica o filter — se o user pede resource de outro tenant, retorna null → 404
- JWT contém `TenantId` claim assinado → não é forjável

✅ **Cross-tenant isolation está sólida**.

**Riscos residuais:**

| # | Risco | Mitigação actual | Recomendação |
|---|---|---|---|
| C1 | `IgnoreQueryFilters()` chamado em ~15 lugares | Audit log + intent claro | Code review checklist |
| C2 | JWT secret comprometido permite forjar TenantId | DataProtection keys persistidas em `/data/dp-keys` | Rotação periódica (3-6 meses) |
| C3 | API key cross-tenant (ServiceApiKey) | `ApiKeyAuthHandler` verifica TenantId | Confirmar test cobertura |

---

## 4. Tokens — invalidação & timeout

| Cenário | Comportamento actual | Avaliação |
|---|---|---|
| Logout explícito | Refresh token revogado + cookie apagado | ✅ |
| Access token expira | 15min, próximo refresh emite novo | ✅ |
| Refresh token expira | 7 dias absolutos | ⚠️ Sem idle timeout |
| User desactivado | Login bloqueia, mas tokens existentes válidos até expirar | ⚠️ Falta endpoint admin "revogar todos os tokens do user X" |
| Password mudada | Tokens antigos continuam válidos | ⚠️ Deveria revogar refresh tokens ao change password |
| Container reiniciado | DataProtection keys persistidas (Sprint anterior) | ✅ |

---

## 5. Endpoints administrativos

**Hoje espalhados** em vários controllers. Não há um `AdminController` central. Cada endpoint admin tem o seu `[Authorize(Roles="Admin")]`. Consequências:

- ✅ Pró: granularidade
- ❌ Contra: fácil esquecer atributo em endpoint novo
- ❌ Contra: difícil ter visão global do que é admin

**Recomendação:** convenção de naming `admin/*` no path + policy `RequireAdmin` global aplicada por route prefix.

---

## 6. Princípio do menor privilégio

**Princípio:** cada user/component deve ter o **mínimo** de acessos necessários.

**Estado actual Mender:**
- Roles: `Admin`, `User` (também `Tecnico`, `Recepcionista` reservados mas não usados)
- Default user = `Admin` (Bruno solo)
- Quando vier o 2º user de uma loja, ele será `User` por defeito? Verificar.

**Gaps:**

| # | Gap | Severidade |
|---|---|---|
| D1 | Não há matriz documentada de "que role pode fazer o quê" | Média |
| D2 | Endpoints admin não-cobertos consistentemente (ver tabela §2) | **Alta** |
| D3 | API keys têm scopes (Sprint 111) mas Bruno's UI permite criar key com **todos os scopes** sem aviso | Média |
| D4 | Sem distinção entre "Admin" (tenant owner) e "Manager" (operacional) | Baixa |

---

## 7. Endpoints públicos — enumeração

**`PublicPortalController` + `/portal/garantia/{slug}`:**

- Acesso público por slug
- Slug é gerado por `PublicSlugGenerator.New()` — UUID-like, alta entropy
- ❌ Sem rate limit por IP → atacante pode tentar muitos slugs em loop
- ❌ Slug inválido retorna 404 mesma latência → não cura enumeration
- ⚠️ Conteúdo exposto inclui PII (nome cliente, IMEI completo no PDF) — se slug é descoberto, dados expostos

**Recomendação:** rate limit policy `public-portal` 60 req/min por IP.

---

# PLANO DE REFACTOR — Tarefas para Codex

## 🔵 Codex Task H — Hardening Auth/Authz

**Branch:** `codex/sprint-237-authz-hardening`

### Fase 1 — Críticos (P0, ~2-3h)

#### H1.1 — Admin role em endpoints write críticos (B2-B7)
Verificar e adicionar `[Authorize(Roles = "Admin")]` em:
- `ProductsController`: `MigrateShop`, `ImportMolano`, `ImportCsvWithMapping`
- `ServiceApiKeysController`: `Create`, `Delete`, `Update`
- `TenantSettingsController`: `Update`, `Patch*`, `Set*`
- `WebhooksController`: `Create`, `Delete`, `Update`
- `FornecedoresController`: `Create`, `Delete`, `Update`
- `ReparacoesController.Delete` (soft-delete pode permanecer User; hard-delete só Admin)
- `VendasController.Cancelar`, `AnularFatura` (operações destrutivas)

Test: cada endpoint com user non-admin → 403.

#### H1.2 — HealthController `[AllowAnonymous]`
- File: `backend/src/RepairDesk.API/Controllers/HealthController.cs`
- Add `[AllowAnonymous]` em todos os endpoints `/api/health/*`
- Confirmar k8s/docker liveness probes funcionam.

#### H1.3 — Revogar refresh tokens ao change password
- File: `backend/src/RepairDesk.API/Controllers/AuthController.cs.ChangePassword`
- Após `ChangePasswordAsync` success, chamar `_refresh.RevokeAllForUserAsync(user.Id, ct)`.
- Implementar método no RefreshTokenService se não existe.

#### H1.4 — Rate limit no PublicPortalController
- Add policy `public-portal` em `Program.cs` (60 req/min por IP).
- Aplicar `[EnableRateLimiting("public-portal")]` em PublicPortalController + PortalGarantiaController.

### Fase 2 — Hardening (P1, ~3-4h)

#### H2.1 — Audit log de falhas de auth
- AuthController.Login: ao 401, criar AuditLog com `AuditAction.LoginFailed`, email tentado, IP.
- AuthController.Logout: criar AuditLog `AuditAction.Logout`.
- Útil para detectar brute-force.

#### H2.2 — Idle timeout para refresh tokens
- RefreshToken entity ganha `LastUsedAt` (datetime).
- Cada vez que é validado/rotated, update `LastUsedAt = now`.
- HostedService diário revoga tokens com `LastUsedAt < now - 30 days` (mesmo dentro do expiry de 7d, se não foi usado nos últimos 30 fica idle).
- Configurable per-tenant via TenantPreferences? Não — global por SaaS, política compliance.

#### H2.3 — Revogar tokens quando user desactivado
- `UserService.DeactivateAsync` (admin-only) → revoga refresh tokens + força logout próximo access token expira (max 15min).

#### H2.4 — Endpoint admin "revogar todas as sessões do user X"
- POST `/api/users/{id}/revoke-sessions`
- Admin only
- Útil para suspeita de compromised account.

### Fase 3 — Defesa em profundidade (P2, ~2-3h)

#### H3.1 — Documentar matriz de roles
- Criar `Contexto/71-Roles-Matrix.md` com tabela: para cada endpoint, que roles podem aceder.
- Tests automáticos validam que matriz está actualizada (snapshot test).

#### H3.2 — Convenção `/admin/*` paths
- Refactor rotas admin para usar prefix `/api/admin/...` consistentemente.
- Policy global "RequireAdmin" em todas as rotas com prefix.
- Evita esquecer atributo em endpoint novo.

#### H3.3 — Confirmar SameSite + Secure cookies
- Validar que refresh cookie tem `Secure=true` em Production.
- `HttpOnly=true` (deveria já estar).

#### H3.4 — Test cobertura cross-tenant
- 1 test por tipo de recurso: user de Tenant A tenta GET `/api/{recurso}/{id-de-tenant-B}` → 404 (não 403, evita enumeration).
- Adicionar como `ApiCrossTenantTests`.

---

## REGRAS DE EXECUÇÃO

1. **Fase 1 obrigatória antes de beta pago.** P0 são gaps com impacto real.
2. **Test obrigatório por mudança de [Authorize].** Garante regressão futura.
3. **Não tocar em:**
   - JWT signing key (DataProtection)
   - DatabaseName
   - Namespaces .NET
4. **Multi-tenant**: TODAS as queries com ITenantContext.
5. **Migrations**: padrão `SprintXXX_DescricaoCurta`.

---

## ORDEM SUGERIDA

**Fase 1 (P0, ~2-3h)** — quick wins críticos antes de beta
**Fase 2 (P1, ~3-4h)** — quando Fase 1 aprovada
**Fase 3 (P2, ~2-3h)** — defesa em profundidade, opcional para beta

**Estimativa total:** 7-10h Codex.

---

## NÃO incluído neste plano (decisão arquitectural)

- **MFA/2FA**: deferido — Bruno é único user. Reabrir quando primeiro tenant externo.
- **Per-user ACL** (ownership a nível de utilizador dentro do tenant): deferido — modelo Mender é single-org-per-tenant.
- **OAuth2/SSO**: deferido — não há demanda.
- **Penetration testing externo**: recomendado após Fase 1+2, antes de marketing público.
