# 77 — Auditoria Error Handling Frontend

**Data:** 2026-05-24
**Pedido Bruno:** auditar tratamento de erros, identificar chamadas sem try/catch, operações async silenciosas, falta de feedback, console.error sem handling, sugerir ErrorBoundary e estratégias UX.

---

## Estado actual (inventário)

| Componente | Estado | Notas |
|---|---|---|
| **ErrorBoundary React** | ❌ **inexistente** | 0 ficheiros. Render error → ecrã branco |
| **Axios interceptor central** | ✅ Existe em `lib/api.ts` | 401 → auto-refresh + dispatch `auth:unauthorized`. Outros erros: pass-through |
| **Toast system** | ✅ Sonner registado em `App.tsx` | Usado em 24/36 ficheiros com mutations |
| **Sentry browser** | ✅ Sprint 250 (env-gated) | Captura erros não-tratados mas ainda sem ErrorBoundary integration |
| **React Query default `onError`** | ❌ Não configurado | Mutations sem `onError` falham silenciosamente excepto throw para boundary |
| **Console.error** | 11 ocorrências em 9 ficheiros | Maior parte legítimos (defensive) |

---

## Achados — chamadas API sem feedback

### 🔴 Catch silenciosos críticos

| Ficheiro | Linha | Problema |
|---|---|---|
| `FotosReparacao.tsx:203,238` | `.catch(() => {})` total swallow | Fail de fetch de foto → user não percebe que algo falhou |
| `Automacoes.tsx:32` | `.catch(() => { /* só admins */ })` | User não-admin não percebe porque não vê config |

### 🟡 Catch silenciosos defensáveis (intencional)

| Ficheiro | Linha | Razão |
|---|---|---|
| `main.tsx:19` | SW register fail | Service Worker é progressive enhancement |
| `HealthIndicator.tsx:36` | JSON parse fail | Health check; sem JSON = treat como down |

### 🟢 Catch com handling correcto

| Ficheiro | Padrão |
|---|---|
| `vendas/api.ts:36` | 404 → null, outros throw |
| `garantias/api.ts:31,40` | Idem |
| `diagnostico/api.ts:86` | Idem |
| `ClienteForm.tsx:64` | `AbortController` + `isAxiosError` |
| `ReparacaoDetalhe.tsx:295` | `setError(detail)` mostra ao user |

### Ficheiros com `useMutation`/`useQuery` SEM `onError` visível

11 ficheiros. Para `useQuery` sem `onError` é geralmente aceitável (React Query expõe `error` que a UI pode renderizar via `if (isError)`). Para `useMutation` é gap.

| Ficheiro | Mutations sem onError | Acção |
|---|---|---|
| `Despesas.tsx` | 3 | Adicionar `onError: showApiError` |
| `Auditoria.tsx` | 2 | Idem |
| `Dashboard.tsx` | 2 | Idem |
| `Iva.tsx`, `Negocio.tsx` | 1+2 | Só queries — aceitar fallback UI |
| `WhatsAppMenu.tsx`, `Layout.tsx`, `CommandPalette.tsx` | 1+1+2 | Pequenos — revisar caso a caso |

**Total ~5-6 mutations sem feedback** — não-crítico mas inconsistente.

---

## Estratégia proposta

### 1. ErrorBoundary global (P0)

`<ErrorBoundary>` envolve `<App>` em `main.tsx`. Apanha erros de render em qualquer ramo, mostra fallback (com botão "Recarregar"), reporta a Sentry.

### 2. ErrorBoundary por rota (P1)

Cada rota lazy-loaded fica num sub-boundary. Se Dashboard partir, Stock continua a funcionar — não vai tudo abaixo.

### 3. Helper `apiErrorMessage(err)` (P0)

Centraliza extracção de mensagem amigável de erros Axios:
- ProblemDetails (`{ detail, code, errors }`) → `detail`
- Erro de rede → "Sem ligação à internet"
- 401 → "Sessão expirou"
- 403 → "Sem permissão"
- 404 → "Não encontrado"
- 429 → "Demasiados pedidos, tenta dentro de 1 minuto"
- 500+ → "Erro do servidor, foi notificado"
- Timeout → "Servidor demorou demasiado"

### 4. React Query `defaultOptions.mutations.onError` (P0)

Toast genérico como **fallback** para mutations sem `onError` específico. Mutations com `onError` próprio continuam a sobrescrever (UI específica).

### 5. Sentry integration nos boundaries (P0)

Quando ErrorBoundary apanha um error, chama `Sentry.captureException(error)` com extra context (URL, componentStack). Sem DSN = no-op (Sprint 250).

### 6. Fixes pontuais nos catch silenciosos (P1)

`FotosReparacao.tsx` linhas 203+238 — substituir por logging Sentry. Estes silenciosos foram colocados durante refactor de signed URLs (Sprint 64+165) e ninguém voltou.

---

## Implementação Sprint 253

1. `components/ErrorBoundary.tsx` — class component que captura + reporta Sentry + fallback UI
2. `lib/errors.ts` — `apiErrorMessage()` + `installGlobalToastOnError()`
3. `main.tsx` — wrap `<App>` no ErrorBoundary global, configurar React Query default onError
4. `App.tsx` — sub-boundaries por rota dentro do `<Suspense>`
5. Fixes pontuais em FotosReparacao + Automacoes
6. Tests unitários `errors.test.ts`

Estimativa: ~2h.

---

[[reference-docker-setup]]
