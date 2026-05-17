# Performance & Caching strategy

Atualizado: 2026-05-16  
Projeto: RepairDesk SaaS PT  
Stack atual: .NET 10 + EF Core 10 + SQL Server 2022 + Redis 7 + React 19 + Vite + React Query 5  
Estado atual: dogfooding LopesTech, 1 tenant, ~16 reparações, ~20 clientes

> Objetivo: chegar a 10 -> 100 -> 1000 lojas sem reescrever o produto. A regra é simples: medir primeiro, optimizar queries/índices antes de cache, e só pôr Redis onde a invalidação é óbvia.

## Decisão curta

**Não começar por "meter Redis em tudo".**

A estratégia recomendada é:

1. Instrumentar endpoints e SQL para saber onde dói.
2. Corrigir queries EF Core que materializam dados cedo demais, sobretudo dashboard.
3. Criar índices compostos/covering só depois de ver planos reais.
4. Usar cache por tenant para dados caros e pouco voláteis: tenant settings, dashboard aggregates e portal público.
5. Deixar CDN/HTTP cache para assets estáticos e respostas públicas cuidadosamente seleccionadas.
6. Manter Docker Compose simples: API, web, SQL Server, Redis. Sem Kafka, sem Elasticsearch, sem arquitectura "enterprise cosplay".

O maior ganho provável está no dashboard: hoje há queries que trazem listas para memória e depois fazem `GroupBy`/`Sum` no C#. Para 16 reparações não interessa; para 100 lojas começa a aparecer.

## Estado observado no código

| Área | Observação | Impacto |
|---|---|---|
| API | `UseSerilogRequestLogging()` activo, mas sem OpenTelemetry, MiniProfiler, OutputCache ou Redis registado em `Program.cs`. | Temos logs básicos, mas não temos traces SQL nem métricas p95 por endpoint. |
| EF Core | EF Core 10.0.0 + SQL Server provider 10.0.0. Uso parcial de `AsNoTracking`. | Boa base, mas falta estratégia consistente para reads. |
| Dashboard | Várias queries materializam listas com `ToListAsync()` e agregam em memória. | Primeiro sítio a optimizar antes de Redis. |
| Reparações | Lista usa `AsNoTracking()` e `Include(Cliente)`, com paginação. Pesquisa usa `LIKE %termo%`. | Aceitável no início; pesquisa não escala indefinidamente com índices normais. |
| Índices | Bons índices base: `TenantId`, `Numero`, `Estado`, `ClienteId`, `PublicSlug`, filtros para soft-delete. | Falta validar covering indexes para dashboard/listas reais. |
| Frontend | React Query tem `staleTime: 30s`, `retry: 1`, `refetchOnWindowFocus: false`. | Sensato para beta, mas precisa de políticas por tipo de query. |
| Web/nginx | Assets Vite em `/assets/` já têm `Cache-Control: public, immutable`. | Boa base para CDN. |
| Redis | Existe na stack, mas sem uso na API. | Deve entrar só para caches com ROI claro. |
| PWA | Estratégia offline está definida em `24-PWA-Offline.md`. | O service worker não deve fazer cache genérico de APIs autenticadas. |

## Métricas alvo

| Fluxo | Meta beta | Meta 100 lojas | Como medir |
|---|---:|---:|---|
| `GET /api/reparacoes?page=1&pageSize=20` | p95 < 300 ms | p95 < 300 ms | Serilog + OpenTelemetry + teste k6 |
| `GET /api/reparacoes/{id}` | p95 < 250 ms | p95 < 300 ms | Trace por request + contagem SQL |
| `GET /api/dashboard` | p95 < 700 ms | p95 < 500 ms com cache | Trace API + SQL Query Store |
| `GET /api/dashboard/financeiro` | p95 < 700 ms | p95 < 500 ms com cache | Trace API + SQL Query Store |
| Portal público `/r/{slug}` | LCP < 1 s | LCP < 1 s | Lighthouse/WebPageTest + RUM simples |
| Import CSV 1000 linhas | < 30 s | < 30 s | Job timing + contadores por linha |
| Export CSV 10k linhas | primeiro byte < 1 s | primeiro byte < 1 s | Streaming test + memória do processo |
| Frontend bundle inicial | < 250 KB gzip ideal, < 350 KB aceitável | igual | `npm run build` + bundle analyzer |

