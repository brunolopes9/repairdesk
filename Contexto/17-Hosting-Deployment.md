# Hosting + Deployment em Producao

Atualizado: 2026-05-16  
Projeto: RepairDesk SaaS PT  
Objetivo: escolher onde alojar a primeira producao/beta do RepairDesk, com custo previsivel, RGPD EU-only, SSL, backups, monitoring e deploy simples para fundador solo.

> Documento operacional, nao contrato cloud. Precos consultados em 2026-05-16 em paginas oficiais quando possivel. Valores em EUR nao incluem IVA salvo indicacao em contrario. Valores em USD foram convertidos com cambio operacional aproximado de **1 USD = 0,86 EUR**. Antes de contratar, confirmar carrinho final, IVA, regiao, backups e DPA.

## Decisao curta

**Recomendacao principal para beta 2-3 lojas:**  
**Hetzner Cloud, VPS EU, Docker Compose, Caddy, Cloudflare DNS/proxy, SQL Server Express, Redis local, backups SQL off-site e fotos em Cloudflare R2.**

Porque e a opcao mais aborrecida que funciona:

- custo muito abaixo de 50 EUR/mes;
- corre a stack atual quase sem refactor;
- fica tudo em datacenter EU;
- Docker Compose continua valido quando houver migracao;
- Bruno consegue contratar e por online em 1 dia;
- evita Kubernetes, Terraform, Pulumi, Fargate e outras ferramentas que so pagam renda em equipas maiores.

**Alternativa pratica:** DigitalOcean Droplet EU se o Bruno preferir uma experiencia mais polida, UI simples, monitoring integrado e aceitar pagar mais.  
**Nao recomendado para ja:** Azure Container Apps/App Service, AWS Fargate, Railway/Render/Fly como producao principal com SQL Server. Sao bons noutros contextos, mas pioram custo/operacao nesta fase.

## Estado atual observado

O `RepairDesk/docker-compose.yml` atual tem 4 servicos:

| Servico | Estado atual | Problema para producao |
|---|---|---|
| `db` | SQL Server 2022 container, `MSSQL_PID: Developer`, porta `1433` exposta | Developer nao pode servir clientes reais; porta da DB nao deve ficar publica. |
| `cache` | Redis 7 Alpine, append-only, porta `6379` exposta | Redis nao deve ficar publico; precisa password/rede privada se exposto fora do compose. |
| `api` | .NET 10, `ASPNETCORE_ENVIRONMENT=Development`, porta `5080` | Ambiente e secrets de desenvolvimento; precisa env de producao. |
| `web` | React + nginx, porta `80` | Precisa reverse proxy com HTTPS e dominio. |

Antes da beta:

1. Trocar `MSSQL_PID` para **Express** ou usar uma DB licenciada/gerida.
2. Remover publicacao de portas `1433` e `6379`; DB/cache so na rede Docker privada.
3. Criar `docker-compose.prod.yml` com imagens versionadas, nao builds locais.
4. Criar `.env.production` no servidor, fora do Git.
5. Meter Caddy/Traefik/nginx-proxy a terminar HTTPS em `80/443`.
6. Ativar backups SQL reais; snapshot da VM nao chega.

## Premissas de sizing

Beta inicial:

- 2-3 lojas amigas;
- poucas dezenas/centenas de reparacoes por mes;
- fotos antes/depois fora da VM, em R2 conforme `Contexto/14-Storage-Fotos.md`;
- DB ainda pequena;
- uptime importante, mas aceitavel janela curta de manutencao planeada.

Sizing recomendado:

| Fase | Infra minima | Quando subir |
|---|---|---|
| 1 loja / beta | VPS 4-8 GB RAM, 2-4 vCPU, 40-80 GB SSD | Se SQL Server/Redis causarem OOM, subir para 8 GB. |
| 10 lojas | VPS 8-16 GB RAM ou app/db separados | Se CPU/RAM >70% varias horas, separar DB. |
| 100 lojas | DB em VM/servico dedicado, app escalavel, backups mais formais | Antes de 100 lojas, reavaliar SQL Server vs PostgreSQL. |

