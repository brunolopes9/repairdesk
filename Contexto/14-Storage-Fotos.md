# Storage de Fotos Antes/Depois

Última atualização: 2026-05-16

Objetivo: decidir onde guardar fotos de reparações no RepairDesk sem meter binários grandes no SQL Server, mantendo custos previsíveis, isolamento por tenant e conformidade EU/RGPD.

Decisão curta: **usar Cloudflare R2 com bucket em jurisdição `eu`**. É a opção mais aborrecida que funciona bem para o MVP: S3-compatible, sem egress, barata, EU-only configurável e simples de trocar mais tarde. **Fallback:** Backblaze B2 em região EU Central se o Bruno quiser o custo mínimo absoluto e aceitar um produto menos integrado com edge/CDN.

---

## Premissas usadas nos custos

Preços confirmados em 2026-05-16 nas páginas/APIs oficiais indicadas no fim do documento. Quando o preço original está em USD, foi convertido com câmbio operacional de **1 USD = 0,8602 EUR** em 2026-05-16. Confirmar câmbio no dia da contratação.

Hipótese de tráfego típico RepairDesk:

| Cenário | Storage | Egress típico/mês | Operações típicas/mês |
|---|---:|---:|---:|
| Pequeno agregado | 100 GB | 20 GB | 10k uploads/PUT + 100k leituras/GET |
| Escala inicial | 1 TB, tratado como 1024 GB | 205 GB | 100k uploads/PUT + 1M leituras/GET |

Esta hipótese é conservadora para fotos privadas: a maioria das imagens é vista pelo técnico, pelo cliente no portal e talvez uma segunda vez em garantia. Se começarmos a servir galerias públicas, vídeos ou fotos em qualidade original, o egress muda de categoria.

---

## Tabela comparativa

Custos mensais aproximados em EUR, já com egress típico. Free tiers simples foram considerados quando são globais e previsíveis: R2 10 GB storage + operações incluídas, Backblaze 10 GB storage, AWS/Azure 100 GB de data transfer out grátis. IVA não incluído.

| Opção | Storage | Egress | PUT/GET | S3-compatible | EU-only | Durabilidade/SLA | Latência PT | Custo 100 GB | Custo 1 TB | Leitura |
|---|---:|---:|---:|---|---|---|---|---:|---:|---|
| Cloudflare R2 | $0,015/GB-mês | $0 | PUT/Class A $0,0045/1000; GET/Class B $0,00036/1000; free tier cobre MVP | Sim, via `Amazon.S3`/S3 API | Sim, criar bucket com jurisdição `eu` | 11 noves; SLA disponibilidade 99,9% | Boa; EU + rede Cloudflare | ~€1,16 | ~€13,08 | Melhor default. Custo previsível e migração fácil. |
| AWS S3 Standard | $0,023/GB-mês em EU Ireland, primeiros 50 TB | ~$0,09/GB após 100 GB/mês | PUT $0,005/1000; GET $0,0004/1000 | Sim, nativo | Sim, `eu-west-1`/`eu-central-1` | 11 noves; disponibilidade 99,99% para S3 Standard | Muito boa em Ireland/Frankfurt | ~€2,06 | ~€29,16 | Excelente tecnicamente, mas egress encarece quando o portal cresce. |
| Backblaze B2 | $6,95/TB-mês (~$0,0068/GB) | Grátis até 3x storage médio; depois $0,01/GB | API calls grátis para uso normal desde 2026-05-01 | Sim; `Amazon.S3` com endpoint B2 | Sim, escolher EU Central/Amsterdam na criação da conta | 11 noves; SLA 99,9% | Boa a média; Amsterdam | ~€0,53 | ~€5,92 | Mais barato. Bom fallback, mas menos polido para produto SaaS multi-tenant. |
| Azure Blob Hot LRS | €0,0167/GB-mês em West Europe | €0,0684/GB após 100 GB/mês | Write €0,00461/1000; Read €0,00037/1000 | Não S3 nativo; SDK Azure excelente | Sim, West/North Europe | LRS 11 noves; ZRS 12 noves | Muito boa se backend estiver em Azure | ~€1,75 | ~€25,11 | Faz sentido se o RepairDesk for todo para Azure. Caso contrário aumenta lock-in. |
| Bunny Storage + CDN | $0,01/GB-mês storage single-region; CDN EU/NA $0,01/GB | Via CDN, $0,01/GB EU/NA; storage-to-CDN grátis | Sem API fees | Não assumir S3; docs atuais indicam HTTP API/FTP/SFTP | Só se restringirmos storage/CDN a regiões EU | 11 noves com múltiplas regiões; SLA 99,99% anunciado | Muito boa, CDN forte | ~€1,03 | ~€10,57 | Ótimo para assets públicos/CDN. Menos bom para fotos privadas e portabilidade S3. |
| MinIO self-hosted | Software sem custo de licença, mas disco+servidor | Depende do host; Hetzner EU Cloud inclui 20 TB tráfego | Sem custo por operação, mas há custo operacional | Sim | Sim, se VPS/dados ficam na UE | Depende da arquitetura; single node não conta | Boa se host EU | ~€9+ single-node | ~€49+ single-node | Não recomendado para MVP. Operar storage é produto à parte. |

