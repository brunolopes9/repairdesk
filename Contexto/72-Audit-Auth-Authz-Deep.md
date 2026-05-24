# 72 — Auditoria Auth/Authz Profunda

**Data:** 2026-05-24
**Pedido Bruno:** análise auth/authz cobrindo 7 áreas (autenticação, ownership, role checks, token invalidação, isolamento admin, endpoints desprotegidos, princípio menor privilégio).

**Contexto prévio:**
- Doc 70 fez auditoria inicial + plano Codex Task H.
- Sprint 238 fez Fase 1 P0 (H1.1 + H1.2).
- Sprints 241+242 (mergeados hoje) fecharam Fases 2 e 3.
- Este doc estende a análise para endpoints **não cobertos** pelo Doc 70 e formaliza o plano de refactor.

---

## Resumo executivo

| # | Pergunta Bruno | Estado |
|---|---|---|
| 1 | Rotas sensíveis exigem auth válida com token não expirado? | ✅ Coberto |
| 2 | Verificação papel Admin/User antes operações restritas? | ⚠️ Parcial — 11 controllers ainda com gaps |
| 3 | Cada operação CRUD valida ownership do recurso? | ⚠️ Tenant-level OK; user-level ownership inexistente |
| 4 | Tokens invalidados no logout/inactividade? | ✅ Coberto (Sprints 241) |
| 5 | Endpoints admin isolados/protegidos por middleware específico? | ⚠️ Atributos sim, prefix path não decidido |
| 6 | Endpoints desprotegidos ou sem verificações? | ⚠️ 30+ endpoints write sem `Roles=Admin` (lista abaixo) |
| 7 | Princípio menor privilégio? | ❌ Só 2 roles (Admin / sem role). Sem Tech/Cashier/ReadOnly |

**Severidade geral:** Média. Base é sólida; o que falta é **granularidade de roles** e fechamento dos gaps abaixo antes de onboarding multi-utilizador em SaaS.

---

## 1. Análise de autenticação (Pergunta 1)

✅ **Já validado e correcto:**
- JWT `ValidateLifetime=true`, `ClockSkew=30s`
- Refresh rotation + revoke on use + LastUsedAt idle timeout (Sprint 241 H2.2)
- Logout revoga refresh + apaga cookie (`AuthController.Logout`)
- Change password revoga todas as refresh tokens (Sprint 241 H1.3)
- Cookie HttpOnly + SameSite=Strict em Production (Sprint 241 H3.3)
- Lockout 5 tentativas/15min + rate limit `auth-strict` (Sprint 233)
- Multi-auth PolicyScheme JWT + ApiKey

**Sem gaps significativos.** Tudo neste eixo está coberto.

---

## 2. Análise de role checks (Pergunta 2)

### Estado actual

Apenas **dois níveis de papel** existem:
- `Admin` — full access
- _(sem role)_ — utilizador autenticado normal

Não existe `Tech`, `Cashier`, `ReadOnly`, `Moderator`, etc.

### Gaps por controller — endpoints write SEM `[Authorize(Roles="Admin")]` que provavelmente deveriam ter