Nota honesta: SQL Server em container gosta de RAM. Para beta real eu nao comecava abaixo de 4 GB, e preferia 8 GB se couber no orcamento.

## Tabela comparativa

Custos mensais aproximados para correr a stack atual completa: DB + API + web + Redis + backups basicos + monitoring minimo. Dominio anual nao esta incluido na linha mensal, excepto quando indicado como custo amortizado. Onde a pagina oficial nao da preco final claro sem carrinho/regiao, marquei `{{confirmar 2026}}`.

| Opcao | Regiao EU | Custo mensal realista | SSL | Backups | Monitoring | Setup | Leitura |
|---|---|---:|---|---|---|---|---|
| **Hetzner Cloud VPS** | Alemanha/Finlandia | **~8-20 EUR/mes** para VPS 4-16 GB + backups; CX33 passou para 6,49 EUR/mes e CX43 para 11,99 EUR/mes sem IVA; backups ~20% do servidor | Caddy/Let's Encrypt automatico | Backups da VM + `.bak` SQL off-site para R2/B2/Storage Box | Metricas simples Hetzner + Better Stack/Sentry free/cheap | 0,5-1 dia | **Default recomendado.** Barato, EU, previsivel, suficiente para beta. |
| **OVHcloud VPS / Public Cloud** | UE, forte presenca FR/PT-friendly | **~10-30 EUR/mes {{confirmar 2026}}**; VPS-2 mundial aparece a $9,99/mes, mas preco PT/EU final varia | Caddy/Let's Encrypt automatico | Opcoes OVH/snapshots; confirmar add-ons | Basico; complementar com Better Stack/Sentry | 1 dia | Boa alternativa se Bruno quiser fornecedor europeu conhecido em Portugal. UX/precos menos limpos que Hetzner. |
| **DigitalOcean Droplet** | Amsterdam/Frankfurt | **~25-35 EUR/mes** para Droplet 4 GB + backups; 8 GB aproxima-se de 50 EUR/mes | Caddy/Let's Encrypt; DO App Platform tem managed SSL, mas nao para Compose direto | Backups semanais + snapshots; backups sao percentagem do Droplet | Monitoring incluido + alertas; complementar com Better Stack/Sentry | 0,5-1 dia | Mais caro que Hetzner, mas muito simples e polido. Bom fallback. |
| **Azure Container Apps / App Service** | West/North Europe | **~45-100+ EUR/mes {{confirmar 2026}}** com App Service/Container Apps + Azure SQL + Redis | Managed SSL | Azure SQL backups geridos; App/infra com Azure Monitor | Azure Monitor/Application Insights | 2-5 dias | Bom futuro enterprise; cedo demais para Bruno solo e pode rebentar o limite 50 EUR. |
| **Railway / Render / Fly.io** | Varia por provider; confirmar EU/DPA | **~20-70+ EUR/mes {{confirmar 2026}}**; Railway Hobby $5 mas uso de CPU/RAM/volume paga; SQL Server persistente encarece | Managed SSL | Varia; volumes/backups por provider | Simples; depende do plano | 0,5-2 dias | Bons para prototipos. Menos bons para Compose com SQL Server + Redis + volumes persistentes. |
| **AWS Lightsail / Fargate** | Ireland/Frankfurt/Paris/Milan/Stockholm/Spain, conforme servico | Lightsail **~25-50 EUR/mes**; Fargate + ALB + RDS/SQL costuma ir **80-150+ EUR/mes {{confirmar 2026}}** | Lightsail/Caddy ou ALB/ACM | Snapshots Lightsail; RDS se for gerido | CloudWatch | 1-5 dias | Lightsail serve, mas Hetzner/DO sao mais simples. Fargate nao e MVP para Bruno solo. |
| **Self-host on-prem PT** | Loja/casa Bruno | Hosting aparente 0 EUR, mas energia+UPS+IP+backup+tempo podem dar **10-40+ EUR/mes** | Possivel com Caddy/Cloudflare Tunnel | Manual; risco alto | Manual | 2-5 dias e manutencao continua | Nao usar para beta SaaS. Fibra residencial, CGNAT, cortes, router e seguranca tornam isto fragil. |