Nota MinIO: a estimativa usa Hetzner Cloud Volume a €0,044/GB-mês + uma VM pequena. Isto **não** dá a mesma durabilidade que R2/S3. Para produção minimamente séria, duplicar nós/discos/backups, e o custo/tempo deixam de compensar nesta fase.

---

## Custo por número de lojas

Volume dado no prompt:

- 30 reparações/mês por loja
- 4 fotos por reparação
- 1 MB por foto depois de compressão
- 120 MB/mês por loja
- Retenção 24 meses => ~2,88 GB por loja em steady state

Estimativa em R2, sem contar IVA e assumindo que as operações ficam no free tier:

| Lojas | Storage aos 24 meses | Custo R2/mês | Se fotos médias forem 4 MB |
|---:|---:|---:|---:|
| 10 | ~29 GB | ~€0,25 | ~€1,40 |
| 100 | ~288 GB | ~€3,59 | ~€14,50 |
| 1000 | ~2,8 TB | ~€37 | ~€149 |

Conclusão importante: **storage de fotos não é o risco financeiro principal**. O risco é guardar originais demasiado grandes, ativar vídeos cedo ou servir tudo por URLs públicas/CDN sem controlo de egress.

---

## Recomendação principal

Escolha: **Cloudflare R2 Standard, bucket privado em jurisdição `eu`**.

Porquê:

1. **Egress zero**: o portal cliente pode mostrar fotos sem medo de conta surpresa.
2. **S3-compatible**: usamos `AWSSDK.S3`/`Amazon.S3` em .NET e evitamos prender o domínio ao provider.
3. **EU jurisdiction**: a jurisdição `eu` garante armazenamento/processamento dentro da UE; melhor que "location hint".
4. **Custo simples**: 1 TB custa ~€13/mês no nosso cenário, não ~€25-30 como Azure/AWS com egress.
5. **Boa história de migração**: R2, S3, B2 e MinIO falam uma base comum de S3 API.

Fallback: **Backblaze B2 EU Central**.

Quando escolher B2:

- Se o objetivo for custo mínimo por TB.
- Se o Bruno preferir uma conta de storage simples, sem ecossistema Cloudflare.
- Se a integração R2 tiver algum bloqueio inesperado com signed URLs, CORS ou suporte.

Quando escolher Azure Blob:

- Se o backend, SQL Server, backups e observabilidade forem todos para Azure.
- Se quisermos Managed Identity, Defender for Storage e integração Microsoft mais do que portabilidade S3.

Quando não escolher:

