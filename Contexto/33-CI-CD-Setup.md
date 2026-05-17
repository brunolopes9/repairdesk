# CI/CD Setup - RepairDesk

Atualizado: 2026-05-17  
Branch de implementacao: `codex/sprint-34-cicd`  
Repo alvo: RepairDesk

---

## 1. O que foi criado

Ficheiros no repo `RepairDesk/`:

- `.github/workflows/ci.yml`
- `.github/workflows/deploy-staging.yml`
- `.github/workflows/deploy-production.yml`
- `.github/dependabot.yml`
- `CHANGELOG.md`
- `docker-compose.prod.yml`
- `.env.production.example`
- `README.md` atualizado
- `backend/src/RepairDesk.API/Dockerfile` ajustado para incluir `curl`, necessario ao healthcheck do container API.

Validacao local concluida em 2026-05-17:

- `dotnet build --configuration Release --no-restore` passa.
- `dotnet test --configuration Release --no-build --logger "trx;LogFileName=tests.trx"` passa: 54/54 testes.
- `npm run lint` passa com warnings nao bloqueantes de hooks existentes.
- `npm run build` passa.
- Corrigido o calculo/atualizacao de `CustoPecasCents`: o custo das pecas fica derivado dos movimentos de stock, nao do valor manual enviado no update da reparacao.

Fluxo:

```text
PR/main -> CI
main -> build imagens GHCR -> deploy staging -> smoke test /api/health
tag vX.Y.Z -> build imagens GHCR -> approval production -> deploy production -> smoke test /api/health
```

---

## 2. GitHub Environments

Criar em GitHub:

```text
Settings -> Environments
```

Ambientes:

1. `staging`
2. `production`

Configuracao recomendada:

- `staging`: sem approval manual.
- `production`: Required reviewers = Bruno.

Producao so avanca depois de aprovar o workflow no GitHub.

---

## 3. Secrets necessarios

### Secrets globais ou por environment

Se as packages GHCR ficarem privadas, configurar:

| Secret | Exemplo | Nota |
|---|---|---|
| `GHCR_USERNAME` | `brunolopes9` | Pode ser o user GitHub |
| `GHCR_TOKEN` | `ghp_...` | PAT com permissao `read:packages`; no build usa `GITHUB_TOKEN` |

Se as imagens forem publicas, o servidor pode nem precisar de login. Mesmo assim, usar login e mais previsivel.

### Staging secrets

| Secret | Descricao |
|---|---|
| `STAGING_SSH_HOST` | IP/dominio do VPS staging |
| `STAGING_SSH_PORT` | porta SSH, normalmente `22` |
| `STAGING_SSH_USER` | user Linux de deploy |
| `STAGING_SSH_KEY` | private key SSH sem passphrase ou compatível com Actions |
| `STAGING_DEPLOY_PATH` | ex. `/opt/repairdesk` |
| `STAGING_API_URL` | ex. `https://staging-api.repairdesk.pt` |
| `STAGING_DISCORD_WEBHOOK` | opcional; vazio = sem notificacao |

### Production secrets

| Secret | Descricao |
|---|---|
| `PROD_SSH_HOST` | IP/dominio do VPS producao |
| `PROD_SSH_PORT` | porta SSH |
| `PROD_SSH_USER` | user Linux de deploy |
| `PROD_SSH_KEY` | private key SSH |
| `PROD_DEPLOY_PATH` | ex. `/opt/repairdesk` |
| `PROD_API_URL` | ex. `https://api.repairdesk.pt` |
| `PROD_DISCORD_WEBHOOK` | opcional |

Nunca colocar `.env.production` no GitHub.

---

## 4. Preparar o servidor

No VPS:

```bash
sudo mkdir -p /opt/repairdesk
sudo chown $USER:$USER /opt/repairdesk
cd /opt/repairdesk
```

Instalar:

- Docker
- Docker Compose plugin
- Caddy ou reverse proxy externo

Copiar para o servidor:

```text
docker-compose.prod.yml
.env.production
```

O workflow copia `docker-compose.prod.yml` em cada deploy, mas o primeiro deploy precisa de `.env.production` criado manualmente.

Criar env:

```bash
cp .env.production.example .env.production
nano .env.production
```

Campos importantes:

```text
REGISTRY=ghcr.io
IMAGE_NAMESPACE=brunolopes9/repairdesk
IMAGE_TAG=v0.1.0
WEB_HTTP_PORT=127.0.0.1:8088
MSSQL_PID=Express
DB_SA_PASSWORD=...
REDIS_PASSWORD=...
JWT_SIGNING_KEY=...
```

Gerar segredos:

```bash
openssl rand -base64 48
```

Portas publicas:

- Abrir `80` e `443` para Caddy/reverse proxy.
- Abrir `22` apenas para SSH.
- Nao abrir `1433` nem `6379`.