Estas metas são realistas para uma app vertical com tenants pequenos. Se o dashboard precisa de 2 segundos com 100 lojas, quase de certeza é query/índice/carga em memória, não falta de Kubernetes.

## Checklist de profiling

### Sprint 1: medir antes de optimizar

- [ ] Guardar tempo total por request, status code, rota, tenant e `RequestId`.
- [ ] Não logar PII: nada de telefone, NIF, IMEI completo, email ou nomes de cliente em logs estruturados.
- [ ] Medir p50/p95/p99 por endpoint, não apenas média.
- [ ] Medir número de queries SQL por request.
- [ ] Medir duração total SQL por request.
- [ ] Ligar logs EF Core detalhados só em Development/Staging, nunca com parâmetros sensíveis em produção.
- [ ] Activar Query Store no SQL Server para capturar planos e runtime stats.
- [ ] Criar uma dashboard simples em Better Stack/Grafana/App Insights com os 10 endpoints mais lentos.
- [ ] Definir orçamento de payload por endpoint: lista de reparações não deve devolver entidades completas desnecessárias.
- [ ] Guardar baseline antes de qualquer índice/cache.

### Endpoints a monitorizar primeiro

| Endpoint | Porquê |
|---|---|
| `GET /api/dashboard` | Agregações e múltiplas queries. Primeiro candidato a optimização. |
| `GET /api/dashboard/financeiro` | Mistura reparações, trabalhos e despesas. |
| `GET /api/dashboard/tendencia` | Agregação temporal; tende a crescer com histórico. |
| `GET /api/dashboard/top-reparacoes` | Ranking e filtros por período. |
| `GET /api/reparacoes` | Endpoint de uso diário; deve ficar sempre rápido. |
| `GET /api/reparacoes/{id}` | Inclui cliente/timeline; risco se timeline crescer. |
| `GET /api/reparacoes/historico-imei` | Pesquisa específica; precisa de índice certo. |
| `GET /api/public/repair/{slug}` | Portal público; clientes refrescam várias vezes. |
| `GET /api/reparacoes/export.csv` | Risco de memória se exportar tudo com `ToList`. |
| `POST /api/reparacoes/import` | Operação batch; medir throughput e falhas por linha. |

### Ferramentas recomendadas

| Ferramenta | Usar quando | Notas |
|---|---|---|
| Serilog request logging | Já | Manter; acrescentar `RequestId`, tenant e tempos. |
| MiniProfiler | Dev/Staging | Bom para ver EF/SQL rapidamente durante desenvolvimento. Não expor em produção pública. |
| OpenTelemetry | Beta em produção | Traces HTTP + SQLClient + métricas. Alinha com `19-Monitoring.md`. |
| Application Insights | Só se a produção for Azure | Bom produto, mas não escolher Azure só por isto. |
| SQL Server Query Store | Desde beta | Fonte principal para queries lentas, planos e regressões. |
| DMVs SQL | Quando houver lentidão real | `sys.dm_db_missing_index_*`, `sys.dm_exec_query_stats`, `sys.dm_db_index_usage_stats`. |
| k6 | Pré-release e nightly | Já recomendado em `27-Plano-Testes.md`. |
| Lighthouse/WebPageTest | Portal público + app shell | Medir LCP, assets, cache headers. |

## Optimizações EF Core 10

### 1. Usar projections antes de cache

Preferir:

```csharp
await _db.Reparacoes
    .AsNoTracking()
    .Where(r => r.TenantId == tenantId)
    .OrderByDescending(r => r.Numero)
    .Select(r => new ReparacaoListItemDto
    {
        Id = r.Id,
        Numero = r.Numero,
        ClienteNome = r.Cliente.Nome,
        Equipamento = r.Equipamento,
        Estado = r.Estado,
        UpdatedAt = r.UpdatedAt
    })
    .Take(20)
    .ToListAsync(ct);
```

Evitar carregar entidade completa + `Include` + mapear em memória quando a UI só precisa de 8 campos.

### 2. Dashboard: empurrar agregações para SQL

Onde o repositório faz `ToListAsync()` e depois `GroupBy`, `Sum` ou `Count` em memória, mudar para LINQ traduzido para SQL:

- `CountAsync` para contagens.
- `SumAsync` para receita/custos.
- `GroupBy(...).Select(...)` para gráficos.
- `Take(n)` antes de materializar rankings.
- Projecções pequenas para DTOs.