- **AWS S3**: tecnicamente excelente, mas egress caro para um SaaS que vai mostrar fotos a clientes.
- **Bunny Storage**: bom para imagens públicas e CDN, mas menos adequado para fotos privadas de reparações e portabilidade S3.
- **MinIO self-hosted**: só quando houver equipa/rotina de infra. Neste momento seria over-engineering.

---

## Arquitetura recomendada

### Fluxo de upload

Usar **upload direto do browser para o object storage via signed URL**, mas sempre autorizado pela API .NET.

Fluxo:

1. Frontend pede à API: `POST /repairs/{repairId}/photos/upload-intent`.
2. API valida tenant, permissões, estado da reparação, quota, número máximo de fotos e metadata do ficheiro.
3. API cria registo `RepairPhoto` em estado `PendingUpload`.
4. API gera signed PUT URL com TTL de 5-10 minutos.
5. Browser faz upload direto para R2/B2/S3.
6. Frontend chama `POST /repair-photos/{photoId}/complete`.
7. API faz `HEAD` ao objeto, valida tamanho/tipo e marca como `Uploaded`.
8. Job em background gera thumbnails/versões web e marca `Processed`.

Evitar upload via API .NET para ficheiros normais porque gasta CPU/bandwidth do backend e complica timeouts no mobile. Manter endpoint via API apenas como fallback para fotos pequenas ou ambientes sem CORS.

### Modelo SQL

Guardar metadata no SQL Server, nunca o binário:

| Campo | Nota |
|---|---|
| `Id` | GUID. |
| `TenantId` | Obrigatório em todos os índices/queries. |
| `RepairId` | FK para reparação. |
| `Kind` | `Antes`, `Depois`, `Diagnostico`, `Extra`. |
| `StorageProvider` | `R2`, `B2`, `S3`, `AzureBlob`, `MinIO`. |
| `Bucket` | Nome lógico, não exposto ao cliente. |
| `ObjectKeyOriginal` | Key privada do original. |
| `ObjectKeyWeb` | Versão comprimida para portal. |
| `ObjectKeyThumb` | Thumbnail. |
| `MimeType`, `SizeBytes`, `Width`, `Height` | Para validação e UI. |
| `Sha256` | Prova de integridade, útil em disputa. |
| `Status` | `PendingUpload`, `Uploaded`, `Processed`, `Rejected`, `Deleted`. |
| `UploadedByUserId` | Audit trail. |
| `DeletedAt`, `DeletedByUserId` | Alinhado com soft-delete. |
| `RetentionUntil` | Data planeada para hard-delete. |

### Path structure

Usar prefixos legíveis, mas com IDs não adivinháveis:

```text
tenants/{tenantId}/reparacoes/{repairId}/photos/{photoId}/original.jpg
tenants/{tenantId}/reparacoes/{repairId}/photos/{photoId}/web.jpg
tenants/{tenantId}/reparacoes/{repairId}/photos/{photoId}/thumb.jpg
```

Se `tenantId` ou `repairId` forem inteiros sequenciais, usar GUID público/ULID no path. O path pode conter contexto para auditoria, mas a segurança vem de bucket privado + signed URLs + validação de tenant na API.

### Resizing e thumbnails

MVP:

- Guardar original privado para prova de entrada/saída.
- Gerar `web.jpg` com largura máxima 1600 px, qualidade 75-82, sem EXIF.
- Gerar `thumb.jpg` com largura 320-480 px, sem EXIF.
- Mostrar thumbnails na lista e `web.jpg` no portal cliente.
- Só permitir download do original no backoffice, para técnicos/admins.

Não usar ImageKit/Cloudinary no MVP. São bons produtos, mas introduzem outro subcontratante RGPD, outro custo e mais lock-in. CDN-side resizing fica para depois, quando houver volume real.

### Validação

Client-side:

- Aceitar `image/jpeg`, `image/png`, `image/webp`, eventualmente `image/heic` se o mobile precisar.
- Máximo inicial: 8 MB por foto antes de compressão.
- Compressão no browser para alvo ~1 MB quando possível.
- Máximo por reparação: 12 fotos no plano base, configurável por plano/tenant.