## Recomendacao principal

### Carrinho recomendado para contratar

Escolha concreta para abrir beta:

| Item | Escolha | Custo mensal esperado |
|---|---|---:|
| VPS | Hetzner **CX33** ou equivalente 8 GB RAM / 4 vCPU / 80 GB | 6,49 EUR sem IVA |
| Backups VM | Hetzner Cloud Backups | +20% ~= 1,30 EUR |
| Dominio `.pt` | Registrar `.pt` + Cloudflare DNS | ~1-2 EUR amortizado, `{{confirmar registrar}}` |
| Object storage fotos | Cloudflare R2 EU | ~0-2 EUR na beta |
| Monitoring | Better Stack + Sentry free tiers | 0 EUR na beta |

**Total beta recomendado:** ~9-12 EUR/mes sem IVA, assumindo que o CX33 chega.  
**Opcao com mais margem:** Hetzner **CX43** ou equivalente 16 GB RAM / 8 vCPU / 160 GB, 11,99 EUR/mes + ~2,40 EUR de backups; total tipico ~15-20 EUR/mes.

Se SQL Server consumir demasiada RAM no CX33, subir para CX43 e seguir. E preferivel pagar mais 6-8 EUR/mes do que perder horas a afinar memoria durante a beta.

### Infra beta

Contratar:

- Hetzner Cloud, regiao Alemanha ou Finlandia;
- VPS 8 GB se o orcamento permitir; 4 GB so se for beta muito controlada;
- backups automáticos da VM ativados;
- volume/disco suficiente para DB + logs + backups locais curtos;
- Cloudflare como DNS/proxy;
- Caddy no servidor para HTTPS automatico;
- Better Stack/Sentry nos free tiers, alinhado com `Contexto/19-Monitoring.md`;
- backups SQL off-site, alinhados com `Contexto/18-Backup-DR.md`;
- fotos em Cloudflare R2 EU, alinhado com `Contexto/14-Storage-Fotos.md`.

Arquitetura:

```text
Cliente/tecnico
   |
   v
Cloudflare DNS/proxy
   |
   v
VPS EU
   |
   +-- Caddy :80/:443
   |     +-- app.repairdesk.pt  -> web nginx
   |     +-- api.repairdesk.pt  -> api .NET
   |
   +-- Docker network privada
         +-- api
         +-- sqlserver-express
         +-- redis
         +-- backup-runner
```

Regras:

- expor publicamente apenas `22`, `80`, `443`;
- `22` so com SSH key, sem password login;
- idealmente permitir SSH apenas do IP do Bruno ou via Cloudflare/ZTNA no futuro;
- DB/cache sem portas publicadas no host;
- logs com retencao curta e sem dados sensiveis;
- secrets no servidor ou secret manager, nunca no Git;
- backups testados antes de aceitar a primeira loja externa.

## Alternativa recomendada

**DigitalOcean Droplet 4 GB/8 GB em Frankfurt ou Amsterdam.**

Escolher DigitalOcean se:

- Bruno valoriza UI muito clara;
- quer monitoring de Droplet integrado sem pesquisar muito;
- aceita pagar ~2x Hetzner;
- quer uma experiencia de suporte/documentacao mais beginner-friendly.

Nao escolher DigitalOcean se a prioridade maxima for custo mensal minimo.

## SQL Server licensing

### Developer Edition

SQL Server Developer e gratuito e tem funcionalidade de edicao paga, mas e para **desenvolvimento e teste**. Nao usar para beta com clientes reais, mesmo que sejam lojas amigas. O `docker-compose.yml` atual usa `MSSQL_PID: Developer`, por isso isto e bloqueador antes de producao.

