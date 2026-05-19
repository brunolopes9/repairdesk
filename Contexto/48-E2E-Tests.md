# 48 - E2E Tests Playwright

## Objectivo

Garantir que os fluxos que uma loja beta usa no primeiro dia nao partem sem aviso: onboarding, reparacoes, POS, faturacao, cancelamentos, bulk emit e portal cliente.

## Stack

- Playwright em `RepairDesk/e2e`
- Browser: Chromium
- Locale/timezone: `pt-PT` / `Europe/Lisbon`
- Backend real via Docker compose
- DB real SQL Server via Docker compose
- Moloni substituido por stub interno (`E2eMoloniClient`) quando `E2E__UseMoloniStub=true`

## Flows cobertos

| Teste | Ficheiro | Valida |
|---|---|---|
| Onboarding | `01-onboarding.spec.ts` | Admin entra, preenche empresa, cria cliente e chega ao dashboard |
| Reparacao lifecycle | `02-reparacao-lifecycle.spec.ts` | Estados core, pagamento automatico ao entregar, fatura Moloni stub |
| POS | `03-vendas-pos.spec.ts` | 2 artigos no carrinho, cliente, MBWay, historico, stock decrementado |
| Cancel venda faturada | `04-cancel-venda-com-fatura.spec.ts` | Cancela venda com fatura, limpa invoice, repoe stock |
| Bulk emit | `05-bulk-emit-faturas.spec.ts` | Chip "3 pendentes fatura", modal, selecionar todas, emitir |
| Portal cliente | `06-portal-cliente.spec.ts` | Link publico mostra estado, fotos e garantia |

## Reset DB

Endpoint novo:

`POST /api/e2e/reset`

Proteccoes:

- devolve `404` se `E2E__Enabled` nao estiver activo
- se `E2E__ApiKey` estiver definido, exige header `X-E2E-Key`
- apaga dados operacionais e mantem seed tenant/admin
- volta a colocar `OnboardingCompletado=false`

Isto permite testes rapidos sem `drop database` + migrations em cada spec.

## Moloni stub

O C19 nao usa Moloni real. O `E2eMoloniClient` devolve:

- series, produtos, IVA, metodos de pagamento e clientes fake
- faturas com `ExternalId` numerico para suportar cancelamento
- `CancelDocumentAsync=true`

Assim testamos o fluxo RepairDesk sem depender da sandbox Moloni nem gastar chamadas externas.

## Correr local

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

## CI

Workflow: `.github/workflows/e2e.yml`

Corre em PR para `main` quando mexe em:

- `backend/**`
- `frontend/**`
- `e2e/**`
- `docker-compose.yml`
- o proprio workflow

Passos:

1. instala dependencias do `e2e`
2. instala Chromium Playwright
3. sobe `docker compose up -d --build db cache api web`
4. corre `npm test`
5. publica screenshots, traces, videos, HTML report e logs Docker

## Regras anti-flaky

- `workers: 1`, porque o reset da DB e global
- nada de `waitForTimeout`
- dados criados via API helper antes do UI quando o objectivo nao e testar formularios
- UI real nos pontos criticos: onboarding, POS, bulk modal e portal cliente
- Moloni stub por configuracao, nunca chamadas reais em CI

## Troubleshooting

Se o `global-setup` falhar em `/api/health/ready`:

- confirmar que SQL Server esta healthy
- ver `e2e/test-results/docker-compose.log`
- confirmar `E2E_ENABLED=true` no passo que sobe o compose

Se o reset devolver `404`:

- o container nao recebeu `E2E__Enabled=true`

Se o reset devolver `401`:

- `E2E_API_KEY` no teste nao coincide com `E2E__ApiKey` no container

Se faturacao falhar:

- confirmar `E2E_USE_MOLONI_STUB=true`
- confirmar que o teste chamou `api.configureBilling()`