Cachear dashboard antes disto só esconde desperdício e cria invalidação cedo demais.

### 3. `AsNoTracking` por defeito em reads

Usar `AsNoTracking()` em:

- listas;
- dashboard;
- portal público;
- export CSV;
- pesquisas;
- lookup tables/settings.

Usar tracking só quando a entidade vai ser alterada no mesmo `DbContext`. Se houver reads com múltiplas referências repetidas à mesma entidade, considerar `AsNoTrackingWithIdentityResolution`, mas só depois de medir.

### 4. Split queries vs single query

Regra prática:

| Caso | Decisão |
|---|---|
| Uma entidade + uma coleção pequena, como reparação + timeline curta | Single query pode ficar. |
| Várias coleções `Include` no mesmo endpoint | Testar `AsSplitQuery()`. |
| Query com paginação `Skip/Take` + split | Garantir `OrderBy` totalmente único, por exemplo `UpdatedAt DESC, Id DESC`. |
| DTO específico sem navegações completas | Preferir `Select` e evitar `Include`. |

Não activar split queries globalmente já. Usar caso a caso quando aparecer cartesian explosion.

### 5. N+1 detection

Sinais:

- Um request gera dezenas/centenas de queries quase iguais.
- Logs SQL mostram o mesmo `SELECT` repetido por cada reparação/cliente.
- O tempo cresce linearmente com o número de linhas visíveis.

Como apanhar:

- MiniProfiler em dev.
- OpenTelemetry SQL spans em beta.
- Alerta se um request normal fizer > 10 queries SQL.
- Teste de integração que carrega 50 reparações e valida número aproximado de queries em endpoints críticos, se se justificar.

### 6. Compiled queries: adiar

EF Core já faz cache interna por shape de query. Compiled queries só fazem sentido quando:

- o endpoint é muito chamado;
- a query é estável e parametrizada;
- o SQL/índice já foi optimizado;
- o profiling mostra overhead de compilação relevante.

Provável decisão até 100 lojas: **não usar**. Primeiro ROI está em projections, índices e cache de agregados.

### 7. DbContext pooling: cuidado com multi-tenant

`AddDbContextPool` pode reduzir overhead, mas o RepairDesk tem `ITenantContext` e filtros globais por tenant. Só considerar se:

- houver teste que prove que o tenant não fica preso num contexto reutilizado;
- o contexto não guardar estado mutável perigoso;
- houver cobertura de multi-tenant antes/depois.

Para beta, manter `AddDbContext` simples.

## Índices SQL Server

### Regra de criação

Criar índice composto quando se verificam 4 condições:

1. Query aparece no top lento por Query Store/APM.
2. Predicate começa por `TenantId` e filtros reais (`Estado`, `Data`, `EntregueEm`, etc.).
3. A query corre frequentemente ou bloqueia fluxo diário.
4. O plano mostra scan caro, lookup repetido ou missing index consistente.

Não criar índice só porque uma DMV sugeriu. Missing index suggestions são pistas, não ordens.

### Índices candidatos

Validar com planos reais antes de migrar.

| Área | Índice candidato | Para quê |
|---|---|---|
| Lista por estado | `Reparacoes(TenantId, Estado, Numero DESC) INCLUDE (ClienteId, Equipamento, UpdatedAt, EstadoPagamento)` filtrado por `IsDeleted = 0` | Listagem/filtros de reparações sem lookups caros. |
| Dashboard mensal | `Reparacoes(TenantId, EntregueEm, EstadoPagamento) INCLUDE (PrecoFinalCents, OrcamentoCents, ClienteId, Estado)` filtrado por `IsDeleted = 0` | Receita, concluídas, pagas, pendentes. |
| Reparações em aberto | `Reparacoes(TenantId, Estado, UpdatedAt) INCLUDE (Numero, ClienteId, Equipamento)` filtrado por `IsDeleted = 0` | Dashboard operacional e alertas. |
| Histórico IMEI | `Reparacoes(TenantId, Imei) INCLUDE (Numero, ClienteId, Estado, CreatedAt, EntregueEm)` filtrado por `IsDeleted = 0 AND Imei IS NOT NULL` | Pesquisa exacta por IMEI. |
| Trabalhos financeiro | `Trabalhos(TenantId, Status, DataConclusao, EstadoPagamento) INCLUDE (PrecoFinalCents, OrcamentoCents, Categoria, ClienteId)` filtrado por `IsDeleted = 0` | Dashboard financeiro. |
| Despesas por período | `Despesas(TenantId, Data) INCLUDE (ValorCents, Categoria, ReparacaoId, TrabalhoId)` filtrado por `IsDeleted = 0` | Custos por mês/categoria. |
| Portal público | `PublicSlug` único já existe | Manter; slug aleatório e não sequencial. |