---

## 5. Primeiro deploy staging

1. Fazer merge para `main` ou correr manualmente o workflow `Deploy staging`.
2. Confirmar que o workflow:
   - constroi `repairdesk-api`;
   - constroi `repairdesk-web`;
   - publica no GHCR;
   - copia `docker-compose.prod.yml`;
   - corre `docker compose pull`;
   - corre `docker compose up -d`;
   - faz `curl $STAGING_API_URL/api/health`.

No servidor:

```bash
cd /opt/repairdesk
docker compose --env-file .env.production -f docker-compose.prod.yml ps
docker compose --env-file .env.production -f docker-compose.prod.yml logs -f api
```

Teste manual:

```bash
curl https://staging-api.repairdesk.pt/api/health
```

Esperado:

```json
{
  "status": "ok",
  "utc": "...",
  "version": "..."
}
```

---

## 6. Primeiro deploy producao

Antes:

- `main` verde no CI.
- staging deployado e smoke test passou.
- backup testado.
- Environment `production` tem approval manual.

Criar tag:

```bash
git checkout main
git pull
git tag -a v0.1.0 -m "Release v0.1.0"
git push origin v0.1.0
```

No GitHub:

1. Abrir Actions.
2. Abrir `Deploy production`.
3. Esperar build das imagens.
4. Aprovar environment `production`.
5. Confirmar smoke test.

---

## 7. Como cortar uma release

Checklist:

1. Confirmar CI verde em `main`.
2. Atualizar `CHANGELOG.md`:
   - mover items de `Unreleased` para nova versao;
   - data ISO `YYYY-MM-DD`.
3. Escolher versao:
   - `fix` = patch, ex. `v0.1.1`;
   - `feat` = minor, ex. `v0.2.0`;
   - breaking = major, so mais tarde.
4. Criar tag anotada:

```bash
git tag -a v0.2.0 -m "Release v0.2.0"
git push origin v0.2.0
```

5. Aprovar producao.
6. Confirmar `/api/health`.
7. Publicar changelog para clientes se houver mudanca visivel.

---

## 8. Rollback rapido

Sem migration destrutiva:

1. Editar `.env.production` no servidor:

```text
IMAGE_TAG=v0.1.0
```

2. Aplicar:

```bash
cd /opt/repairdesk
docker compose --env-file .env.production -f docker-compose.prod.yml pull
docker compose --env-file .env.production -f docker-compose.prod.yml up -d
curl https://api.repairdesk.pt/api/health
```

Com migration destrutiva:

- Tratar como incidente.
- Preferir forward fix.
- Restore DB so se houver corrupcao/perda de dados ou app inutilizavel.

---

## 9. Dependabot

Configurado semanalmente para:

- NuGet em `/backend`
- npm em `/frontend`
- Dockerfiles backend/frontend
- GitHub Actions

Regra operacional:

- patch/minor podem ser agrupados;
- major deve ser revisto manualmente;
- updates de GitHub Actions e imagens Docker devem passar CI antes de merge.

---

## 10. Notas de seguranca

- Secrets ficam em GitHub Secrets/Environments.
- SSH key nunca entra no repo.
- `.env.production` vive so no servidor.
- GHCR token no servidor deve ter apenas `read:packages`.
- O smoke test so faz `/api/health`; nao imprime secrets.
- Gitleaks bloqueia segredos acidentais em PR.
- CodeQL corre C# e TypeScript.

---

## 11. Problemas provaveis

| Sintoma | Causa provavel | Correcao |
|---|---|---|
| `docker login ghcr.io` falha no servidor | `GHCR_TOKEN` sem `read:packages` | Criar PAT novo ou tornar packages publicas |
| Compose diz `IMAGE_NAMESPACE is required` | `.env.production` incompleto ou env nao passado | Confirmar `.env.production` e workflow |
| API unhealthy | DB/cache nao subiram, JWT/env missing, migration falhou | Ver `docker compose logs api db cache` |
| Smoke test falha mas containers estao up | `STAGING_API_URL`/`PROD_API_URL` errado ou reverse proxy | Testar curl local e DNS |
| Redis healthcheck falha | password errada/desalinhada | Confirmar `REDIS_PASSWORD` no `.env.production` |
| SQL healthcheck falha | password fraca/errada ou SQL ainda a iniciar | Ver logs DB e aumentar start_period se necessario |

---

## 12. Melhorias futuras

Nao bloquear MVP por isto:

- backup pre-deploy obrigatorio em staging;
- script `deploy.sh` versionado;
- release notes GitHub automaticas;
- Playwright smoke tests pos-deploy;
- status page;
- blue/green deploy quando houver 10+ lojas ativas;
- SBOM e cosign signing para imagens.
