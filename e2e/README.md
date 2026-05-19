# RepairDesk E2E

Suite Playwright para validar os fluxos core antes da beta.

## O que cobre

- Onboarding: login admin, dados da empresa, primeiro cliente, reparacao demo.
- Reparacao lifecycle: Recebido -> Diagnostico -> AguardaPeca -> EmReparacao -> Pronto -> Entregue, pago e faturado.
- POS: venda de 2 artigos com MBWay, historico e stock decrementado.
- Cancelamento de venda faturada: invoice limpa, stock reposto, status Cancelada.
- Bulk emit de faturas: chip de reparacoes pagas sem fatura -> modal -> emitir todas.
- Portal cliente publico: estado, fotos publicas e garantia digital.

## Correr localmente

> Atencao: o endpoint E2E faz reset da base de dados do Docker compose local. Usa apenas no ambiente de desenvolvimento.

PowerShell:

```powershell
$env:E2E_ENABLED = 'true'
$env:E2E_USE_MOLONI_STUB = 'true'
$env:E2E_API_KEY = 'repairdesk-e2e-local'
docker compose up -d --build db cache api web

cd e2e
npm install
npx playwright install chromium

$env:E2E_BASE_URL = 'http://localhost'
$env:E2E_API_URL = 'http://localhost:5080/api'
$env:E2E_API_KEY = 'repairdesk-e2e-local'
npm test
```

Para modo visual:

```powershell
npm run test:headed
```

## Variaveis

| Variavel | Default | Nota |
|---|---|---|
| `E2E_BASE_URL` | `http://localhost` | Frontend via nginx do Docker compose |
| `E2E_API_URL` | `http://localhost:5080/api` | API publica no host |
| `E2E_API_KEY` | vazio | Tem de bater certo com `E2E__ApiKey` no container |
| `E2E_ADMIN_EMAIL` | seed do compose | Admin seed |
| `E2E_ADMIN_PASSWORD` | `ChangeMe!2026` | Password seed |

## Como funciona

- `global-setup.ts` espera por `/api/health/live`, `/api/health/ready` e pelo frontend.
- Cada teste chama `POST /api/e2e/reset` antes de comecar.
- O reset so existe quando `E2E__Enabled=true`.
- Faturacao usa `E2eMoloniClient`, activado com `E2E__UseMoloniStub=true`, para nao depender da Moloni real.
- A suite corre com `workers: 1`, porque o reset de base de dados e global.

## Artifacts

Em CI, falhas guardam:

- screenshots/videos/traces Playwright em `e2e/test-results`
- relatorio HTML em `e2e/playwright-report`
- logs Docker em `e2e/test-results/docker-compose.log`
