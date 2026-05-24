# 74 — Security Headers + CORS + RLS + Vazamento de Dados

**Data:** 2026-05-24
**Pedido Bruno:** Security Headers, CORS apertado para o próprio domínio, Row Level Security, prevenção de vazamento de dados.

---

## 1. Security Headers (implementado)

### Antes
Zero headers de segurança. ASP.NET Core deixava browser usar defaults (nenhuns).

### Agora — `SecurityHeadersMiddleware` (Sprint 249)

Aplicado a **todas** as responses (registado antes de UseCors para apanhar preflights):

| Header | Valor | Razão |
|---|---|---|
| `X-Content-Type-Options` | `nosniff` | Browser não adivinha MIME se Content-Type estiver lá |
| `X-Frame-Options` | `DENY` | Impede embedding em iframe (clickjacking) |
| `Referrer-Policy` | `strict-origin-when-cross-origin` | Não vaza path em cross-origin |
| `Permissions-Policy` | `camera=(), microphone=(), geolocation=(), payment=(), usb=(), magnetometer=(), gyroscope=(), accelerometer=()` | Desactiva APIs sensíveis que Mender não usa |
| `Cross-Origin-Opener-Policy` | `same-origin` | Isolamento processo browser (mitiga Spectre + cross-window) |
| `Cross-Origin-Resource-Policy` | `same-site` | Bloqueia loading cross-site |
| `Content-Security-Policy` | `default-src 'none'; frame-ancestors 'none'; base-uri 'none'; form-action 'none'; img-src 'self' data: {R2}` | API só serve JSON/PDF/imagem — nunca HTML interpretable |
| `Strict-Transport-Security` | `max-age=31536000; includeSubDomains; preload` (Production only) | Força HTTPS, preload-ready |
| `Server`, `X-Powered-By` | **removidos** | Defesa em profundidade, não revelar stack |

### Tests (Sprint 249)
4 testes em `SecurityHeadersTests.cs`:
- Anónimo (login + health) → headers presentes
- Autenticado (`/api/auth/me`) → headers presentes
- `Server` / `X-Powered-By` ausentes

---

## 2. CORS apertado (implementado)

### Antes
```csharp
.WithOrigins(corsOrigins)
.AllowAnyHeader()
.AllowAnyMethod()
.AllowCredentials()
```

`AllowAnyHeader()` e `AllowAnyMethod()` aceitam qualquer cabeçalho/método que o browser proponha. As origens já estavam whitelistadas via `Cors:AllowedOrigins` + `Frontend:BaseUrl` (default em dev: localhost:5173/3000), mas a superfície de ataque era larga.

### Agora — Sprint 249

```csharp
.WithOrigins(corsOrigins)
.WithMethods("GET", "POST", "PUT", "PATCH", "DELETE", "OPTIONS")
.WithHeaders("Authorization", "Content-Type", "Accept", "X-Correlation-Id", "X-Requested-With", "X-API-Key")
.WithExposedHeaders("X-Correlation-Id")
.AllowCredentials()
.SetPreflightMaxAge(TimeSpan.FromMinutes(10))
```

### Configuração para produção

Em `appsettings.Production.json` (ou env var) **definir**:
```json
{
  "Cors": {
    "AllowedOrigins": [ "https://app.mender.pt" ]
  },
  "Frontend": {
    "BaseUrl": "https://app.mender.pt"
  }
}
```

Sem essa config, `BuildCorsOrigins` em dev cai para `localhost:5173/3000` apenas. Em prod, `throw InvalidOperationException("CORS origins not configured...")`.

### Webhooks B2B (externals)
Webhooks de saída (`POST {tenant}/webhook`) **não passam por CORS** — são chamadas servidor→servidor. Webhooks de entrada (Stripe-style HMAC) também não — `EmailIngestController` usa `[AllowAnonymous]` + shared secret no header, sem cookie/credentials.

---

## 3. Row Level Security (RLS) — análise

### Estado actual

**Tenant-level RLS via EF Core `HasQueryFilter`** — Sprint 1.

`AppDbContext.cs` aplica filter global a **todas** as entidades que implementam `ITenantEntity`:
```csharp
modelBuilder.Entity<TEntity>().HasQueryFilter(e => e.TenantId == _tenant.TenantId)
```