### Express Edition

SQL Server Express e gratuito e pode ser usado em producao, mas tem limites:

- ate 10 GB por base de dados;
- recursos limitados de CPU/RAM;
- sem SQL Server Agent;
- menos confortavel para jobs e automacao.

Para a beta, Express chega se:

- fotos ficam fora da DB;
- logs/auditoria nao explodem;
- backups sao feitos por script/container, nao por SQL Agent;
- Bruno acompanha tamanho da DB semanalmente.

Configuracao de beta: trocar para `MSSQL_PID=Express`, manter DB/cache privados e criar backup-runner.

### Standard Edition

SQL Server Standard e a via "correta" quando a DB cresce e Express deixa de chegar, mas e caro para SaaS pre-receita. Antes de comprar Standard, comparar seriamente:

- Azure SQL Database;
- SQL Server Standard em marketplace/cloud com licenca incluida;
- migracao para PostgreSQL.

Nao assumir Standard como inevitavel. Pode matar a margem cedo.

### Plano B: PostgreSQL

Vale a pena migrar para PostgreSQL quando acontecer uma destas:

1. DB aproxima-se de 5-8 GB ainda antes de haver receita confortavel.
2. O RepairDesk quer managed DB barata e simples.
3. O custo/licenciamento SQL Server comeca a ser maior que o custo de migracao.
4. Queremos correr em PaaS mais barato sem hacks.
5. Antes de 100 lojas, se ainda houver tempo para migrar sem dor.

Nao recomendo migrar antes da beta se isso atrasar 2-3 meses. Recomendo sim: **abrir beta com Express, medir crescimento real e decidir PostgreSQL antes do lancamento comercial grande.**

## DNS e dominio

### Onde comprar

Opcoes:

| Opcao | Leitura |
|---|---|
| **DNS.pt / registrar acreditado .pt** | Caminho natural para `repairdesk.pt`. Confirmar preco anual e renovacao. |
| **Cloudflare Registrar** | Vende dominios a preco de custo, mas confirmar se suporta `.pt` no momento da compra. Se nao suportar, comprar noutro registrar e usar Cloudflare DNS. |
| **GoDaddy/Namecheap/outros** | Funciona, mas evitar upsells e renovacoes caras. |

Recomendacao: comprar `repairdesk.pt` num registrar `.pt` confiavel se Cloudflare nao suportar `.pt`, e depois apontar nameservers para Cloudflare.

### Subdominios

| Subdominio | Uso | Cloudflare proxy |
|---|---|---|
| `app.repairdesk.pt` | frontend web | Sim |
| `api.repairdesk.pt` | API .NET | Sim, salvo problema com uploads/websockets futuros |
| `status.repairdesk.pt` | status page Better Stack/Uptime Kuma | Sim ou CNAME para provider |
| `admin.repairdesk.pt` | opcional, admin interno futuro | Sim, com acesso restrito |

SSL:

- Cloudflare SSL mode: **Full (strict)**;
- Caddy emite/renova Let's Encrypt no origin;
- nao usar "Flexible SSL";
- HSTS so depois de confirmar tudo, nao no primeiro dia.

## Estrategia de deploy

### MVP de deploy

Fluxo recomendado:

1. GitHub Actions corre testes/build.
2. Build de imagens Docker `api` e `web`.
3. Push para GitHub Container Registry (`ghcr.io`).
4. SSH para VPS.
5. `docker compose -f docker-compose.prod.yml pull`.
6. Backup rapido da DB antes de migrations.
7. Correr migrations num job controlado.
8. `docker compose -f docker-compose.prod.yml up -d`.
9. Health check `api.repairdesk.pt/api/health`.
10. Se falhar, rollback para tag anterior.

### Tags

Usar sempre:

- tag por commit SHA, ex. `api:2026-05-16-a1b2c3d`;
- tag movel `api:prod`;
- manifest de deploy com `commit`, `data`, `imagem_api`, `imagem_web`.

