# RepairDesk

SaaS de gestão de reparações, clientes, peças, fornecedores e (futuramente) faturação certificada para Portugal. Construído pela **LopesTech** (Bruno Lopes).

> **Estado:** Sprint 0 — fundação. Backend .NET 10 + frontend React 19 + SQL Server em Docker. Multi-tenant ready (single-tenant na Fase 1).

---

## Stack

| Camada | Tecnologia |
|---|---|
| Backend | .NET 10 (LTS), C#, EF Core 10, Clean Architecture |
| Database | SQL Server 2022 (Docker) |
| Cache | Redis 7 (Docker) |
| Frontend | React 19, Vite 8, TypeScript, Tailwind CSS v4 |
| Logging | Serilog (Console + File rotativo) |
| Testes | xUnit, FluentAssertions, Moq, EF InMemory |
| CI | GitHub Actions |

## Estrutura

```
RepairDesk/
├── backend/
│   ├── src/
│   │   ├── RepairDesk.Core/            # Entities, interfaces, enums (zero deps)
│   │   ├── RepairDesk.Common/          # Helpers, Guards, Result types
│   │   ├── RepairDesk.DAL/             # DbContext, Configurations, Migrations
│   │   ├── RepairDesk.Services/        # Lógica de negócio, validators
│   │   ├── RepairDesk.Infrastructure/  # Serviços externos (SMS, email, AT WS)
│   │   └── RepairDesk.API/             # Controllers, middleware, composição DI
│   ├── tests/RepairDesk.Tests/
│   ├── Directory.Build.props           # Propriedades comuns (TFM, nullable, audit)
│   ├── global.json                     # Pin do SDK
│   └── RepairDesk.sln
├── frontend/                           # Vite + React + Tailwind v4
├── docker-compose.yml                  # API + SQL Server + Redis
└── .github/workflows/ci.yml
```

Regras Clean Architecture (impostas por referências de projeto):
- **Core** e **Common** não dependem de nada.
- **DAL**, **Services**, **Infrastructure** dependem apenas de Core + Common.
- **API** é composição: depende de tudo, faz wiring DI.
- Acesso a serviços externos sempre via interfaces em Core.

## Correr localmente

### Pré-requisitos
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Node.js 24+](https://nodejs.org)
- [Docker Desktop](https://www.docker.com/products/docker-desktop)

### Setup primeira vez
```bash
cp .env.example .env       # edita as passwords se quiseres
docker compose up -d db cache
cd backend && dotnet restore
cd ../frontend && npm install
```

### Correr (3 terminais ou Docker compose)
```bash
# Terminal 1 — DB + Redis
docker compose up db cache

# Terminal 2 — API (auto-aplica migrations + seed da LopesTech)
cd backend
dotnet run --project src/RepairDesk.API/RepairDesk.API.csproj

# Terminal 3 — Frontend
cd frontend
npm run dev
```

- API: http://localhost:5080
- Swagger: http://localhost:5080/swagger
- Frontend: http://localhost:5173 (proxy `/api` → API)

### Tudo em containers
```bash
docker compose up --build
```

## Migrations

```bash
cd backend

# Adicionar nova migration
dotnet ef migrations add <Nome> \
  --project src/RepairDesk.DAL \
  --startup-project src/RepairDesk.API \
  --output-dir Migrations

# Aplicar manualmente (não é preciso, a API aplica no arranque)
dotnet ef database update \
  --project src/RepairDesk.DAL \
  --startup-project src/RepairDesk.API
```

Para desativar auto-migrate em arranque, define `Database:SkipAutoMigrate=true` em `appsettings`.

## Multi-tenancy

Toda entidade que implemente `ITenantEntity` recebe automaticamente:
- Filtro global por `TenantId` (extraído do claim JWT `tenant_id`).
- `TenantId` é injetado em `INSERT` se não estiver definido.
- Filtro de soft-delete (`IsDeleted = false`).

A Fase 1 corre com **um único tenant** (LopesTech, seed em `DbInitializer`). A passagem para multi-tenant real é só ativar o módulo de onboarding — zero retrabalho de schema.

## Variáveis de ambiente importantes

| Var | Onde | Default | Notas |
|---|---|---|---|
| `ConnectionStrings__Default` | API | `Server=localhost,1433;...` | SQL Server |
| `Redis__Connection` | API | `localhost:6379` | Cache |
| `Jwt__Issuer` / `Jwt__Audience` | API | `repairdesk-local` | Sprint 1 |
| `JWT_SIGNING_KEY` | `.env` | — | min 32 chars, gerar com `openssl rand -base64 48` |
| `DB_SA_PASSWORD` | `.env` | — | password forte do SQL Server |

## Roadmap (alto nível)

- [x] **Sprint 0** — Setup, arquitetura, Docker, multi-tenant, primeira migration
- [ ] **Sprint 1** — Auth (Identity + JWT + refresh)
- [ ] **Sprint 2** — Clientes (NIF PT, click-to-WhatsApp)
- [ ] **Sprint 3** — Reparações (state machine, fotos, time tracking, recibo de entrada)
- [ ] **Sprint 4** — Orçamentos + Peças/Fornecedores
- [ ] **Sprint 5** — Dashboard + PWA mobile-first
- [ ] **Sprint 6+** — Comunicação WebService AT, faturação certificada, IA, marketplace

Detalhe completo no `Contexto/RepairDesk_DOCUMENTO_DEFINITIVO.docx`.

## Faturação e AT

A Fase 1 **não emite** faturas pelo software — apenas regista referências às que são emitidas no Portal das Finanças. A integração com WebService AT (`Fatcorews.wsdl`) chega no Sprint 7. A certificação AT do software de emissão (Despacho 8632/2014) só faz sentido depois de validar o produto com clientes a pagar.

## Licença

Proprietário © LopesTech. Todos os direitos reservados.
