# 71 - Matriz de Roles / Authz

<!-- roles-matrix-snapshot:b1099b45b6138799 -->

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
| ClientesController | CRUD/export base + `GET /{id}/comunicacoes` (S453) | `Authenticated`; hard-delete `Admin` |
| ClienteTagsController (S480) | `GET /api/cliente-tags`; `GET /api/cliente-tags/segmento`; `GET /api/cliente-tags/{id}/segmento`; `PUT /api/clientes/{id}/tags` | `Authenticated` |
| ClienteTagsController (S480) | `POST/PUT/DELETE /api/cliente-tags*` (segmentos CRM do tenant) | `Admin` |
| ReparacaoComunicacoesController (S452) | `GET/POST/DELETE /api/reparacoes/{id}/comunicacoes` | `Authenticated` |
| ReparacoesController | `GET /{id}/entrada.pdf` (S450), `GET /{id}/entrega.pdf` (S451) | `Authenticated` |
| ReparacoesController (S512) | fatura: `POST /{id}/emitir-fatura`, `/anular-fatura`, `/limpar-fatura-local` (desvincular fatura já anulada no Moloni para re-emitir) | `Authenticated` |
| DocumentosController (S513) | `GET /api/documentos/vendas`, `/api/documentos/vendas/export.csv` (lista única de faturas emitidas) | `Authenticated` |
| DocumentosController (S527) | `POST /api/documentos/{documentId}/recibo` (emite Recibo Moloni que liquida Fatura a crédito) | `Policy=RequireAdmin` |
| ShopConditionImagesController (S531) | `GET/PUT/DELETE /api/shop-condition-images*` (imagens por estado de condição da loja online) | `Policy=RequireAdmin` |
| DashboardController | `GET /api/dashboard/avisos-pendentes` (S460), `GET /devices-garantia-a-expirar` (S467) | `Authenticated` |
| DevicesController (S461+S464) | `GET/POST/PUT/DELETE /api/devices*` + `GET /api/devices/by-imei/{imei}` — asset registry | `Authenticated` |
| PublicPortalController / PublicWarrantyController | `GET/POST /api/public/*` | `Anonymous` + rate limit `public-portal` |
| RelatoriosController | `GET /api/relatorios/*` (inclui Sprint 187 taxa-defeito-fornecedor) | `Authenticated` |
| ServiceApiKeysController | `GET/POST /api/service-keys*` | `Admin` |
| UsersController | `POST /api/users/{id}/revoke-sessions`, `POST /api/users/{id}/deactivate` | `Admin` |
| WebhooksController | `GET/POST/PUT/DELETE /api/webhooks*` | `Admin` |
| **Sprint 243 (Doc 72 Fase A) — operações fiscais/credenciais/estruturais** | | |
| TrabalhosController | `DELETE /{id}`, billing endpoints (`emitir-fatura`, `anular-fatura`, `converter-orcamento-fatura`, `bulk-emit-faturas`, `emitir-orcamento-moloni`), `reabrir` | `Admin` |
| SupplierInvoicesController | `approve`, `reject`, `approve-stock`, `reprocess` | `Admin` |
| DespesasController | `POST`, `PUT`, `DELETE`, `POST /{id}/converter-stock` (afecta IVA dedutível) | `Admin` |
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
| **Sprint 311 (Doc 72 Fase D) — roles granulares fundação** | | |
| UsersController | `GET/PUT /{id}/roles` (gestão roles do tenant) | `Admin` |
| ReparacoesController | `PUT /{id}/assign` (atribuir técnico a reparação) | `Admin` |
| **Sprint 344 (Doc 83 Pillar 3) — assinaturas digitais** | | |
| SignaturesController | `GET/POST /api/reparacoes/{id}/signatures` | `Authenticated` |
| SignaturesController | `DELETE /{signatureId}` | `Admin` |
| **Sprint 420-422 (Doc 90) — perfil próprio, inventário, tarefas internas** | | |
| AuthController | `PUT /api/auth/me` (DisplayName + PhoneNumber próprios) | `Authenticated` |
| StockTakesController | `GET/POST /api/stock-takes*` (inventário físico mexe stock real) | `Admin` |
| InternalTasksController | `GET/POST/PUT/DELETE /api/internal-tasks*` (TODO list por staff) | `Authenticated` |
| **Sprint 435-439 (Doc 91) — catálogo serviços + inbox pedidos online** | | |
| ServiceItemsController | `GET /api/services` | `Authenticated` |
| ServiceItemsController | `POST/PUT/DELETE /api/services*` (catálogo mão-de-obra) | `Admin` |
| RepairRequestsController | `PUT /{id}/triagem` (notas internas + prioridade) | `Authenticated` |
| RepairRequestsController | `POST /{id}/converter-em-trabalho` (cria orçamento) | `Authenticated` |
| RepairRequestsController | `POST /manual` (registar lead offline) | `Authenticated` |
| **Sprint 443 (Doc 91 ponto 3) — calendar subscription** | | |
| AutomacoesController | `GET /api/automacoes/calendar-feed` (token URL) | `Authenticated` |
| AutomacoesController | `POST /api/automacoes/calendar-feed/regenerate` (rotacionar token) | `Admin` |
| PublicCalendarFeedController | `GET /api/public/calendar-feed/{token}.ics` | `AllowAnonymous` (token-auth) |

## Sprint 311 — Roles granulares (Tech / Cashier / ReadOnly)

Foram adicionadas 4 roles canónicas em `RepairDesk.Core.Auth.AppRoles`:
- `Admin` — acesso total (fiscal, RGPD, peças, fornecedores).
- `Tech` — reparações, diagnóstico, peças (não-fiscal).
- `Cashier` — vendas POS, caixa, fatura, clientes.
- `ReadOnly` — leitura apenas (dashboard, histórico).

Policies disponíveis em `RepairDesk.Core.Auth.AppPolicies`:
- `RequireAdmin` — só Admin (equivalente ao `[Authorize(Roles = "Admin")]`).
- `RequireTechOrAdmin` — Admin OU Tech.
- `RequireCashierOrAdmin` — Admin OU Cashier.
- `RequireWrite` — Admin OU Tech OU Cashier (qualquer non-readonly).

Roles são aditivas — um user pode ter `Tech + Cashier`. Os `[Authorize(Roles = "Admin")]`
existentes continuam intactos: refactor para policies fica para Fase E (controllers vão
passar a usar `[Authorize(Policy = AppPolicies.RequireXxx)]` à medida que cada feature
precisar de abrir acesso a roles não-Admin).

Para a matriz exaustiva, o teste `RolesMatrixDocTests` calcula a snapshot por reflection dos controllers.