Server-side:

- Validar extensão, MIME e magic bytes.
- Fazer decode/re-encode da imagem para eliminar payloads estranhos e remover EXIF/GPS nas versões servidas.
- Rejeitar SVG para fotos de reparação.
- Validar dimensões mínimas/máximas.
- Guardar hash SHA-256 do original.
- Não confiar no `Content-Type` enviado pelo browser.

### Vírus scanning

Para MVP: **não meter ClamAV síncrono no upload de fotos**. É pesado e acrescenta atrito.

Fazer em vez disso:

- aceitar apenas formatos de imagem raster;
- validar magic bytes;
- re-encodar para `web.jpg`/`thumb.jpg`;
- nunca executar ficheiros;
- servir sempre com `Content-Type` fixo e `Content-Disposition` adequado.

Adicionar ClamAV ou serviço equivalente quando o RepairDesk aceitar PDFs, ZIPs, anexos de clientes ou ficheiros de diagnóstico.

### Soft-delete e retention

Alinhar com `BaseEntity`:

1. Ao apagar foto no produto, fazer soft-delete no SQL e esconder da UI.
2. Marcar objeto com metadata/tag `deleted=true` ou mover para prefixo `trash/`.
3. Hard-delete automático após 30-60 dias, salvo se a reparação estiver em disputa/garantia ativa.
4. Retenção default: **24 meses após `Entregue` ou `Cancelado`**.
5. Permitir tenant configurar 12/24/36 meses, mas avisar sobre custo e RGPD.

Para o Bruno: começar com 24 meses. É suficiente para prova operacional e mantém o crescimento controlado. Rever legalmente antes de vender a oficinas maiores.

---

## Segurança

Regras não negociáveis:

1. Buckets sempre privados.
2. Nunca guardar signed URLs na BD; guardar apenas `bucket` + `objectKey`.
3. Signed GET URLs com TTL curto: 5-15 minutos.
4. Signed PUT URLs com TTL curto: 5-10 minutos e, quando suportado, restrição de `Content-Length`/tipo.
5. Object keys com GUID/ULID; nunca `foto1.jpg` público.
6. A API nunca aceita um `objectKey` arbitrário vindo do cliente para ler/apagar.
7. Todas as queries de fotos levam `TenantId` e `RepairId`.
8. Audit log para upload, visualização sensível, delete e export.
9. Rate limit por tenant/utilizador para upload intent.
10. Quotas por plano para evitar abuso.

Cross-tenant isolation:

- Um bucket por ambiente: `repairdesk-prod-media`, `repairdesk-staging-media`.
- Prefixo obrigatório por tenant.
- Adapter de storage só recebe comandos já validados pela camada de aplicação.
- Testes automáticos para garantir que utilizador do tenant A não consegue obter signed URL de objeto do tenant B.
- Job diário de reconciliação: lista objetos por prefixo e compara com SQL para encontrar órfãos.

---

## RGPD e compliance

Notas de produto, não aconselhamento jurídico.

1. **EU-only**: criar R2 com jurisdição `eu`. Se for B2, escolher EU Central na criação da conta. Se for AWS/Azure, usar região EU. Não usar CDN global para fotos privadas sem análise RGPD.
2. **Encryption at rest**: todos os providers comparados têm encryption at rest; no R2/S3/Azure/B2 ativar ou validar configuração default e documentar no DPA.
3. **DPA/subcontratantes**: adicionar Cloudflare/Backblaze/etc. à lista de subcontratantes do RepairDesk e à política de privacidade.
4. **Minimização**: remover EXIF/GPS das versões `web` e `thumb`. Evitar fotografar cartões SIM, documentos, passwords, ecrãs com dados pessoais ou notificações abertas.
5. **Export do cliente**: export da reparação deve incluir metadata + ZIP com fotos ou links assinados temporários. Para tenant export, gerar pacote por tenant.
6. **Direito ao apagamento**: hard-delete deve apagar SQL + objetos `original/web/thumb` + versões se versioning estiver ativo. Guardar apenas registo mínimo de auditoria quando houver fundamento legal.
7. **Portabilidade**: fotos devem ser exportáveis em formato normal (`jpg/png`) e metadata em JSON/CSV.
8. **Retenção**: usar retenção por estado fechado, não retenção infinita. Fotos são dados pessoais quando identificam pessoa, equipamento, número de série, IMEI ou contexto doméstico/profissional.