### Pesquisa textual

A pesquisa actual com `LIKE %termo%` em equipamento/avaria/cliente não vai usar bem índices normais com wildcard inicial. Caminho recomendado:

1. Até 100 lojas: manter, limitar paginação e medir.
2. Se ficar lento: adicionar coluna normalizada simples (`SearchText`) por tenant, actualizada em escrita.
3. Se ainda não chegar: SQL Server Full-Text Search.
4. Só considerar Elasticsearch/OpenSearch muito mais tarde, se houver pesquisa avançada real.

## Caching matrix

### Backend/API

| Dados | Onde cachear | Chave | TTL | Invalidação | Default |
|---|---|---|---:|---|---|
| Tenant settings | Redis ou `IMemoryCache` na fase 1 | `rd:{env}:tenant:{tenantId}:settings:v1` | 30-60 min | Ao actualizar settings/logo/dados loja | Sim |
| Dashboard resumo | Redis | `rd:{env}:tenant:{tenantId}:dashboard:summary:{yyyyMMdd}:v1` | 2-5 min | Criar/editar reparação, trabalho, despesa, pagamento | Sim após optimizar SQL |
| Dashboard financeiro | Redis | `rd:{env}:tenant:{tenantId}:dashboard:financeiro:{yyyyMM}:v1` | 5 min | Alterações financeiras | Sim |
| Tendência mensal | Redis | `rd:{env}:tenant:{tenantId}:dashboard:tendencia:{from}:{to}:v1` | 10-15 min | Alterações financeiras ou nightly | Sim |
| Top reparações/clientes | Redis | `rd:{env}:tenant:{tenantId}:dashboard:top:{periodo}:v1` | 10 min | Alterações em reparações/trabalhos | Sim |
| Portal público por slug | Redis | `rd:{env}:public:repair:{slug}:v1` | 30-120 s | Mudança de estado/orçamento/levantamento | Sim, curto |
| Templates WhatsApp | `IMemoryCache` | `rd:{env}:tenant:{tenantId}:whatsapp-templates:v1` | 30-60 min | Alteração de templates | Sim |
| Lista de reparações | Não no backend | N/A | N/A | React Query + SQL rápido | Não |
| Detalhe reparação autenticado | Não inicialmente | N/A | N/A | ETag mais tarde | Não |
| Export CSV | Não | N/A | N/A | Streaming/batching | Não |
| Dados por utilizador/permissão | Evitar | incluir `userId` se inevitável | curto | Logout/permissões | Não |

### HTTP cache

| Recurso | Header recomendado | Nota |
|---|---|---|
| `/assets/*` Vite hashed | `Cache-Control: public, max-age=31536000, immutable` | Já existe em `nginx.conf`; Cloudflare pode respeitar. |
| `index.html` SPA | `Cache-Control: no-cache` | Precisa revalidar para apanhar deploy novo. |
| APIs autenticadas | `Cache-Control: private, no-store` por defeito | Evita leak em browser partilhado na loja. |
| Tenant settings GET | `ETag` + `Cache-Control: private, max-age=60` | Só se não incluir segredos. |
| Portal público API | `ETag` + `Cache-Control: private, max-age=30` | Não pôr em cache CDN partilhada; tem dados de cliente/reparação. |
| Fotos antes/depois originais | Signed URL TTL 5-15 min | Alinhar com `14-Storage-Fotos.md`. |
| Thumbnails de fotos | CDN se autorização permitir | Se forem sensíveis, signed URL também. |

### React Query

| Query | `staleTime` | Invalidação |
|---|---:|---|
| Lista reparações | 20-30 s | Após criar/editar/mudar estado/reabrir. |
| Detalhe reparação | 15-30 s | Após mudança de estado, diagnóstico, pagamento, timeline. |
| Dashboard | 60 s | Após mutações financeiras/operacionais; pode refetch em background. |
| Tenant settings | 5-10 min | Após guardar definições. |
| Portal público | 30 s | Refetch manual/intervalo leve enquanto reparação aberta. |
| Tabelas estáticas/status labels | 1 h | Nova versão do frontend. |

