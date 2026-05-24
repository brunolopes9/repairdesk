# 71 - Matriz de Roles / Authz

<!-- roles-matrix-snapshot:7371f016ebf98e3f -->

Documento gerado para Sprint 239 e estendido em Sprint 243 (Doc 72 Fase A). A snapshot acima e a
tabela abaixo devem ser actualizadas sempre que um controller, rota, verbo HTTP ou atributo
`[Authorize]` / `[AllowAnonymous]` mudar.

## Decisão `/api/admin/*`

Não vamos mover endpoints admin-only para `/api/admin/*` nesta sprint. A decisão é manter as rotas
existentes para não quebrar frontend/API antes da beta, e reforçar a protecção com `[Authorize(Roles = "Admin")]`,
testes e esta matriz com snapshot.

## Matriz

| Controller | Endpoint | Acesso |
|---|---|---|
| AuditController | `GET /api/audit*` | `Admin` |
| BackupsController | `GET/POST /api/backups*` | `Admin` |
| AuthController | `POST /api/auth/login`, `POST /api/auth/refresh` | `Anonymous` |
| AuthController | `POST /api/auth/logout`, `POST /api/auth/change-password`, `GET /api/auth/me` | `Authenticated` |
| ClientesController | CRUD/export base | `Authenticated`; hard-delete `Admin` |
| PublicPortalController / PublicWarrantyController | `GET/POST /api/public/*` | `Anonymous` + rate limit `public-portal` |
| RelatoriosController | `GET /api/relatorios/*` (inclui Sprint 187 taxa-defeito-fornecedor) | `Authenticated` |
| ServiceApiKeysController | `GET/POST /api/service-keys*` | `Admin` |
| UsersController | `POST /api/users/{id}/revoke-sessions`, `POST /api/users/{id}/deactivate` | `Admin` |
| WebhooksController | `GET/POST/PUT/DELETE /api/webhooks*` | `Admin` |
| **Sprint 243 (Doc 72 Fase A) — operações fiscais/credenciais/estruturais** | | |
| TrabalhosController | `DELETE /{id}`, billing endpoints (`emitir-fatura`, `anular-fatura`, `converter-orcamento-fatura`, `bulk-emit-faturas`, `emitir-orcamento-moloni`), `reabrir` | `Admin` |
| SupplierInvoicesController | `approve`, `reject`, `approve-stock`, `reprocess` | `Admin` |
| DespesasController | `POST`, `PUT`, `DELETE` (afecta IVA dedutível) | `Admin` |
| PartsController | `POST /{id}/movimento` (ajuste manual stock), `POST /import` | `Admin` |
| TenantPreferencesController | `PUT /`, `POST /reset/{group}` | `Admin` |
| LlmUsageController | `POST/DELETE /anthropic-key` (BYOK credencial) | `Admin` |
| AutomacoesController | `POST /ingest-email/regenerate` | `Admin` |
| **Sprint 244 (Doc 72 Fase B) — configuração comercial/estrutural** | | |
| PriceTableController | `POST /`, `PUT/DELETE /{id}`, `POST /import` | `Admin` |
| DiagnosticoController | `POST/DELETE /templates` (execuções por reparação ficam Authenticated) | `Admin` |
| ClientesController | `DELETE /{id}` (soft-delete), `POST /import` | `Admin` |
| **Sprint 300 (Doc 80 Pillar A.1) — POS PT controlo de caixa** | | |
| CashController | `GET /today`, `/by-date/{date}`, `/recent`, `POST /open`, `POST /movement`, `GET /{id}/zreport.pdf` | `Authenticated` |
| CashController | `POST /{id}/close` (fecho caixa impacta relatórios fiscais) | `Admin` |

Para a matriz exaustiva, o teste `RolesMatrixDocTests` calcula a snapshot por reflection dos controllers.