Evitar `latest` em producao sem rasto.

### Rollback rapido

Ter um script no servidor:

```bash
./deploy.sh rollback 2026-05-16-a1b2c3d
```

O rollback deve:

1. apontar `docker-compose.prod.yml` para tags anteriores;
2. `docker compose pull`;
3. `docker compose up -d`;
4. verificar health check;
5. registar no log de deploy.

DB rollback e mais delicado. Regra de beta: migrations destrutivas so com backup imediato e janela de manutencao. Evitar `drop column`, renames destrutivos e deletes massivos sem plano.

### Zero downtime

Para os primeiros 2-3 clientes:

- aceitar deploy com 30-60 segundos de indisponibilidade planeada;
- fazer deploy fora de horas;
- avisar clientes se for em horario comercial.

Quando houver 10+ lojas ativas:

- criar blue-green simples para `api` (`api_blue`/`api_green`);
- Caddy aponta para a versao saudavel;
- migrations backwards-compatible;
- frontend estatico troca quase instantanea.

Nao implementar blue-green antes de haver clientes suficientes para justificar a complexidade.

## Backups e DR

Snapshots do provider sao bons para recuperar depressa, mas nao substituem backup da DB.

Minimo antes da beta:

| Item | Frequencia | Destino |
|---|---:|---|
| Snapshot VM | diario ou automatico do provider | Mesmo provider |
| SQL full backup | diario | Local curto + off-site EU |
| SQL log backup | horario se usar recovery model FULL | Off-site EU |
| Backup `.env`/config manifest | quando mudar | Secret manager + off-site encriptado |
| Restore test | mensal; primeiro antes da beta | VM temporaria ou ambiente staging |

Usar o runbook em `Contexto/18-Backup-DR.md` como fonte operacional.

## Monitoring

Para beta:

- Better Stack para uptime de `app`, `api`, health check e status page;
- Sentry para exceptions backend/frontend;
- alertas por email e telemovel;
- alertar se backup falhar ou nao correr ha 24h;
- logs aplicacionais com retencao definida.

Nao comecar com Prometheus/Grafana/Loki self-hosted. E mais uma stack para manter.

## RGPD e compliance

Obrigatorio:

- servidor, backups e object storage em UE/EEE sempre que possivel;
- DPA com hosting provider;
- subprocessors listados na privacy policy/DPA do RepairDesk;
- logs com retencao definida;
- nao escrever passwords, tokens, IMEI, notas sensiveis ou dados de cliente completos em logs;
- apagar/exportar dados conforme processos de `Contexto/16-Compliance-RGPD.md`;
- backups encriptados e com retencao clara;
- acesso SSH/admin com MFA onde aplicavel.

Retencao inicial sugerida:

| Dado | Retencao |
|---|---:|
| Logs app tecnicos | 30 dias |
| Logs de seguranca/admin | 90-180 dias |
| Backups DB diarios | 30 dias |
| Backups mensais | 12 meses |
| Fotos reparacao | conforme `14-Storage-Fotos.md`; default 24 meses ou politica contratual |

## Plano semana 1, 2, 3

### Semana 1 - Meter online com seguranca minima

1. Comprar dominio ou confirmar dominio.
2. Criar conta Hetzner e escolher datacenter EU.
3. Criar VPS 8 GB se possivel; 4 GB se beta apertada.
4. Configurar SSH key, firewall `22/80/443`, updates automaticos.
5. Instalar Docker, Docker Compose e Caddy.
6. Criar `docker-compose.prod.yml`.
7. Trocar SQL Server para Express.
8. Remover portas publicas de DB/Redis.
9. Criar `.env.production` forte.
10. Apontar Cloudflare DNS.
11. Confirmar HTTPS em `app.repairdesk.pt` e `api.repairdesk.pt`.
12. Fazer deploy manual e health check.

### Semana 2 - Automatizar deploy, backups e alertas

