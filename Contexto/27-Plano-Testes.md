# Plano de testes automatizados

Atualizado: 2026-05-16  
Projeto: RepairDesk SaaS PT  
Stack atual: .NET 10 + React 19 + Vite + SQL Server + Docker  
Estado atual: backend com xUnit/WebApplicationFactory; frontend sem testes; CI atual com backend test + frontend lint/build

> Objetivo: confianca suficiente para beta com 2-3 lojas amigas, sem transformar o Bruno num departamento QA. Este plano privilegia poucos testes bem escolhidos, deterministas e baratos de manter.

## Decisao curta

Stack recomendada:

| Camada | Ferramenta | Decisao |
|---|---|---|
| Backend unit/integration | xUnit + WebApplicationFactory | Manter. Aumentar foco em multi-tenant e endpoints criticos. |
| Frontend unit/component | Vitest + React Testing Library + jsdom | Adicionar, mas so para logica/componentes de risco. |
| E2E browser | Playwright | Escolha principal. Melhor fit para React/Vite, multi-browser, tracing e CI. |
| Load | k6 | Escolha principal. Scripts JS, leve, thresholds, bom para API/SaaS pequeno. |
| DAST/security | OWASP ZAP baseline | Semanal/pre-release, nao em todos os PRs no inicio. |
| Dependency scan | Dependabot + `npm audit` + `dotnet list package --vulnerable` | Baseline gratuita. Snyk so se houver necessidade. |
| Secret scanning | GitHub secret scanning + gitleaks | Gitleaks em PR; TruffleHog opcional. |
| SAST | CodeQL | GitHub Actions para C# e JS/TS. Confirmado suporte .NET 10 via changelog CodeQL 2.24. |

Regra de ouro:

```text
PR CI < 10 min:
backend tests + frontend lint/build + vitest + smoke e2e pequeno + gitleaks.

Nightly/weekly:
full e2e + ZAP + k6 + CodeQL/dependency audit.
```

## 1. Test pyramid para RepairDesk

Distribuicao alvo para fundador solo:

| Tipo | Quantidade pre-beta | Quando correr | Objetivo |
|---|---:|---|---|
| Backend unit/integration | 70-100 | todos PRs | regras de negocio, controllers, tenant isolation. |
| Frontend unit/component | 15-30 | todos PRs | calculos, forms complexos, CSV preview, guards. |
| E2E smoke | 5-8 | todos PRs | login e 2-3 fluxos que nao podem partir. |
| E2E full | 15-25 | nightly/pre-release | jornadas reais end-to-end. |
| Load | 5 scripts | semanal/pre-release | regressao performance e capacidade beta. |
| Security | 4 scanners | gitleaks PR; ZAP/CodeQL semanal | riscos obvios antes de beta. |

Nao perseguir 100% coverage. Para beta, o objetivo bom e:

- backend line/branch coverage: 60-70% nos modulos criticos;
- frontend coverage: 40-60% nos componentes/logica testados;
- e2e: 20 fluxos que cobrem 80% do risco operacional;
- zero endpoints criticos sem teste multi-tenant.

## 2. E2E: Playwright vs Cypress

### Decisao: Playwright

Razoes:

- funciona bem com Vite/React sem acoplar ao framework;
- suporta Chromium, Firefox e WebKit;
- traces, screenshots e videos ajudam muito quando o teste falha em CI;
- bom suporte a auth setup, fixtures e paralelismo;
- CLI/codegen oficial ajuda a criar o primeiro rascunho, mas o codigo final deve ser limpo a mao.

Cypress continua bom, sobretudo para devs frontend e component testing, mas para este projeto Playwright e mais pragmatico:

- menor lock-in visual;
- melhor multi-browser;
- melhor para testar portal publico e backoffice em contextos separados;
- uma ferramenta para E2E, sem dashboard pago obrigatorio.

### Estrutura proposta

```text
RepairDesk/
  e2e/
    playwright.config.ts
    tests/
      auth.setup.ts
      smoke/
      repairs/
      public-portal/
      import-export/
      multi-tenant/
    fixtures/
      users.ts
      tenants.ts
      test-data.ts
```

### Regras anti-flakiness

- Usar `getByRole`, `getByLabel`, `getByText` estavel e `data-testid` apenas onde role/label nao chega.
- Criar dados por API ou seed deterministica antes do teste, nao depender de dados manuais.
- Cada teste cria o seu tenant/user/reparacao ou usa fixture resetada.
- Nao usar `waitForTimeout`.
- Aguardar por resposta/estado visivel.
- Falha em CI guarda trace/screenshot.
- Teste que falha 2 vezes por flake vira ticket; nao se ignora.