| Controller | Endpoint | Verbo | Acção | Severidade | Recomendação |
|---|---|---|---|---|---|
| **TrabalhosController** | `/{id}` | DELETE | Apagar trabalho | 🔴 Alta | Admin (paralelo com Reparações/Vendas) |
| TrabalhosController | `/{id}/emitir-fatura` | POST | Faturação Moloni | 🔴 Alta | Admin |
| TrabalhosController | `/{id}/anular-fatura` | POST | Emite NC Moloni | 🔴 Alta | Admin |
| TrabalhosController | `/{id}/converter-orcamento-fatura` | POST | Faturação | 🔴 Alta | Admin |
| TrabalhosController | `/bulk-emit-faturas` | POST | Bulk Moloni | 🔴 Alta | Admin |
| TrabalhosController | `/{id}/emitir-orcamento-moloni` | POST | Emite documento | 🟡 Média | Admin |
| TrabalhosController | `/{id}/reabrir` | POST | Reabre concluído | 🟡 Média | Admin |
| **SupplierInvoicesController** | `/{id}/approve` | POST | Aprova fatura B2B | 🔴 Alta | Admin |
| SupplierInvoicesController | `/{id}/reject` | POST | Rejeita fatura B2B | 🔴 Alta | Admin |
| SupplierInvoicesController | `/{id}/approve-stock` | POST | Aprovação com stock | 🔴 Alta | Admin |
| SupplierInvoicesController | `/{id}/reprocess` | POST | Re-parse | 🟡 Média | Admin |
| SupplierInvoicesController | `/upload` + `/upload-photo` | POST | Upload PDF/foto | 🟡 Média | Aceitável Authenticated, mas log audit |
| **DespesasController** | `/`, `/{id}`, `/{id}` | POST/PUT/DELETE | CRUD despesas | 🔴 Alta | Admin — afecta IVA dedutível |
| **PartsController** | `/`, `/{id}`, `/{id}` | POST/PUT/DELETE | CRUD stock | 🟡 Média | Mantém Authenticated mas adicionar audit |
| PartsController | `/{id}/movimento` | POST | Ajuste manual stock | 🔴 Alta | Admin — pode esconder shrinkage |
| PartsController | `/import` | POST | Import CSV peças | 🟡 Média | Admin |
| **PriceTableController** | `/`, `/{id}`, `/{id}` | POST/PUT/DELETE | CRUD tabela preços | 🟡 Média | Admin |
| PriceTableController | `/import` | POST | Import preços | 🟡 Média | Admin |
| **TenantPreferencesController** | `/` | PUT | Editar preferências tenant | 🔴 Alta | Admin |
| TenantPreferencesController | `/reset/{group}` | POST | Reset preferências | 🔴 Alta | Admin |
| **LlmUsageController** | `/anthropic-key` | POST | Set BYOK key | 🔴 Alta | Admin |
| LlmUsageController | `/anthropic-key` | DELETE | Remove BYOK key | 🔴 Alta | Admin |
| **AutomacoesController** | `/ingest-email/regenerate` | POST | Regenera secret email forwarding | 🔴 Alta | Admin |
| **DiagnosticoController** | `/templates` | POST | Cria template | 🟡 Média | Admin |
| DiagnosticoController | `/templates/{id}` | DELETE | Apaga template | 🟡 Média | Admin |
| **ClientesController** | `/{id}` | DELETE | Soft-delete cliente | 🟡 Média | Admin |
| ClientesController | `/import` | POST | Import CSV | 🟡 Média | Admin |
| **GarantiasController** | `/{id}/anular` | POST | Já tem ✓ | — | — |
| **FotosController** | `/`, `/{fotoId}` | POST/PUT/DELETE | Upload/edit fotos | 🟢 Baixa | OK Authenticated |

**Resumo:** 30 endpoints write sem `Roles=Admin` que merecem revisão. Os mais críticos (🔴 Alta) são 13 — todos têm impacto fiscal ou estrutural.

---

## 3. Ownership de recurso (Pergunta 3)

### Estado actual

- **Tenant-level isolation:** ✅ Sólido. `HasQueryFilter` global por `TenantId`. Sprint 242 adicionou 8 testes cross-tenant que confirmam isolamento.
- **User-level ownership:** ❌ Inexistente.

### Cenário problemático

Quando RepairDesk for usado por **múltiplos funcionários do mesmo tenant** (futura realidade quando Bruno onboarda outras oficinas):
- Funcionário João cria reparação para Cliente X.
- Funcionário António pode editar/apagar essa reparação sem restrição (assumindo que ambos são admin) — ou ver tudo (se não-admin).
- Sem audit "quem fez o quê" granular.

### Recomendação

**Não introduzir ownership granular agora.** Custo/benefício não compensa enquanto Bruno é o único user:
- Auditoria já regista `UserId` em `AuditLog` (Sprint 99).
- Multi-tenant isolation cobre o caso real (oficinas concorrentes).
- Ownership user-level só faz sentido com 5+ users/tenant + necessidade de separar "minhas vs todas".

**Apenas adicionar quando houver primeiro tenant com 3+ users.**

---

## 4. Token invalidação (Pergunta 4)

✅ Tudo coberto pelo Codex Task H:
- Logout revoga refresh + apaga cookie
- ChangePassword revoga todas as sessões do user
- Idle timeout 30d via `RefreshTokenCleanupHostedService`
- User desactivado → revoga sessões (Sprint 241 H2.3)
- Admin pode revogar sessões de qualquer user via `POST /api/users/{id}/revoke-sessions`

---

## 5. Isolamento de endpoints admin (Pergunta 5)

### Estado actual

Endpoints admin estão **espalhados** pelos controllers regulares com `[Authorize(Roles="Admin")]` per-action. Não há prefix `/api/admin/*`.

**Decisão Codex (Sprint 242, Doc 71):** manter rotas existentes; não fazer refactor para `/api/admin/*` antes de beta.

### Recomendação adicional

- ✅ Continuar com atributos por action — abordagem actual é boa
- ⚠️ Adicionar **policy** `RequireAdmin` em `Program.cs` como alternativa a repetir `Roles="Admin"` em todo o lado:
  ```csharp
  options.AddPolicy("RequireAdmin", p => p.RequireRole("Admin"));
  ```
  Permite no futuro mudar a política (ex: "Admin OR Owner") num só sítio.
- ⚠️ Considerar `[Authorize(Policy="RequireAdmin")]` em vez de `[Authorize(Roles="Admin")]` quando refactorar.