1. Criar GitHub Actions para build/push no GHCR.
2. Criar script `deploy.sh` no servidor.
3. Criar backup-runner SQL.
4. Enviar backups para R2/B2/Storage Box EU.
5. Fazer restore test numa VM/ambiente separado.
6. Ligar Better Stack e Sentry.
7. Criar status page `status.repairdesk.pt`.
8. Definir retencao de logs.
9. Documentar runbook de incidentes basico.

### Semana 3 - Preparar beta real

1. Rever DPA/subprocessors em `16-Compliance-RGPD.md`.
2. Validar privacy policy e termos.
3. Criar checklist de onboarding da primeira loja.
4. Fazer teste de carga simples: login, criar reparacao, upload foto, PDF, WhatsApp link.
5. Confirmar backup antes/depois do teste.
6. Convidar 1 loja primeiro, nao 3 no mesmo dia.
7. Monitorizar durante 7 dias.
8. So depois abrir a segunda/terceira loja.

## Checklist pre-launch

Infra:

- [ ] VPS em regiao EU.
- [ ] Firewall ativa: so `22`, `80`, `443`.
- [ ] SSH password login desligado.
- [ ] Docker Compose prod separado do dev.
- [ ] `ASPNETCORE_ENVIRONMENT=Production`.
- [ ] `MSSQL_PID=Express` ou DB licenciada.
- [ ] SQL Server sem porta publica.
- [ ] Redis sem porta publica.
- [ ] Secrets fortes e fora do Git.

DNS/SSL:

- [ ] `app.repairdesk.pt` criado.
- [ ] `api.repairdesk.pt` criado.
- [ ] `status.repairdesk.pt` criado.
- [ ] Cloudflare SSL em Full (strict).
- [ ] Certificado origin valido.
- [ ] Renovacao SSL testada/observada.

Backups:

- [ ] Snapshot automatico ativo.
- [ ] Backup SQL diario.
- [ ] Backup log horario ou diferencial 6/6h.
- [ ] Off-site EU configurado.
- [ ] Restore test feito e documentado.
- [ ] Alerta se backup falhar.

Monitoring:

- [ ] Health check API.
- [ ] Uptime frontend.
- [ ] Sentry backend.
- [ ] Sentry frontend.
- [ ] Alertas no email/telemovel do Bruno.
- [ ] Status page publica.

RGPD:

- [ ] DPA do provider guardado.
- [ ] Subprocessor adicionado ao DPA/Privacy Policy.
- [ ] Retencao de logs definida.
- [ ] Procedimento de export/apagamento conhecido.
- [ ] Backups encriptados.

Produto:

- [ ] Tenant demo removido ou isolado.
- [ ] Seed admin password alterada.
- [ ] Emails/WhatsApp em modo correto.
- [ ] PDFs/orcamentos testados.
- [ ] Criar reparacao, editar estado e fechar reparacao testado ponta-a-ponta.

## Custo total mensal estimado

Estimativa usando a recomendacao Hetzner + R2 + Better Stack/Sentry free tiers. Sem IVA. Dominio anual `.pt` tratado como ~1 EUR/mes amortizado, mas confirmar no registrar.

| Escala | Infra sugerida | Custo esperado | Comentario |
|---|---|---:|---|
| **1 loja / beta** | 1 VPS 4-8 GB, backups VM, SQL Express, R2, monitoring free | **~10-18 EUR/mes** | Abaixo de 50 EUR com margem. Melhor gastar tempo em backups/testes. |
| **10 lojas** | VPS 8-16 GB ou app+DB pequenos separados, backups reforcados, R2 | **~20-45 EUR/mes** | Ainda cabe no budget se SQL Express continuar suficiente. |
| **100 lojas** | App e DB separados, DB 16-32 GB ou managed, monitoring pago, backups mais fortes | **~70-150 EUR/mes sem SQL Standard** | Antes desta fase decidir PostgreSQL/Azure SQL/SQL Standard. Licenciamento pode mudar tudo. |