## 3. 20 cenarios E2E criticos

Prioridade:

- P0: smoke em todos PRs.
- P1: full E2E nightly e antes de release.
- P2: bom antes de beta publica, nao bloqueia primeiras 2-3 lojas.

| # | Prioridade | Cenario | Resultado esperado |
|---:|---|---|---|
| 1 | P0 | Login valido | Utilizador entra no dashboard da tenant correta. |
| 2 | P0 | Login invalido | Erro claro, sem revelar se email existe. |
| 3 | P0 | Criar cliente minimo | Cliente aparece na lista/pesquisa. |
| 4 | P0 | Criar reparacao com cliente existente | Reparacao fica em estado inicial correto. |
| 5 | P0 | Mudar estado de reparacao | Timeline/estado atual atualizam corretamente. |
| 6 | P0 | Logout + rota protegida | Utilizador sem sessao volta ao login. |
| 7 | P1 | Criar reparacao com novo cliente inline | Cliente e reparacao criados sem duplicar dados. |
| 8 | P1 | Editar dados do equipamento/IMEI | IMEI guardado e apresentado; validacoes basicas aplicam-se quando existirem. |
| 9 | P1 | Marcar reparacao como paga | Dashboard/estado financeiro refletem pagamento. |
| 10 | P1 | Associar despesa/peca a reparacao | Lucro/custo ficam ligados a reparacao correta. |
| 11 | P1 | Export CSV de clientes | Ficheiro descarrega com cabecalhos e dados esperados. |
| 12 | P1 | Export CSV de reparacoes | Ficheiro inclui reparacoes da tenant e exclui outras tenants. |
| 13 | P1 | Import CSV clientes: preview + confirmar | Preview mostra linhas validas/invalidas; import cria clientes. |
| 14 | P1 | Import CSV reparacoes: cliente existente | Reaproveita cliente por NIF/telefone conforme regra. |
| 15 | P1 | Portal publico por slug | Cliente sem login ve estado publico minimo. |
| 16 | P1 | Portal publico nao expoe dados internos | Sem custos internos, notas privadas, NIF/telefone, outras reparacoes. |
| 17 | P1 | QR/link publico invalido | 404/estado neutro, sem enumeracao. |
| 18 | P1 | Settings tenant: logo/NIF/IBAN/CAE | Dados guardam e aparecem em PDF/preview quando aplicavel. |
| 19 | P2 | Permissoes: tecnico vs admin | Tecnico nao acede a settings/admin/export se regra existir. |
| 20 | P2 | Multi-tenant UI: user A tenta abrir URL/id de tenant B | Acesso negado/404 e zero dados vazados. |

Smoke PR inicial: cenarios 1, 3, 4, 5, 15, 20.  
Full nightly: os 20.

## 4. Load testing: k6 vs Locust vs Gatling

### Decisao: k6

Razoes:

- open-source, leve, scripts em JavaScript;
- thresholds nativos para passar/falhar;
- bom para API HTTP;
- integra bem com GitHub Actions e Docker;
- mais simples para fundador solo do que Locust/Gatling.

Locust e bom se Bruno quiser escrever cenarios em Python com comportamento de utilizador mais sofisticado. Gatling e forte, mas pesado demais para este momento.

### Metricas alvo beta

Ambiente alvo: staging parecido com producao pequena, nao laptop do Bruno.

| Metrica | Beta 2-3 lojas | 10-30 lojas | Nota |
|---|---:|---:|---|
| API p95 endpoints normais | < 500 ms | < 350 ms | Exclui upload/download de fotos. |
| API p99 | < 1500 ms | < 1000 ms | Alertar se exceder. |
| Error rate | < 1% | < 0,5% | 4xx esperados nao contam como erro se forem parte do cenario. |
| Login p95 | < 800 ms | < 600 ms | Inclui auth/JWT. |
| Portal publico p95 | < 400 ms | < 300 ms | Pagina leve e cacheavel. |
| Dashboard p95 | < 700 ms | < 500 ms | Query agregada, sem N+1. |

### 5 cenarios k6

