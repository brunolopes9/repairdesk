# Cloudflare R2 photo storage

O upload de fotos continua a usar a interface `IPhotoStorage`. Por defeito o backend usa `LocalFileSystemPhotoStorage` em `/data/photos`. Para usar Cloudflare R2, muda apenas configuracao/env vars.

## Provider

```env
Storage__Provider=local
```

Valores aceites:

- `local`: default para dev/self-hosted simples.
- `r2`: usa `CloudflareR2PhotoStorage` com API S3-compatible.

## Environment vars R2

```env
Storage__Provider=r2
Storage__R2__AccountId=<cloudflare-account-id>
Storage__R2__AccessKey=<r2-access-key-id>
Storage__R2__Secret=<r2-secret-access-key>
Storage__R2__Bucket=repairdesk-prod-media
```

O endpoint e construido pelo backend como:

```text
https://{accountId}.r2.cloudflarestorage.com
```

Nao colocar credenciais em `appsettings.json`. Usar `.env`, variaveis do servidor, secrets do CI/CD ou gestor de secrets.

## Setup Cloudflare

1. Criar bucket privado em R2, idealmente com jurisdicao `eu`.
2. Criar API token/S3 credential limitado ao bucket e ambiente.
3. Configurar as env vars acima.
4. Reiniciar containers.
5. Fazer upload de uma foto numa reparacao.
6. Confirmar no dashboard Cloudflare que o object key aparece com prefixo `tenants/{tenantId}/...`.
7. Testar rollback com `Storage__Provider=local`.

## Docker compose

`docker-compose.yml` e `docker-compose.prod.yml` ja passam estas variaveis para a API. O volume local `photos_data` continua configurado para manter compatibilidade e rollback.

## Migracao local -> R2

Script one-shot:

```powershell
dotnet tool install -g dotnet-script
$env:LOCAL_PHOTOS_ROOT="C:\\caminho\\para\\photos"
$env:Storage__R2__AccountId="<account-id>"
$env:Storage__R2__AccessKey="<access-key>"
$env:Storage__R2__Secret="<secret>"
$env:Storage__R2__Bucket="repairdesk-prod-media"
dotnet script scripts/migrate-photos-to-r2.csx
```

Por defeito o script corre em dry-run. Para fazer upload real:

```powershell
$env:DRY_RUN="false"
dotnet script scripts/migrate-photos-to-r2.csx
```

Se as keys locais ja forem iguais as keys guardadas em SQL (`tenants/{tenantId}/...`), nao e preciso atualizar `StorageKey` na base de dados. Se no futuro houver outro formato de path, fazer migracao em batch: copiar objectos, validar contagem/hash e so depois atualizar `StorageKey`/provider.

## Custos esperados

Referencia de produto: `Contexto/14-Storage-Fotos.md`.

Estimativa usada para RepairDesk:

- 30 reparacoes/mes por loja.
- 4 fotos por reparacao.
- 1 MB por foto depois de compressao.
- Cerca de 120 MB/mes por loja.
- 24 meses de retencao: cerca de 2,88 GB por loja em steady state.

Cloudflare R2 cobra storage e operacoes, mas tem zero egress. Na estimativa do documento, 100 lojas ficam na ordem de poucos euros por mes em storage. Confirmar sempre precos atuais no dashboard Cloudflare antes de producao.

## Notas tecnicas

- O adapter usa `AWSSDK.S3`.
- `DeleteAsync` e idempotente.
- `ExistsAsync` usa `GetObjectMetadataAsync`/HEAD e trata 404 como `false`.
- O cliente S3 e lazy: se `Storage__Provider=local`, a falta de configuracao R2 nao bloqueia dev.
- Nao mudar consumidores de `IPhotoStorage`; a escolha do provider fica em DI/config.