Ao trocar de tenant ou fazer logout: `queryClient.clear()` para não ficar cache de outra loja no browser.

### `IMemoryCache` vs Redis

| Situação | Escolha |
|---|---|
| Uma instância API no VPS | `IMemoryCache` chega para lookups pequenos. |
| Cache que precisa sobreviver a restart? | Redis. |
| Cache partilhada por várias instâncias API | Redis. |
| Dados caros de calcular e iguais para todos os users da tenant | Redis. |
| Dados por utilizador/permissão | Evitar ou incluir `userId` na chave. |
| Objectos grandes | Evitar cache; corrigir query/payload. |

Para a fase beta, dá para começar com `IDistributedCache` usando Redis só nas entradas escolhidas. O código fica portável e não obriga a acoplar tudo a StackExchange.Redis directamente.

## Invalidação

Estratégia recomendada: **TTL curto + invalidação manual nos eventos óbvios**.

Eventos que devem limpar dashboard da tenant:

- criar reparação;
- editar reparação;
- mudar estado;
- marcar pagamento;
- criar/editar/apagar despesa;
- criar/editar/apagar trabalho;
- importar CSV;
- associar custo/peça a reparação.

Eventos que devem limpar portal público:

- mudança de estado;
- orçamento aprovado/recusado;
- alteração de valor/ETA;
- reparação entregue/cancelada;
- garantia alterada.

Padrões:

- Todas as chaves começam com `rd:{env}:`.
- Todas as chaves privadas incluem `tenant:{tenantId}`.
- Se a resposta depende de permissões, incluir `user:{userId}` ou não cachear.
- Usar sufixo de versão `:v1`; quando o DTO muda, subir para `:v2`.
- Cache miss deve ser seguro e rápido; Redis em baixo não pode mandar a API abaixo.
- Não guardar PII desnecessária em Redis. Quando inevitável, TTL curto e sem logs de payload.

## Frontend performance

### Bundle splitting

Prioridade:

1. Lazy load de rotas pesadas: `Definicoes`, `Dashboard`, `Trabalhos`, `Despesas`, `Diagnostico`, portal público.
2. Se houver biblioteca de charts pesada, importar só dentro do dashboard.
3. Evitar meter PDF/export/charts no bundle inicial.
4. Medir após cada sprint com `npm run build`.

Meta: a primeira visita à app deve carregar o shell e a lista principal, não o produto inteiro.

### Imagens

| Tipo | Regra |
|---|---|
| Logo da tenant | Redimensionar no upload; servir WebP/PNG optimizado. |
| Fotos antes/depois | Guardar original, gerar thumbnail e medium. Nunca usar original em listas. |
| Portal público | `loading="lazy"` para imagens abaixo da dobra. |
| Avatares/ícones | Preferir SVG/icon font local; evitar imagens remotas. |

### Service worker / PWA

Alinhar com `24-PWA-Offline.md`:

- cachear app shell e assets versionados;
- IndexedDB para dados offline;
- sync queue para mutações;
- não fazer cache genérico de `GET /api/*` autenticado no service worker;
- nunca misturar dados offline entre tenants.

## CDN

Cloudflare deve ser usado como proxy/CDN simples, já alinhado com `17-Hosting-Deployment.md`.

| Recurso | Cloudflare cache? | Decisão |
|---|---|---|
| JS/CSS/assets Vite | Sim | Cache longo, purge automático por filename hash. |
| `index.html` | Revalidar | Não cachear agressivamente. |
| API autenticada | Não | Risco de dados por tenant/user. |
| Portal público app shell | Sim | É frontend estático. |
| Portal público API | Não por defeito | Pode conter PII/estado; usar Redis server-side curto. |
| Fotos thumbnails | Talvez | Só se signed URLs/controlo de acesso estiver claro. |
| Fotos originais | Não público | Signed URLs curtas. |

## Database scaling path

### 1 loja -> 10 lojas

- SQL Server Express pode chegar, desde que DB < 5 GB e p95 esteja dentro das metas.
- Backups diários e restore testado valem mais do que réplica.
- Optimizar dashboard/listas antes de mexer em edição SQL.