| # | Cenario | Carga beta | Threshold |
|---:|---|---:|---|
| 1 | Login burst | 20 VUs durante 2 min | p95 < 800ms, errors < 1% |
| 2 | Dashboard polling | 10 VUs, refresh a cada 10s por 5 min | p95 < 700ms |
| 3 | Portal publico | 50 VUs por 5 min | p95 < 400ms, errors < 0,5% |
| 4 | Criacao reparacoes em massa | 10 VUs criam cliente+reparacao por 5 min | p95 < 800ms, zero 500 |
| 5 | Pesquisa/listagem | 20 VUs pesquisam clientes/reparacoes | p95 < 500ms, sem timeouts |

Quando correr:

- local: quando Bruno mexer em queries/dashboard/imports;
- CI PR: nao correr por defeito;
- semanal: k6 em staging;
- pre-release: k6 obrigatorio;
- depois de incidente performance: k6 regressao.

## 5. Security testing automatizado

### Camadas

| Ferramenta | Frequencia | Bloqueia PR? | Nota |
|---|---|---|---|
| Dependabot | continuo | nao automaticamente | Abrir PRs de deps. Agrupar minors. |
| `npm audit --audit-level=high` | PR | sim para high/critical depois de estabilizar | Pode gerar ruido; comecar como warning. |
| `dotnet list package --vulnerable --include-transitive` | PR/weekly | warning no inicio | Bloquear critical depois. |
| gitleaks | PR | sim | Segredo em repo e blocker. |
| CodeQL C# + JS/TS | weekly + PR para main | sim para high depois | SAST gratuito no GitHub. |
| OWASP ZAP baseline | weekly/pre-release | nao no primeiro mes; depois warning/blocker por high | DAST contra staging. |

### ZAP

Comecar por ZAP baseline contra staging anonimo/publico:

- landing;
- login;
- portal publico `/r/{slug}`;
- endpoints publicos.

Depois adicionar scan autenticado com utilizador teste, mas so quando auth e seed estiverem estaveis. Scan autenticado mal feito vira flake factory e pode mexer em dados reais.

### Secrets

Usar:

- GitHub secret scanning se disponivel no repo;
- gitleaks em CI;
- `.env.example` sem valores reais;
- bloquear commits com `private.key`, `.pfx`, `.pem`, `.env`, connection strings, JWT secrets.

## 6. Frontend unit/component tests

### Decisao: Vitest + React Testing Library

Razoes:

- Vite-native;
- rapido;
- encaixa com React 19 + TypeScript;
- Testing Library testa comportamento e acessibilidade, nao implementacao.

Adicionar dev deps:

```text
vitest
jsdom
@testing-library/react
@testing-library/user-event
@testing-library/jest-dom
@vitest/coverage-v8
```

Scripts:

```json
{
  "test": "vitest run",
  "test:watch": "vitest",
  "test:coverage": "vitest run --coverage"
}
```

Componentes/logica que valem testar:

| Area | Porque |
|---|---|
| Health Score / dashboard financial calculations | Numeros errados destroem confianca. |
| CSV import preview/parser | Muitos edge cases, alto risco de dados sujos. |
| Form validation clientes/reparacoes | Evita regressao em fluxo diario. |
| Status badge/mapping | Estados errados confundem lojas/clientes. |
| Permission guards/route guards | Seguranca UX, ainda que backend mande. |
| Public portal DTO rendering | Garantir que dados internos nao aparecem. |
| Money/date formatting PT | Evita bugs feios e suporte desnecessario. |

O que nao testar:

- snapshots de UI grandes;
- classes Tailwind;
- detalhes de layout pixel-perfect;
- componentes triviais sem logica;
- mocks profundos de React Query para tudo;
- testes que reproduzem implementacao linha a linha.

## 7. Multi-tenant isolation tests

Este e o risco tecnico mais importante do produto.

### Regra

Todo endpoint que devolve, altera ou apaga dados de negocio precisa de pelo menos um teste:

```text
Tenant A cria recurso.
Tenant B autenticado tenta GET/PUT/DELETE esse recurso por id.
Resultado: 404 ou 403, nunca dados de A.
```

### Framework

Criar uma base de testes backend:

```csharp
public abstract class TenantIsolationTestBase : IClassFixture<RepairDeskApiFactory>
{
    protected Task<AuthClient> CreateTenantClientAsync(string tenantName);
    protected Task<TResource> CreateResourceAsTenantAAsync();
    protected Task AssertTenantBCannotReadOrMutateAsync(...);
}
```