---

## 6. Endpoints desprotegidos (Pergunta 6)

### Endpoints `[AllowAnonymous]` legítimos

| Controller | Endpoint | Protecção alternativa |
|---|---|---|
| AuthController | `/login`, `/refresh` | Rate limit `auth-strict` + lockout |
| PublicPortalController | `/api/public/*` | Rate limit `public-portal` 60/min (Sprint 241) |
| PortalGarantiaController | `/api/portal/garantia/*` | Idem |
| BillingOAuthController | `/callback` | State token + TTL 10min em Redis |
| EmailIngestController | `/api/email-ingest` | Shared secret no header |
| HealthController | Tudo | Sprint 238 H1.2 — public health check |

### Endpoints `[AllowAnonymous]` a investigar

| Controller | Endpoint | Preocupação |
|---|---|---|
| **E2eController** | `/api/e2e/reset` | Config-gated `E2E:Enabled` + shared key. **Confirmar que appsettings.Production NUNCA tem `E2E:Enabled=true`.** |
| **FotosController** | `/api/fotos/{id}/export-content` | Public? Verificar se é signed URL ou IDOR. |

---

## 7. Princípio menor privilégio (Pergunta 7)

### Análise

Hoje o sistema tem **all-or-nothing**:
- Admin → vê e modifica tudo
- User normal → vê e modifica quase tudo (gaps acima), mas não consegue mexer em billing/admin

Para SaaS multi-tenant onde uma oficina pode ter:
- **Dono** (Admin) — vê tudo, configura, factura
- **Técnico** — abre/fecha reparações, registo de peças, mas não factura nem mexe em definições
- **Atendimento/Caixa** — cria clientes, abre reparações, faz vendas POS, mas não vê relatórios financeiros nem cancela docs
- **Leitor** (contabilista convidado) — só consulta relatórios, exporta CSV/PDF

### Recomendação

**Fase 1 (curto prazo, antes beta):** Continuar com 2 roles. Maturar gaps em §2.

**Fase 2 (multi-utilizador):** introduzir 4 roles:
- `Admin` (existente) — owner
- `Tech` — operação técnica (reparações, peças, fotos)
- `Cashier` — atendimento + POS
- `ReadOnly` — relatórios + export

E `Authorize(Roles="Admin,Tech")` etc. nos endpoints relevantes.

---

## Plano de refactor — Fases

### Fase A (P0 — segurança imediata): fechar gaps `Roles="Admin"` críticos
- **A.1** TrabalhosController: 7 endpoints (DELETE, emitir/anular/converter/bulk-emit, reabrir, emitir-orcamento-moloni)
- **A.2** SupplierInvoicesController: approve, reject, approve-stock, reprocess
- **A.3** DespesasController: POST, PUT, DELETE
- **A.4** PartsController: `/{id}/movimento`, `/import`
- **A.5** TenantPreferencesController: PUT, POST /reset/{group}
- **A.6** LlmUsageController: anthropic-key POST/DELETE
- **A.7** AutomacoesController: ingest-email/regenerate

Estimativa: 2-3h. Pattern repete-se: adicionar 1 atributo + test cross-role.

### Fase B (P1 — defensivo): gaps médios
- **B.1** PartsController: POST/PUT/DELETE base + admin endpoints já existentes
- **B.2** PriceTableController: CRUD + import
- **B.3** DiagnosticoController: templates POST/DELETE
- **B.4** ClientesController: DELETE (soft), import
- **B.5** Verificar `FotosController /export-content` AllowAnonymous

Estimativa: 1-2h.

### Fase C (P2 — robustez infra)
- **C.1** Adicionar policy `RequireAdmin` em `Program.cs` + migrar gradualmente atributos para usar policy
- **C.2** Confirmar `E2E:Enabled=false` em todos os appsettings excepto Development
- **C.3** Tests por endpoint da Fase A (cross-role: user normal → 403)
- **C.4** Actualizar `Contexto/71-Roles-Matrix.md` + snapshot hash

Estimativa: 2-3h.

### Fase D (P3 — preparar multi-utilizador, deixar para quando houver tenant com 3+ users)
- **D.1** Introduzir roles `Tech`, `Cashier`, `ReadOnly` no Identity seeder
- **D.2** UI Definições > Utilizadores: dropdown de role
- **D.3** Refactor `Authorize(Roles="Admin")` para `Authorize(Roles="Admin,Tech")` onde apropriado
- **D.4** Migration: para tenants existentes, todos os users actuais ficam `Admin`

Estimativa: 4-6h.

---

## Tarefas criadas (TaskTool)

Cada fase tem sprint correspondente — ver TaskList.

[[feedback-codex-bugs-recorrentes]] [[reference-docker-setup]] [[project-lopestech-roadmap]]