### 10 lojas -> 100 lojas

Gatilhos para sair de SQL Server Express:

- DB passa 7-8 GB e aproxima-se do limite de 10 GB.
- Buffer pool limitado causa leituras físicas frequentes.
- CPU/RAM do VPS passam a ser gargalo.
- Backups/maintenance ficam difíceis.
- Necessidade de SQL Agent, jobs mais robustos ou melhor observabilidade.

Opções:

1. SQL Server Standard num VPS/VM mais sério.
2. Azure SQL/managed SQL se o custo compensar gestão reduzida.
3. Migrar para PostgreSQL antes de o custo/licenciamento SQL Server ficar estrutural.

### 100 lojas -> 1000 lojas

- Primeiro separar DB do VPS da app.
- Depois considerar read replica/managed DB se reads forem gargalo.
- Só considerar sharding por tenant quando houver sinais reais: DB enorme, tenants grandes ou isolamento comercial/regulatório.
- PostgreSQL deve ser decidido antes de dependermos de features SQL Server específicas demais.

## Optimizações por ROI

| Ordem | Optimização | ROI | Quando fazer |
|---:|---|---|---|
| 1 | Instrumentar p95, SQL duration e Query Store | Muito alto | Antes de mexer em performance. |
| 2 | Dashboard: agregações em SQL + DTOs pequenos | Muito alto | Sprint 1/2. |
| 3 | Projections em listas e detalhes | Alto | Sprint 2. |
| 4 | Índices compostos medidos | Alto | Depois de Query Store mostrar scans/lookups. |
| 5 | React Query keys/staleTime/invalidation por domínio | Alto | Sprint 2. |
| 6 | Redis para dashboard/tenant settings/portal público | Médio/alto | Depois das queries estarem decentes. |
| 7 | HTTP headers/ETags em settings e portal público | Médio | Sprint 3. |
| 8 | Lazy loading de rotas e charts | Médio | Quando build avisar bundle grande. |
| 9 | CSV streaming/batching | Médio | Antes de tenants com milhares de linhas. |
| 10 | MiniProfiler em dev/staging | Médio | Muito útil enquanto se mexe em EF. |
| 11 | Full-text/search column | Médio | Só quando `%LIKE%` ficar lento. |
| 12 | EF compiled queries | Baixo até prova contrária | Só após profiling. |
| 13 | Read replicas | Baixo agora | 100+ lojas e DB read-heavy. |

## Roadmap de implementação

### Sprint 1: baseline e visibilidade

- Adicionar métricas por endpoint: duração, status, rota, tenant, request id.
- Activar Query Store na DB de beta.
- Criar teste k6 simples para login, lista reparações, detalhe, dashboard e portal público.
- Adicionar MiniProfiler em Development/Staging, protegido.
- Documentar baseline inicial: p50/p95/p99 e top 10 queries.

Entregável: relatório pequeno `Contexto/performance-baseline-YYYY-MM-DD.md` ou secção em `19-Monitoring.md`.

### Sprint 2: EF Core + SQL

- Refactor dashboard para agregações em SQL.
- Substituir `Include` por projections nos endpoints de lista onde fizer sentido.
- Rever `FindByIdWithTimelineAsync`: limitar/projetar timeline se crescer.
- Criar 2-4 índices medidos, não uma árvore de Natal de índices.
- Validar planos antes/depois.

Entregável: PR com benchmarks antes/depois e plano de rollback das migrations de índice.

### Sprint 3: cache controlada

- Adicionar `IDistributedCache` com Redis.
- Implementar cache para tenant settings.
- Implementar cache dashboard com TTL curto + invalidação manual.
- Implementar cache portal público 30-120s.
- Adicionar ETag/`Cache-Control: private` em settings e portal público se o DTO permitir.
- Garantir prefixo `tenant:{tenantId}` em todas as chaves.

Entregável: cache hit ratio visível em logs/métricas.

### Sprint 4: frontend, CDN e carga

- Lazy load rotas pesadas.
- Rever nginx headers: assets long cache, `index.html` revalidate.
- Confirmar Cloudflare cache rules.
- Alinhar service worker PWA para app shell/assets, sem API auth genérica.
- Optimizar fotos antes/depois com thumbnails quando a feature existir.
- Correr k6 com cenário 10 lojas e depois 100 lojas simuladas.