Padrao recomendado:

- usar WebApplicationFactory;
- seedar TenantA/TenantB/UserA/UserB;
- criar helper `AsTenant(tenantId, role)` para JWT;
- testar endpoints por contrato HTTP;
- usar teoria/lista de endpoints quando possivel.

### Matriz minima de endpoints

| Recurso | GET list | GET by id | POST | PUT/PATCH | DELETE/soft delete | Export |
|---|---|---|---|---|---|---|
| Clientes | sim | sim | sim | sim | sim | sim |
| Reparacoes | sim | sim | sim | sim | sim | sim |
| Trabalhos | sim | sim | sim | sim | sim | sim |
| Despesas | sim | sim | sim | sim | sim | sim |
| Settings tenant | sim | N/A | N/A | sim | N/A | N/A |
| Fotos futuras | sim | sim | sim | sim | sim | sim |
| Portal publico | N/A | slug scoped | N/A | N/A | N/A | N/A |

Meta pre-beta: 100% dos endpoints de negocio cobertos por isolamento.

## 8. CI/CD proposto

### Estrategia de jobs

| Job | PR | main | nightly | Tempo alvo |
|---|---|---|---|---:|
| backend | sim | sim | sim | 2-4 min |
| frontend | sim | sim | sim | 2-4 min |
| frontend-tests | sim | sim | sim | 1-2 min |
| e2e-smoke | sim | sim | sim | 2-4 min |
| e2e-full | nao | sim opcional | sim | 5-10 min |
| security-fast | sim | sim | sim | 1-3 min |
| codeql | nao/auto | sim | weekly | varia |
| zap | nao | nao | weekly/pre-release | 5-15 min |
| k6 | nao | nao | weekly/pre-release | 5-15 min |

### Snippet GitHub Actions

Exemplo para acrescentar faseado ao CI atual, nao colar cegamente sem adaptar paths/ports/secrets:

```yaml
name: CI

on:
  push:
    branches: [main]
  pull_request:
    branches: [main]
  schedule:
    - cron: "0 3 * * 1"

concurrency:
  group: ${{ github.workflow }}-${{ github.ref }}
  cancel-in-progress: true

jobs:
  frontend-tests:
    name: Frontend tests
    runs-on: ubuntu-latest
    defaults:
      run:
        working-directory: frontend
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-node@v4
        with:
          node-version: "24"
          cache: "npm"
          cache-dependency-path: frontend/package-lock.json
      - run: npm ci
      - run: npm run test -- --coverage

  e2e-smoke:
    name: E2E smoke
    runs-on: ubuntu-latest
    timeout-minutes: 10
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-node@v4
        with:
          node-version: "24"
          cache: "npm"
          cache-dependency-path: frontend/package-lock.json
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: "10.0.x"

      - name: Start stack
        run: docker compose up -d --build

      - name: Install frontend deps
        working-directory: frontend
        run: npm ci

      - name: Install Playwright browsers
        working-directory: e2e
        run: npx playwright install --with-deps chromium

      - name: Run smoke tests
        working-directory: e2e
        env:
          BASE_URL: http://localhost:5173
          API_URL: http://localhost:5080
        run: npx playwright test --grep @smoke --project=chromium

      - name: Upload Playwright report
        if: always()
        uses: actions/upload-artifact@v4
        with:
          name: playwright-report
          path: e2e/playwright-report/

      - name: Stop stack
        if: always()
        run: docker compose down -v

  security-fast:
    name: Fast security checks
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Gitleaks
        uses: gitleaks/gitleaks-action@v2

      - name: npm audit
        working-directory: frontend
        run: npm audit --audit-level=high

      - name: dotnet vulnerable packages
        working-directory: backend
        run: dotnet list package --vulnerable --include-transitive

  zap-baseline:
    name: ZAP baseline
    if: github.event_name == 'schedule'
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - name: ZAP baseline scan
        uses: zaproxy/action-baseline@v0.14.0
        with:
          target: "https://staging.example.com"
          fail_action: false

  k6-weekly:
    name: k6 weekly load
    if: github.event_name == 'schedule'
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - name: Run k6
        uses: grafana/k6-action@v0.3.1
        with:
          filename: tests/load/portal-publico.js
```

Nota: `docker compose up` em CI deve usar dados de teste, nunca producao/staging real. Para ZAP/k6, preferir staging isolado com seed controlada.

## 9. Plano por sprint

### Sprint 1 - fundacao de testes