Onde `_tenant` é `ITenantContext` injectado por DI (resolvido pelo middleware via JWT claim ou ApiKey claim).

### Validação

- ✅ **Sprint 242 (Codex Task H)** adicionou `CrossTenantTests.cs` com 8 testes: User de TenantA cria recurso → User de TenantB recebe 404 em GET/PUT/DELETE para todos os tipos (Cliente, Reparação, Venda, Produto, Garantia, Fornecedor, Webhook, ApiKey).
- ✅ Tests cobrem o critical path. Qualquer regressão futura no `HasQueryFilter` parte os testes.

### Limitações conhecidas (intencional)

- **`IgnoreQueryFilters()` é usado em hot paths específicos** (export RGPD, admin operations, DbInitializer, ProblemDetailsExceptionMiddleware). Cada local tem comentário explicando porquê. Auditoria manual confirma que nenhum endpoint user-facing chama `IgnoreQueryFilters`.
- **User-level ownership** (dentro do mesmo tenant) **não existe** — qualquer user do tenant vê tudo do tenant. Decisão deliberada (Doc 72): adiado até primeiro tenant com 3+ users (Sprint #343). Audit log `AuditLog` já regista `UserId` para forensics.

### Postgres RLS (DECISÃO: não implementar agora)

Bruno usa SQL Server, não Postgres. Postgres RLS seria uma camada extra abaixo do EF filter, mas:
1. Trocar de DB nesta fase é grande.
2. EF filter já cobre o critical path com testes verdes.
3. Adicionar Postgres RLS sem migrar BD não vale o esforço.

Veredicto: **RLS já está implementado de forma adequada para a fase actual** via HasQueryFilter + testes Sprint 242.

---

## 4. Vazamento de dados — análise

### Mensagens de erro (`ProblemDetailsExceptionMiddleware`)

✅ **Não vaza stack trace em Production** — verificação directa do código:
```csharp
if (ex is not null && (_env.IsDevelopment() || _env.IsEnvironment("Testing")))
{
    problem.Extensions["exception"] = ex.GetType().FullName;
    problem.Extensions["stackTrace"] = ex.StackTrace;
    ...
}
```

Em Production: response contém apenas `{ status, title, detail (genérico), type, instance }`. Logger interno regista exception completa para forensics.

### Logs (Serilog)

✅ Estruturado via `EnrichDiagnosticContext` — `CorrelationId` + `TenantId`. Não regista bodies de request, só path/status/duration.
✅ EF Core logs em Warning level em Development (Sprint G1 Codex Frente 1) — sem SELECT queries com parâmetros visíveis a olho.

### API responses

- ✅ JWT signing key, refresh tokens, API keys, passwords nunca devolvidos em nenhuma response (auditoria visual de DTOs).
- ✅ ApiKey plaintext só mostrado uma vez na criação (Sprint 71+72 — memory: `api_key_plain_only_once`).
- ✅ Anthropic key BYOK encriptada via DataProtection antes de persistir; nunca é devolvida.
- ✅ Moloni refresh token NUNCA persistido em plaintext.

### Audit log endpoints

- ✅ Listagem só Admin (`AuditController` `[Authorize(Roles="Admin")]`).
- ✅ Multi-tenant via HasQueryFilter.

### Veredicto vazamento

**Sem leak crítico detectado.** Posture defensivo está OK. Endpoints já fechados em Sprint 243-245 (Doc 72) impedem que utilizadores não-admin sequer cheguem aos endpoints que podiam expor mais dados.

---

## Checklist final (4 pedidos Bruno)

| # | Pedido | Estado |
|---|---|---|
| 1 | Security Headers | ✅ Implementado Sprint 249 — middleware + 4 testes |
| 2 | Vazamento de dados | ✅ Auditado — ProblemDetails OK em prod, logs estruturados sem PII, secrets nunca devolvidos |
| 3 | Row Level Security | ✅ Já existia via HasQueryFilter + Sprint 242 tests (8 cross-tenant) |
| 4 | CORS apertado para domínio próprio | ✅ Sprint 249 — methods + headers whitelistados, Cors:AllowedOrigins obrigatório em prod |

---

## Tasks criadas (TaskTool)

- **Sprint 249** ✅ — entregue nesta sprint.

[[reference-docker-setup]] [[feedback-codex-bugs-recorrentes]]