Entregável: relatório com metas atingidas ou próximos gargalos.

## Riscos e mitigação

| Risco | Como acontece | Mitigação |
|---|---|---|
| Leak entre tenants por cache key errada | Chave `dashboard:summary` sem `tenantId`. | Prefixo obrigatório `tenant:{tenantId}` + testes unitários das chaves. |
| Dados stale no dashboard | TTL alto ou invalidação esquecida. | TTL 2-5 min + limpar em mutações óbvias + UI pode mostrar "actualizado há X min". |
| CDN cacheia API privada | Regra Cloudflare demasiado agressiva. | Bypass para `/api/*`; headers `private/no-store` por defeito. |
| Redis em baixo quebra API | Código assume cache sempre disponível. | Cache best-effort: fallback para DB e log warning. |
| Cache stampede | Vários requests recalculam dashboard ao mesmo tempo. | Lock leve por chave ou TTL com jitter; só se aparecer. |
| Índices demais atrasam writes | Cada insert/update mantém muitos índices. | Criar poucos índices medidos; rever `sys.dm_db_index_usage_stats`. |
| Missing index DMV engana | Sugestões duplicadas, sem filtered/unique, sem custo de includes. | Cruzar com Query Store e plano real. |
| Compiled queries complicam código | Optimização prematura em queries dinâmicas. | Só usar depois de profiling e query estável. |
| PII em logs/cache | Serializar DTO inteiro em log ou Redis. | Redacção de logs, TTL curto, payload mínimo. |
| Browser partilhado mostra dados antigos | Oficina usa tablet comum. | Logout limpa React Query/IndexedDB por tenant; APIs auth `no-store`. |

## Checklist pré-100 lojas

- [ ] Dashboard sem agregações em memória para períodos grandes.
- [ ] Query Store ligado e consultado semanalmente.
- [ ] Top 10 endpoints com p95 visível.
- [ ] Redis usado só em 3-5 caches claras.
- [ ] Todas as cache keys privadas incluem tenant.
- [ ] API autenticada não é cacheada por CDN.
- [ ] React Query limpa cache em logout/troca de tenant.
- [ ] Export CSV faz streaming ou batch.
- [ ] Import CSV tem limites, progresso e rollback/relatório de erro.
- [ ] Build frontend sem bundle inicial desnecessariamente grande.
- [ ] Teste k6 cobre lista, detalhe, dashboard, portal e import pequeno.

## Fontes verificadas

- Microsoft Learn — [Efficient Querying - EF Core](https://learn.microsoft.com/en-us/ef/core/performance/efficient-querying)
- Microsoft Learn — [Advanced Performance Topics - EF Core](https://learn.microsoft.com/en-us/ef/core/performance/advanced-performance-topics)
- Microsoft Learn — [Single vs. Split Queries - EF Core](https://learn.microsoft.com/en-us/ef/core/querying/single-split-queries)
- Microsoft Learn — [Output caching middleware in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/performance/caching/output?view=aspnetcore-10.0)
- Microsoft Learn — [Distributed caching in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/performance/caching/distributed?view=aspnetcore-10.0)
- Microsoft Learn — [Cache in-memory in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/performance/caching/memory?view=aspnetcore-10.0)
- Microsoft Learn — [Editions and supported features of SQL Server 2022](https://learn.microsoft.com/en-us/sql/sql-server/editions-and-components-of-sql-server-2022?view=sql-server-ver16)
- Microsoft Learn — [Tune nonclustered indexes with missing index suggestions](https://learn.microsoft.com/en-us/sql/relational-databases/indexes/tune-nonclustered-missing-index-suggestions?view=sql-server-ver17)
- Microsoft Learn — [Create filtered indexes](https://learn.microsoft.com/en-us/sql/relational-databases/indexes/create-filtered-indexes?view=sql-server-ver15)
- Microsoft Learn — [Monitor performance by using the Query Store](https://learn.microsoft.com/en-us/sql/relational-databases/performance/monitoring-performance-by-using-the-query-store?view=sql-server-ver16)
- OpenTelemetry — [.NET documentation](https://opentelemetry.io/docs/languages/dotnet/)
- MiniProfiler — [ASP.NET Core documentation](https://miniprofiler.com/dotnet/AspDotNetCore)