---

## Plano de integração técnica

### Fase 1: abstração e provider

1. Criar interface `IObjectStorageService` com operações mínimas: `CreateUploadUrl`, `CreateDownloadUrl`, `HeadObject`, `DeleteObject`, `CopyObject` se necessário.
2. Implementar provider `S3CompatibleObjectStorageService` usando `AWSSDK.S3`.
3. Configurar R2:
   - bucket privado em jurisdição `eu`;
   - CORS limitado aos domínios do RepairDesk;
   - API token só para esse bucket/ambiente;
   - lifecycle para `trash/` e uploads pendentes.
4. Guardar configuração por ambiente, não por tenant no MVP.

### Fase 2: modelo e endpoints

1. Criar entidade `RepairPhoto`.
2. Endpoints:
   - `POST /repairs/{id}/photos/upload-intent`
   - `POST /repair-photos/{id}/complete`
   - `GET /repair-photos/{id}/view-url`
   - `DELETE /repair-photos/{id}`
3. Validar `TenantId` em todos os endpoints.
4. Adicionar quotas iniciais: tamanho máximo, contagem máxima por reparação, storage estimado por tenant.

### Fase 3: processamento de imagem

1. Job background para gerar `web` e `thumb`.
2. Re-encode e strip EXIF.
3. Guardar hash do original e dimensões.
4. Marcar falhas como `Rejected` e apagar objetos inválidos.

### Fase 4: UI

1. Na ficha da reparação, adicionar secções "Antes", "Depois" e "Extra".
2. Upload mobile-first com câmara/galeria.
3. Mostrar thumbnails com skeleton/loading.
4. Antes de enviar foto ao cliente, usar sempre URL assinada.
5. No portal cliente, mostrar apenas fotos marcadas como `VisibleToCustomer`.

### Fase 5: operação

1. Alertas de custo por bucket.
2. Métrica por tenant: storage total, fotos/mês, egress estimado.
3. Job para uploads pendentes há mais de 24h.
4. Job para hard-delete de fotos expiradas.
5. Export por reparação/tenant.

---

## Riscos

| Risco | Impacto | Mitigação |
|---|---|---|
| Fotos originais maiores que 1 MB | Custo 3-8x acima do estimado | Compressão client-side + `web.jpg`; manter original só se fizer sentido legal. |
| Egress inesperado | Conta alta em AWS/Azure/CDN | R2 como default; URLs com TTL; thumbnails; rate limits. |
| Falha cross-tenant | Incidente grave RGPD | Testes automáticos, validação no backend, nunca aceitar objectKey do cliente. |
| Provider muda preços | Margem SaaS afetada | Adapter S3, exports, plano de migração documentado. |
| Uploads mobile instáveis | Má UX no balcão | Signed URLs curtos mas renováveis, progress bar, retry, compressão antes do upload. |
| EXIF/GPS exposto | Dados pessoais desnecessários | Strip EXIF em versões servidas; aviso no backoffice. |
| MinIO single-node tentador | Perda de dados/tempo de operação | Não usar para produção sem cluster e backups. |
| Bunny CDN global | Dados privados em caches fora da UE | Não usar CDN global para fotos privadas; restringir a Europa ou evitar Bunny para este caso. |

---

## Plano de migração entre providers

Desenhar desde o início como se fôssemos mudar de provider.