Tempo: 1-2 dias focados.

- Adicionar Vitest/RTL.
- Criar 5-8 testes frontend de logica critica.
- Adicionar Playwright em pasta `e2e`.
- Criar seed/test users deterministico.
- Implementar 6 smoke tests P0.
- Adicionar gitleaks.
- Garantir CI PR < 10 min.

Criterio de saida:

- PR falha se login/criar reparacao/portal publico/tenant isolation smoke partirem.

### Sprint 2 - cobertura beta

Tempo: 1-2 dias.

- Expandir para 15-20 E2E.
- Criar helpers multi-tenant backend.
- Cobrir isolamento dos endpoints principais.
- Adicionar full E2E nightly.
- Adicionar CodeQL.
- Adicionar `npm audit` e `dotnet list package --vulnerable` como warning/blocker calibrado.

Criterio de saida:

- 100% endpoints negocio principais tem teste de tenant isolation.

### Sprint 3 - load + security pre-beta

Tempo: 1 dia.

- Criar 5 scripts k6.
- Criar staging seed para load.
- Adicionar ZAP baseline semanal.
- Rodar primeiro teste de carga e guardar baseline.
- Documentar thresholds.

Criterio de saida:

- Bruno sabe se a app aguenta 2-3 lojas e tem baseline para regressao.

### Manutencao mensal

Tempo: 1-2h/mes.

- Rever falhas/flakes.
- Atualizar browsers Playwright.
- Rever Dependabot.
- Rodar k6 pre-release.
- Adicionar 1-2 testes para bugs reais encontrados.

Regra: todo bug P1/P0 corrigido deve ganhar teste automatizado.

## 10. Time budget

| Atividade | Tempo |
|---|---:|
| Setup inicial Sprint 1 | 8-12h |
| Expandir Sprint 2 | 6-10h |
| Load/security Sprint 3 | 4-8h |
| Manutencao semanal | 20-30 min |
| Manutencao mensal | 1-2h |
| Por feature nova | 10-20% do tempo da feature, maximo razoavel |

Como usar 1 hora concentrada:

1. 10 min: escolher um fluxo critico real.
2. 20 min: criar/ajustar fixture ou seed.
3. 20 min: escrever teste.
4. 10 min: correr local + limpar selectors.

Se em 1h o teste ainda esta fragil, provavelmente o fluxo precisa de melhor API seed, melhor acessibilidade/labels, ou deve ser teste de backend em vez de E2E.

## 11. Metricas de sucesso

Antes de beta:

- CI PR completo < 10 min.
- 6 smoke E2E em PR.
- 15-20 full E2E nightly.
- 20-30 frontend unit/component tests.
- 70+ backend tests, com foco em endpoints criticos.
- 100% endpoints principais com tenant isolation test.
- gitleaks ativo em PR.
- CodeQL semanal.
- ZAP baseline semanal em staging.
- 5 k6 scripts com thresholds.

Qualidade:

- zero flaky tests conhecidos sem ticket;
- falhas E2E produzem trace;
- nenhum teste depende de dados manuais;
- nenhuma credencial real em fixtures;
- todo incidente/bug serio vira teste.

## 12. Fontes consultadas

- Playwright docs: https://playwright.dev/docs/intro
- Playwright codegen: https://playwright.dev/docs/codegen
- Playwright CI docs: https://playwright.dev/docs/ci
- Cypress app/docs: https://www.cypress.io/app
- Vitest docs: https://vitest.dev/
- Testing Library React docs: https://testing-library.com/docs/react-testing-library/intro/
- Grafana k6 OSS: https://grafana.com/oss/k6/
- k6 docs: https://grafana.com/docs/k6/latest/
- k6 GitHub Action: https://github.com/grafana/k6-action
- OWASP ZAP baseline GitHub Action: https://github.com/marketplace/actions/zap-baseline-scan
- ZAProxy GitHub action: https://github.com/zaproxy/action-baseline
- GitHub CodeQL action: https://github.com/github/codeql-action
- CodeQL docs: https://codeql.github.com/docs/
- GitHub changelog CodeQL .NET 10 support: https://github.blog/changelog/2026-01-29-codeql-2-24-0-adds-swift-6-2-support-net-10-compatibility-and-file-handling-for-minified-javascript/
- Gitleaks action: https://github.com/gitleaks/gitleaks-action
- Dependabot docs: https://docs.github.com/en/code-security/dependabot