Se for preciso SQL Server Standard cedo, estes numeros deixam de ser validos. Marcar decisao: `{{confirmar custo SQL Server Standard/Azure SQL antes de 20 lojas pagantes}}`.

## Plano de migracao se mudarmos provider

Manter desde o inicio:

1. `docker-compose.prod.yml` sem dependencias proprietarias.
2. Imagens no GHCR, nao no registry do provider.
3. Backups SQL em formato `.bak` + manifest.
4. Fotos em S3-compatible/R2 com paths portaveis.
5. DNS em Cloudflare, nao no provider de hosting.
6. TTL DNS baixo durante migracoes.
7. Runbook de restore testado.

Migracao:

1. Criar nova VPS EU.
2. Instalar Docker/Caddy.
3. Copiar `.env.production` de forma segura.
4. Restaurar ultimo backup SQL.
5. Sincronizar uploads/object storage se necessario.
6. Fazer deploy das mesmas imagens.
7. Testar health checks e login.
8. Baixar TTL e trocar DNS.
9. Manter servidor antigo read-only/standby 48h.
10. Desligar antigo so depois de confirmar backups e trafego.

## Decisao final para Bruno

Comprar agora:

1. **Hetzner Cloud VPS EU**, classe 8 GB se couber no orcamento.
2. **Dominio `.pt`** num registrar confiavel; Cloudflare DNS por cima.
3. **Cloudflare R2 EU** para fotos/backups auxiliares conforme doc 14.
4. **Better Stack + Sentry** nos planos free/cheap para beta.

Primeiro objetivo: uma loja real online com SSL, backup testado e alertas. Nao tres lojas de uma vez. Nao Kubernetes. Nao self-host em casa. Nao SQL Server Developer.

## Fontes consultadas

Precos e produtos:

- Hetzner Cloud pricing e price adjustment 2026: <https://www.hetzner.com/cloud/> e <https://docs.hetzner.com/general/infrastructure-and-availability/price-adjustment/>
- Hetzner European Cloud/specs: <https://www.hetzner.com/european-cloud>
- Hetzner backups/snapshots: <https://docs.hetzner.com/cloud/servers/backups-snapshots/overview/>
- OVHcloud VPS pricing: <https://www.ovhcloud.com/en/vps/>
- DigitalOcean Droplets pricing: <https://www.digitalocean.com/pricing/droplets> e backups: <https://docs.digitalocean.com/products/images/backups/>
- Azure Container Apps/App Service/Azure SQL pricing: <https://azure.microsoft.com/en-us/pricing/details/container-apps/>, <https://azure.microsoft.com/en-us/pricing/details/app-service/linux/>, <https://azure.microsoft.com/en-us/pricing/details/azure-sql-database/single/>
- Railway pricing: <https://docs.railway.com/reference/pricing/plans>
- Render pricing: <https://render.com/pricing>
- Fly.io pricing: <https://fly.io/docs/about/pricing/>
- AWS Lightsail pricing: <https://aws.amazon.com/lightsail/pricing/> e Fargate pricing: <https://aws.amazon.com/fargate/pricing/>

Licenciamento, DNS e SSL:

- SQL Server downloads/editions: <https://www.microsoft.com/en-us/sql-server/sql-server-downloads> e <https://learn.microsoft.com/en-us/sql/sql-server/editions-and-components-of-sql-server-2022>
- Cloudflare Registrar: <https://www.cloudflare.com/products/registrar/>
- Cloudflare SSL Full strict: <https://developers.cloudflare.com/ssl/origin-configuration/ssl-modes/full-strict/>
- Caddy automatic HTTPS: <https://caddyserver.com/docs/automatic-https>

Documentos internos relacionados:

- `Contexto/14-Storage-Fotos.md`
- `Contexto/16-Compliance-RGPD.md`
- `Contexto/18-Backup-DR.md`
- `Contexto/19-Monitoring.md`
