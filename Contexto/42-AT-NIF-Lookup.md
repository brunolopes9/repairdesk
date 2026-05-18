# AT NIF Lookup - Setup e Operacao

Atualizado: 2026-05-18  
Branch: `codex/sprint-42-at-nif-lookup`

Objetivo: permitir que uma loja escreva um NIF portugues no RepairDesk e receba sugestao automatica de nome/morada via webservice da Autoridade Tributaria.

## 1. O que ficou implementado

- Endpoint autenticado: `GET /api/at/nif-lookup/{nif}`
- Validacao local primeiro com `NifValidator`.
- Cache distribuida por NIF:
  - key: `at:nif:{nif}`
  - TTL: 30 dias por defeito
- Rate limit por tenant:
  - key: `at:nif:quota:{tenantId}:{yyyyMMdd}`
  - limite: 100 chamadas AT/dia por defeito
  - cache hit nao consome quota
- Frontend no form de clientes:
  - NIF valido localmente dispara consulta AT
  - mostra `A verificar AT...`
  - se houver resultado, mostra nome/morada e botao `Aceitar nome`
  - se AT falhar, nao bloqueia o form
- Logs nunca imprimem NIF completo; so formato mascarado `*****8141`.

## 2. Configuracao

### Variaveis principais

```text
AT_PRODUCTION=false
AT_SECRETS_HOST_PATH=/opt/repairdesk/secrets/at
AT_CERT_PATH=/run/secrets/at/ChaveCifraPublicaAT2027.cer
AT_KEY_PATH=/run/secrets/at/at-private-key.pem
AT_KEY_PASSWORD=
AT_NIF_MAX_DAILY_CALLS=100
```

Em ASP.NET estas variaveis entram no container como:

```text
AtNifLookup__Production
AtNifLookup__CertPath
AtNifLookup__KeyPath
AtNifLookup__KeyPassword
AtNifLookup__MaxDailyCallsPerTenant
```

Endpoints AT configurados:

```text
Teste:     https://servicostst.portaldasfinancas.gov.pt:701/sgdtoi/dadosTOI
Producao:  https://servicos.portaldasfinancas.gov.pt/sgdtoi/dadosTOI
```

Por defeito, desenvolvimento usa teste (`AT_PRODUCTION=false`). Producao deve usar `AT_PRODUCTION=true` so depois de validar no ambiente de testes.

## 3. Docker

Criar pasta de secrets no servidor:

```bash
sudo mkdir -p /opt/repairdesk/secrets/at
sudo chmod 700 /opt/repairdesk/secrets/at
```

Colocar ficheiros:

```text
/opt/repairdesk/secrets/at/ChaveCifraPublicaAT2027.cer
/opt/repairdesk/secrets/at/at-private-key.pem
```

O `docker-compose.prod.yml` monta:

```yaml
- ${AT_SECRETS_HOST_PATH:-/opt/repairdesk/secrets/at}:/run/secrets/at:ro
```

Assim o container le:

```text
/run/secrets/at/ChaveCifraPublicaAT2027.cer
/run/secrets/at/at-private-key.pem
```

Nunca commitar certificados, chaves privadas, passwords ou ficheiros `.env.production` reais.

## 4. Redis

O RepairDesk usa `IDistributedCache`.

Em producao:

```text
Redis__Connection=cache:6379,password=...
```

Em testes automatizados usa cache em memoria. Em desenvolvimento sem Redis, tambem cai para memoria se `Redis:Connection` nao estiver configurado; em Docker dev aponta para o container `cache`.

## 5. Teste manual

1. Arrancar stack dev com Redis:

```bash
docker compose up -d db cache api web
```

2. Login na app.
3. Abrir Clientes -> Novo cliente.
4. Inserir NIF:

```text
263758141
```

Resultado esperado:

```text
A verificar AT...
Bruno Lopes (LopesTech) - Aceitar nome
```

5. Clicar `Aceitar nome`; o campo `Nome` fica preenchido.
6. Repetir o mesmo NIF: deve vir da cache e nao consumir nova chamada AT.

## 6. Comportamento de erro

| Caso | Endpoint | UI |
|---|---|---|
| NIF local invalido | `422` | mostra erro local; nao chama AT |
| NIF valido mas AT nao encontra | `404` | "NIF valido localmente, sem confirmacao na AT" |
| AT offline/timeouts/cert mal configurado | `503` | "AT indisponivel agora. Podes continuar." |
| Quota diaria excedida | `429` | "Limite diario de consultas AT atingido" |
| Cache hit | `200` | sugestao aparece rapido, sem chamada AT |

## 7. RGPD e base legal

Os dados devolvidos pela AT podem ser publicos/empresariais, mas guardar em cache no RepairDesk continua a ser tratamento de dados.

Base legal recomendada:

- Execucao de contrato/pre-contrato entre loja e cliente quando o NIF e usado para identificar cliente fiscal.
- Interesse legitimo da loja em reduzir erros de faturacao/orcamentos.

Minimizacao:

- Guardar apenas NIF, nome, morada, status e data de verificacao.
- TTL curto e proporcional: 30 dias.
- Nao guardar payload SOAP bruto.
- Nao logar NIF completo.

## 8. Notas tecnicas

O cliente SOAP esta isolado em `AtDadosToiSoapClient`. Se a AT fornecer WSDL/contract com nomes de operacao diferentes, ajustar apenas:

- `IAtDadosToiPort`
- `AtDadosToiRequest`
- `AtDadosToiResponse`
- mapping em `AtDadosToiSoapClient.Map`

O resto do produto (endpoint, cache, rate-limit e UI) nao muda.
