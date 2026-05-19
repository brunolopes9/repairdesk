# RepairDesk

[![CI](https://github.com/brunolopes9/repairdesk/actions/workflows/ci.yml/badge.svg)](https://github.com/brunolopes9/repairdesk/actions/workflows/ci.yml)
[![Deploy staging](https://github.com/brunolopes9/repairdesk/actions/workflows/deploy-staging.yml/badge.svg)](https://github.com/brunolopes9/repairdesk/actions/workflows/deploy-staging.yml)

> **Software de gestão para oficinas de reparação de electrónica em Portugal.**
> Backoffice multi-tenant + portal cliente público estilo Uber.
> Construído pela [LopesTech](https://lopestech.pt) (Bruno Lopes, Viseu).

**Estado:** em desenvolvimento activo. Funcional ponta-a-ponta mas ainda não em produção.
Beta com primeira oficina externa previsto Junho/Julho 2026 — critérios em [`Contexto/34-Beta-Launch-Criteria.md`](Contexto/34-Beta-Launch-Criteria.md).

---

## Para quem é isto

Oficina de reparação de telemóveis / electrónica em Portugal, **1-5 funcionários**, que hoje usa:

- Excel ou caderno para registar reparações
- WhatsApp para falar com cliente (5 telefonemas por dia "está pronto?")
- Software de faturação separado (Moloni, Wisedat, etc) — sem ligação ao registo da reparação
- Sem visibilidade financeira clara (margem, receita pendente, custo de stock)

O RepairDesk substitui o Excel + organiza a comunicação + dá visibilidade financeira honesta. A faturação fiscal continua a sair de provider certificado (ver [Faturação](#faturação) abaixo) — o RepairDesk integra, não substitui.

---

## O que faz hoje (já implementado)

### Operação diária
- **Reparações** com state machine completa (Recebido → Diagnóstico → Aguarda Peça → Em Reparação → Reparado → Entregue), kanban + lista, drag & drop entre estados
- **Stock de peças** com SKU, fornecedor, localização, alertas de stock baixo, decremento automático ao consumir numa reparação
- **Clientes** com NIF PT validado, histórico completo, click-to-call e click-to-WhatsApp
- **Trabalhos** (não-reparação: websites, suporte, instalações) — gestão financeira igual
- **Despesas** imputáveis a reparações/trabalhos ou avulsas (overhead, stock)
- **IMEI tracking** interno com validação Luhn + alerta histórico ("este IMEI já entrou cá X vezes")
- **Diagnóstico guiado** com templates por tipo de dispositivo, gera Health Score 0-100

### Dashboard financeiro honesto
- **Lucro Realizado** (só receita paga, não vapor) vs **Receita Pendente** vs **Investimento em Stock** — separados, não somados ingenuamente
- Δ% versus período anterior em cada KPI
- Tendência mensal últimos 6 meses (gráfico Receita vs Custo vs Lucro)
- Alertas inline: itens concluídos por cobrar, despesas órfãs, reparações paradas > 7 dias
- Top reparações lucrativas + top clientes por receita
- Quebra por categoria (Reparações / Websites / Software / etc)

### Portal cliente público (Uber-style)
- Cada reparação tem link `/r/{slug}` + QR code (no PDF orçamento)
- Cliente abre no telemóvel sem instalar nada → vê estado, timeline, fotos antes/depois, health score
- Aprovação/recusa de orçamento sem login
- Botão WhatsApp directo para a loja
- Após entrega: pedido de avaliação automático (4-5 ★ redirecciona para Google Reviews; 1-3 ★ fica interno)

### Garantia digital
- Garantia gerada automaticamente quando reparação fica `Entregue`
- Link público permanente `/g/{slug}` + QR code
- Cliente verifica validade em qualquer altura — fim das "perdi o recibo da garantia"

### Fotos antes/durante/depois
- Upload de fotos por reparação com legendas
- Tipos: **Antes** e **Depois** visíveis no portal cliente (transparência); **Durante** ficam internas
- Storage abstraído (`IPhotoStorage`): hoje filesystem local; switch para Cloudflare R2 (S3-compat, EU, zero egress) por env var

### PDFs profissionais
- Orçamento PDF com logo, NIF, IBAN, T&Cs configuráveis no tenant
- Garantia PDF com QR code para verificação pública
- Powered by [QuestPDF](https://www.questpdf.com)

### Avaliações + NPS
- Score 1-5 + comentário
- NPS calculado no dashboard
- Estratégia anti-review-bombing: só 4-5 ★ propõem Google Reviews

---

## O que torna isto diferente

Em mercado dominado por software genérico (Moloni / Vendus / InvoiceXpress) ou estrangeiro (RepairShopr, mHelpDesk), o RepairDesk diferencia-se por:

1. **Portal cliente público estilo Uber** — outros softwares só mandam SMS feio. Aqui o cliente vê timeline visual, fotos antes/depois, aprova orçamento em 2 cliques.
2. **Garantia digital com QR** — cliente nunca mais perde garantia.
3. **Dashboard financeiro honesto** — Lucro Realizado ≠ Receita Pendente ≠ Investimento Stock. Outros softwares somam tudo e fingem que está tudo bem.
4. **Diagnóstico guiado por dispositivo** — templates por marca/modelo, Health Score 0-100 para entregar ao cliente.
5. **IMEI tracking com alerta histórico** — base para futura integração com BD de IMEIs reportados (PSP/GSMA).
6. **Fotos antes/depois transparentes** — prova visual para o cliente, defesa para a loja em caso de disputa.
7. **NPS integrado com filtro inteligente** — só clientes 4-5 ★ chegam ao Google Reviews; 1-3 ★ ficam internos como sinal de melhoria.

---

## Stack técnica

| Camada | Tecnologia |
|---|---|
| Backend | .NET 10 (LTS), C#, EF Core 10, Clean Architecture multi-tenant |
| Database | SQL Server 2022 (Docker) |
| Cache | Redis 7 (Docker) |
| Frontend | React 19, Vite 8, TypeScript, Tailwind CSS v4, React Query 5 |
| PDF | QuestPDF |
| Auth | ASP.NET Core Identity + JWT Bearer + refresh tokens |
| Storage | `IPhotoStorage` (LocalFileSystem ou Cloudflare R2 S3-compat) |
| Logging | Serilog (planeado), Microsoft.Extensions.Logging (actual) |
| Testes | xUnit, FluentAssertions, WebApplicationFactory, EF InMemory |
| CI/CD | GitHub Actions (ci, deploy-staging, deploy-production via tag) |

### Multi-tenancy

Toda entidade que implementa `ITenantEntity` recebe automaticamente:
- Filtro global por `TenantId` (extraído do claim JWT `tenant_id`)
- `TenantId` injectado em `INSERT` se não estiver definido
- Filtro de soft-delete (`IsDeleted = false`)

Onboarding wizard cria um novo tenant em 5 passos (dados empresa, primeiro cliente, primeira reparação demo, tour dashboard, equipa).

---

## Estrutura

```
RepairDesk/
├── backend/
│   ├── src/
│   │   ├── RepairDesk.Core/            # Entities, interfaces, enums (zero deps)
│   │   ├── RepairDesk.Common/          # Helpers, Guards, Result types
│   │   ├── RepairDesk.DAL/             # DbContext, Configurations, Migrations
│   │   ├── RepairDesk.Services/        # Lógica de negócio, validators, DTOs
│   │   ├── RepairDesk.Infrastructure/  # Storage (Local/R2), serviços externos
│   │   └── RepairDesk.API/             # Controllers, middleware, composição DI
│   ├── tests/RepairDesk.Tests/
│   └── RepairDesk.sln
├── frontend/                           # Vite + React + Tailwind v4
│   └── src/
│       ├── components/                 # Layout, Modal, primitives (Button, PageHeader, EmptyState, Skeleton)
│       ├── pages/                      # Dashboard, Clientes, Reparacoes, Trabalhos, Stock, ...
│       ├── pages/PortalCliente.tsx     # /r/{slug} — portal público
│       └── pages/PortalGarantia.tsx    # /g/{slug} — verificação garantia
├── docs/                               # docs técnicos (R2 storage, CI/CD)
├── scripts/                            # scripts utilitários (migrate fotos para R2, etc)
├── docker-compose.yml                  # Dev: db + cache + api + web
├── docker-compose.prod.yml             # Prod: imagens do registry, sem expor DB
└── .github/workflows/                  # CI + deploy staging + deploy production
```

Regras Clean Architecture (impostas por referências de projeto):
- **Core** e **Common** não dependem de nada
- **DAL**, **Services**, **Infrastructure** dependem apenas de Core + Common
- **API** é composição: depende de tudo, faz wiring DI
- Acesso a serviços externos sempre via interfaces em Core

---

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

### Correr (Docker compose, recomendado)
```bash
docker compose up --build
```
- Frontend: http://localhost
- API: http://localhost:5080
- Swagger: http://localhost:5080/swagger
- DB: localhost:1433 (SA password no `.env`)
- Redis: localhost:6379

### Correr em 3 terminais (dev mode com hot-reload)
```bash
# Terminal 1 — DB + Redis
docker compose up db cache

# Terminal 2 — API (auto-aplica migrations + seed)
cd backend
dotnet run --project src/RepairDesk.API/RepairDesk.API.csproj

# Terminal 3 — Frontend
cd frontend
npm run dev
```

Frontend dev: http://localhost:5173 (proxy `/api` → API)

### Login inicial (tenant demo)
- Email: `bruno@lopestech.pt`
- Password: ver `.env` / `DbInitializer`

---

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

Para desactivar auto-migrate em arranque, define `Database:SkipAutoMigrate=true` em `appsettings`.

---

## Variáveis de ambiente importantes

| Var | Onde | Default | Notas |
|---|---|---|---|
| `ConnectionStrings__Default` | API | `Server=localhost,1433;...` | SQL Server |
| `Redis__Connection` | API | `localhost:6379` | Cache |
| `Jwt__Issuer` / `Jwt__Audience` | API | `repairdesk-local` | JWT |
| `JWT_SIGNING_KEY` | `.env` | — | min 32 chars, gerar com `openssl rand -base64 48` |
| `DB_SA_PASSWORD` | `.env` | — | password forte do SQL Server |
| `Storage__Provider` | API | `local` | `local` ou `r2` |
| `Storage__R2__AccountId` | API | — | Cloudflare R2 (se `Storage__Provider=r2`) |
| `Storage__R2__Bucket` | API | — | bucket R2 |

Exemplo completo em `.env.example`.

---

## Faturação

**Curta:** O RepairDesk **não emite faturas legais directamente** — integra com provider PT certificado (Moloni / InvoiceXpress) que emite em nome do tenant. O RepairDesk armazena referência + PDF.

**Porquê:** DL 28/2019 art. 4.º n.º 1 b) obriga que software que emite faturas seja certificado pela AT (auditoria, hash chain, assinatura digital, etc — processo de 6-12 meses + €5-15k). Caminho ineficiente para um produto novo a validar mercado.

Decisão completa, alternativas e prompt de implementação em [`Contexto/35-Faturacao-Decisao-Final.md`](Contexto/35-Faturacao-Decisao-Final.md). Integração planeada para sprint 39.

---

## CI/CD

O repo usa GitHub Actions:

- **`ci.yml`** — corre em pull requests e pushes para `main`
  - Backend: `dotnet restore`, `dotnet build`, `dotnet test`
  - Frontend: `npm ci`, `npm run build`, `npm run lint`
  - Security: gitleaks, audit npm/NuGet, CodeQL
- **`deploy-staging.yml`** — corre em push para `main`, publica imagens no GitHub Container Registry e actualiza staging via SSH
- **`deploy-production.yml`** — corre em tags `v*.*.*`, usa GitHub Environments para approval manual

### E2E Playwright

```bash
cd e2e
npm install
npm test
```

Antes de correr localmente, sobe o compose com `E2E_ENABLED=true`, `E2E_USE_MOLONI_STUB=true` e `E2E_API_KEY` definido. Instrucoes completas em [`e2e/README.md`](e2e/README.md).

### Cortar uma release

```bash
git tag -a v0.1.0 -m "Release v0.1.0"
git push origin v0.1.0
```

O workflow constrói as imagens `repairdesk-api` e `repairdesk-web`, espera approval no environment `production`, e faz smoke test em `/api/health`.

Staging é automático em merge para `main`. Produção é manual por tag.

---

## Roadmap

### Curto prazo (Maio-Julho 2026 — pré-beta)
- [ ] Backup automático SQL Server → Cloudflare R2 (sprint 36)
- [ ] Audit log + RGPD UI (export Art. 20, hard-delete) (sprint 37)
- [ ] Serilog + correlation IDs + health checks granulares (sprint 38)
- [ ] Integração Moloni / InvoiceXpress (sprint 39)
- [ ] Página de privacidade pública
- [ ] Vídeo demo 90s + landing actualizada

### Médio prazo (Q3 2026)
- [ ] Beta fechado com 1ª oficina amiga externa
- [ ] Iterar feedback real
- [ ] WhatsApp Business API integration (templates de notificação)
- [ ] PWA offline mode (operações balcão)
- [ ] Multi-loja por tenant

### Longo prazo (2027+)
- [ ] IMEI ↔ autoridades (GSMA CheckMEND, PSP) — feature destacada
- [ ] Marketplace de peças B2B
- [ ] App mobile nativa
- [ ] Internacionalização (ES, FR)

Roadmap detalhado e justificações em [`Contexto/04-Roadmap-Detalhado.md`](Contexto/04-Roadmap-Detalhado.md).

---

## Licença

Proprietário © LopesTech. Todos os direitos reservados.

Para perguntas comerciais: bruno.miguel.martins.lopes@gmail.com