1. **Não hardcodar URLs**: BD guarda provider, bucket e key. URLs são sempre geradas na hora.
2. **Object key neutra**: não usar features proprietárias no path.
3. **Adapter único**: app chama `IObjectStorageService`; provider troca por configuração.
4. **Checksums**: guardar SHA-256 para validar cópias.
5. **Migração batch**:
   - listar fotos ativas no SQL;
   - copiar objetos para novo provider com `rclone`/S3 sync/job próprio;
   - validar contagem, bytes e hash;
   - atualizar `StorageProvider` em batches;
   - durante 7-30 dias, fazer leitura fallback do provider antigo;
   - apagar provider antigo só depois de export/backup validado.
6. **Dual-write só se necessário**: para migrações pequenas, batch copy chega. Dual-write aumenta bugs.

R2 -> B2/AWS/MinIO é simples por S3 API. Bunny -> S3 é mais chato porque não devemos assumir S3; teria de ser via HTTP API/rclone específico.

---

## Steps 1, 2, 3 para começar

1. Abrir conta Cloudflare, ativar R2, criar bucket privado `repairdesk-dev-media` com jurisdição `eu`.
2. Implementar o adapter S3-compatible no backend e testar upload/download assinado num endpoint de dev.
3. Criar `RepairPhoto` + fluxo `upload-intent`/`complete` numa reparação, ainda sem UI bonita.

Critério de saída do MVP: Bruno consegue tirar 2 fotos "antes" e 2 fotos "depois" pelo telemóvel, vê-las na reparação e gerar um link temporário para o cliente sem tornar o bucket público.

---

## Fontes consultadas

- Cloudflare R2 pricing: https://developers.cloudflare.com/r2/pricing/
- Cloudflare R2 data location/jurisdiction: https://developers.cloudflare.com/r2/reference/data-location/
- Cloudflare R2 durability: https://developers.cloudflare.com/r2/reference/durability/
- Cloudflare R2 S3 SDKs: https://developers.cloudflare.com/r2/examples/aws/
- AWS S3 pricing: https://aws.amazon.com/s3/pricing/
- AWS public price list para Amazon S3 `eu-west-1`: `https://pricing.us-east-1.amazonaws.com/offers/v1.0/aws/AmazonS3/current/eu-west-1/index.json`
- AWS S3 durability: https://docs.aws.amazon.com/AmazonS3/latest/userguide/DataDurability.html
- AWS SDK for .NET S3: https://docs.aws.amazon.com/sdk-for-net/v3/developer-guide/s3-apis-intro.html
- Backblaze B2 pricing/API: https://www.backblaze.com/cloud-storage/transaction-pricing
- Backblaze B2 signup/current price: https://www.backblaze.com/sign-up/cloud-storage
- Backblaze B2 S3 API: https://www.backblaze.com/docs/en/cloud-storage-call-the-s3-compatible-api
- Backblaze B2 regions: https://www.backblaze.com/docs/cloud-storage-data-regions
- Backblaze B2 durability: https://help.backblaze.com/hc/en-us/articles/218485257-B2-Resiliency-Durability-and-Availability
- Azure Blob pricing: https://azure.microsoft.com/en-us/pricing/details/storage/blobs/
- Azure Retail Prices API: https://learn.microsoft.com/en-us/rest/api/cost-management/retail-prices/azure-retail-prices
- Azure Storage redundancy: https://learn.microsoft.com/en-us/azure/storage/common/storage-redundancy
- Bunny pricing: https://bunny.net/pricing/
- Bunny Storage pricing docs: https://docs.bunny.net/storage/pricing
- Bunny Storage overview: https://bunny.net/storage/
- Hetzner Cloud volumes: https://www.hetzner.com/cloud/
- Hetzner traffic docs: https://docs.hetzner.com/robot/general/traffic/
- MinIO license/overview: https://docs.min.io/license/
- USD/EUR usado para conversão: https://cincodias.elpais.com/mercados/divisas/dolar-usa-euro/
