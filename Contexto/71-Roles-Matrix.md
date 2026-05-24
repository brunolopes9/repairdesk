# 71 - Matriz de Roles / Authz

<!-- roles-matrix-snapshot:bf0706aa164cdbda -->

Documento gerado para Sprint 239. A snapshot acima e a tabela abaixo devem ser actualizadas sempre que
um controller, rota, verbo HTTP ou atributo `[Authorize]` / `[AllowAnonymous]` mudar.

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

Para a matriz exaustiva, o teste `RolesMatrixDocTests` calcula a snapshot por reflection dos controllers.
