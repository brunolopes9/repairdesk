# Monitoring + Alertas + Observabilidade

Atualizado: 2026-05-16

Objetivo: monitorização mínima viável para RepairDesk SaaS B2B, com alertas acionáveis, baixo custo e baixa manutenção para fundador solo.

Estado atual observado no código:

- Backend `.NET 10`.
- Serilog já configurado em `RepairDesk.API/Program.cs`.
- Sinks atuais: console + ficheiro diário.
- Endpoint simples: `GET /api/health`.
- Sem APM.
- Sem alertas.
- Sem dashboard público.
- Sem tracking estruturado de eventos de negócio.

## Conclusão executiva

Stack recomendada:

1. **Sentry** para erros backend/frontend, stack traces, releases e alertas de exceções.
2. **Better Stack** para uptime, status page, heartbeats de backups/jobs e logs leves.
3. **OpenTelemetry + Grafana Cloud** só na Fase 2, quando houver tráfego real e necessidade de p95/p99/SQL traces.

Não começar com Datadog/NewRelic. Não começar self-hosting Prometheus/Grafana/Loki só para parecer profissional. Para Bruno solo, a regra é: **alertas simples, poucos e que obriguem ação clara**.

Primeiro dia de implementação:

- Integrar Sentry no backend e frontend.
- Criar monitor Better Stack para API `/api/health` e frontend.
- Criar status page `status.repairdesk.pt`.
- Testar alerta manual.

## 1. O que monitorizar

Critério: se isto está mal, algo está partido ou vai partir em breve.

| Área | Métrica | Threshold inicial | Severidade | Ação |
|---|---|---:|---|---|
| Uptime API | `GET /api/health` responde 200 | falha 2 checks seguidos | P1 | Confirmar deploy/DB/container. |
| Uptime Web | homepage/app responde 200 | falha 2 checks seguidos | P1 | Confirmar nginx/frontend/CDN. |
| Error rate | 5xx por minuto | >5% durante 5 min ou >10 erros/5 min | P1/P2 | Ver Sentry + logs. |
| Exceptions novas | erro novo em produção | 1 evento novo | P2 | Triage no mesmo dia. |
| Exceptions críticas | auth, pagamento, perda de dados, tenant leak | 1 evento | P1 | Parar e corrigir. |
| Response time API | p95 | >1s durante 10 min | P2 | Ver endpoints lentos. |
| Response time API | p99 | >3s durante 10 min | P2 | Investigar query/infra. |
| SQL slow query | duração | >500ms em endpoint comum | P2 | Index/query review. |
| DB health | conexão falha | qualquer falha em health check | P1 | DB indisponível. |
| DB size | crescimento | >80% disco disponível | P2 | Limpar/logs/aumentar disco. |
| Deadlocks | SQL deadlock | >=1/dia | P2 | Rever transações. |
| Containers CPU | CPU média | >80% 15 min | P2 | Ver processo/deploy. |
| Containers RAM | memória | >80% 15 min ou OOM | P1/P2 | Ver leak/restart. |
| Disco | uso | >80% aviso, >90% crítico | P2/P1 | Limpar logs/backups. |
| Backup | último backup OK | >24h sem sucesso | P1 | Fazer backup manual. |
| SSL | expiração | <14 dias | P2 | Renovar certificado. |
| Signup | novo tenant | evento | P3 | Ver onboarding. |
| Ativação | primeira reparação | <24h após signup | P3 | Medir activation. |
| Churn | conta cancela/inativa | evento | P3 | Contactar/perceber razão. |

## 2. Comparação de ferramentas

### Tabela

| Ferramenta | Faz bem | Free/cheap tier | Limitações | Veredicto RepairDesk |
|---|---|---|---|---|
| Sentry | Erros, stack traces, frontend, releases, performance básica | Free ~5k errors/events/mês; Team ~26 USD/mês | Quota pode evaporar com bug em loop | Usar já. |
| Better Stack | Uptime, status page, logs, heartbeats, incidentes | Free: 10 monitors/heartbeats, 1 status page, logs limitados; Team ~29-34 USD/mês | Logs/telemetry avançada pode subir custo | Usar já. |
| UptimeRobot | Uptime simples | Free/paid barato; free pode ter restrição comercial; Solo ~8-10 USD/mês | Menos all-in-one, status/alertas menos bons | Alternativa se Better Stack não agradar. |
| Pingdom | Synthetic/RUM maduro | Pago, trial | Mais caro e enterprise-ish | Não começar aqui. |
| OpenTelemetry -> Grafana Cloud | Métricas/traces/logs vendor-neutral | Free generoso: métricas/logs/traces limitados; Pro ~19 USD/mês + uso | Mais setup e ruído | Fase 2. |
| OpenTelemetry -> Honeycomb | Tracing excelente | Free até grande volume de eventos; Pro ~130 USD/mês | Mais orientado a tracing/eventos, caro quando paga | Alternativa avançada. |
| SigNoz | Open-source/self-host ou cloud | Self-host possível | Manutenção e infra | Não para Bruno solo cedo. |
| Application Insights | APM nativo se hospedar em Azure | Pay-as-you-go; bom em Azure | Custo por ingestão/logs, menos simples fora Azure | Usar só se deploy for Azure. |
| Plausible/Umami self-host | Analytics frontend privacidade | Self-host grátis | Mais uma app para manter | Adiar; usar eventos internos primeiro. |

### Sentry

Usar para:

- Exceptions backend.
- Erros React frontend.
- Release tracking.
- Alertas de erro novo.
- Breadcrumbs de request/rota.

Não usar inicialmente para:

- Capturar 100% de performance transactions.
- Session replay sempre ligado.
- Logs de tudo.

Configuração inicial:

- Backend: capturar `Error` e `Fatal`.
- Frontend: capturar exceptions e unhandled promise rejections.
- `TracesSampleRate`: 0.05 ou 0 no free tier.
- `ProfilesSampleRate`: 0.
- PII desligado; não enviar passwords, tokens, IMEI, notas sensíveis.

### Better Stack

Usar para:

- Monitor API `/api/health`.
- Monitor frontend.
- SSL monitor.
- Heartbeat de backup diário.
- Status page pública.
- Logs leves de produção, se couber no free tier.

Porquê:

- Faz 3 coisas necessárias numa só ferramenta: uptime + status page + heartbeats/logs.
- Free tier chega para 0-10 lojas.
- Melhor que montar UptimeRobot + Statuspage + cron monitor separados.

### UptimeRobot

Bom se:

- Só quiseres uptime barato.
- Não quiseres logs/status/incident management.

Problema:

- Free tier pode não ser adequado para uso comercial, conforme mudanças recentes.
- Mais uma ferramenta se já houver Better Stack.

### OpenTelemetry + Grafana Cloud

Entrar quando:

- Já houver 5-10 lojas ativas.
- Houver problemas reais de performance.
- Precisares de p95/p99 por endpoint e traces SQL.

Não entrar no dia 1 se isto atrasar beta. O stack mínimo primeiro deve dizer: "está em baixo?", "deu erro?", "backup correu?", "certificado vai expirar?".

### Application Insights

Bom se:

- O hosting for Azure App Service/Container Apps/SQL Azure.
- Quiseres integração nativa com Azure Monitor.

Risco:

- Custo por ingestão.
- É fácil mandar demasiados traces/logs e pagar por ruído.

Decisão:

- Não usar como padrão agora, a menos que o RepairDesk vá mesmo para Azure.

## 3. Stack recomendada por fase

### Fase 0 - agora, 0-10 lojas

| Necessidade | Ferramenta | Custo esperado |
|---|---|---:|
| Erros backend/frontend | Sentry Developer | 0 USD |
| Uptime + status + backup heartbeat | Better Stack Free | 0 USD |
| Logs locais | Serilog file + Docker logs | 0 EUR |
| Analytics negócio | tabela/eventos internos no DB | 0 EUR |

Custo: **0 EUR/mês**.

Nota: se o free tier do Sentry estourar por erro em loop, corrigir sampling/filtros antes de pagar.

### Fase 1 - beta pública, 10-50 lojas

| Necessidade | Ferramenta | Custo esperado |
|---|---|---:|
| Erros com equipa/mais quota | Sentry Team | ~26 USD/mês |
| Uptime/status/telefone opcional | Better Stack Team | ~29-34 USD/mês |
| Logs centralizados | Better Stack logs, retenção curta | incluído/uso |
| Métricas técnicas básicas | Better Stack/Grafana se necessário | 0-19 USD/mês |

Custo: **55-80 USD/mês**.

### Fase 2 - 50-100+ lojas

| Necessidade | Ferramenta | Custo esperado |
|---|---|---:|
| APM/tracing real | OpenTelemetry -> Grafana Cloud | 0-50 USD/mês inicial |
| Error tracking | Sentry Team/Business | 26-80 USD/mês |
| Incident/status | Better Stack Team | 29-34 USD/mês |
| Logs | Better Stack ou Grafana Loki | conforme GB |

Custo: **80-180 USD/mês**, dependendo de volume.

## 4. Alertas e severidade

### Canais

| Severidade | Canal | Acorda Bruno? | Exemplos |
|---|---|---|---|
| P1 | SMS/telefonema Better Stack + email | Sim | API down, DB down, backup falhou >24h, tenant leak, perda de dados. |
| P2 | Telegram/Discord/Slack privado + email | Não, salvo horário útil | erro novo, p95 alto, SSL <14 dias, disco >80%. |
| P3 | Email digest diário/semanal | Não | signup, primeira reparação, loja inativa, churn. |

Recomendação para Bruno:

- Criar um **Telegram privado** ou **Discord privado** só para alertas RepairDesk.
- P1 por Better Stack call/SMS quando houver clientes pagantes.
- Antes de clientes pagantes: P1 por email + Telegram chega.

### Definição operacional

P1:

- Cliente não consegue usar o sistema.
- Dados podem estar incorretos/perdidos.
- Segurança ou tenant isolation em risco.
- Backup não existe há mais de 24h.

P2:

- Sistema funciona, mas há degradação.
- Erro novo que afeta fluxo importante.
- Performance má.
- Disco/certificado/infra em risco.

P3:

- Informação útil para negócio.
- Não exige ação imediata.
- Pode ir para digest.

### Anti-spam

Regras:

- Agrupar alertas por causa: "API down" não deve mandar 20 emails.
- Snooze durante deploy planeado.
- Só alertar P1 depois de 2 checks falhados.
- Para Sentry, alertar por **issue nova**, não por cada evento.
- Rate limit de alertas: máximo 1 P2 por issue por hora.
- Digest diário para P3.

## 5. Integração .NET

### Packages NuGet - Fase 0

```bash
dotnet add src/RepairDesk.API/RepairDesk.API.csproj package Sentry.AspNetCore
dotnet add src/RepairDesk.API/RepairDesk.API.csproj package Sentry.Serilog
```

Opcional para health checks reais:

```bash
dotnet add src/RepairDesk.API/RepairDesk.API.csproj package AspNetCore.HealthChecks.SqlServer
```

### Packages NuGet - Fase 2 OpenTelemetry

```bash
dotnet add src/RepairDesk.API/RepairDesk.API.csproj package OpenTelemetry.Extensions.Hosting
dotnet add src/RepairDesk.API/RepairDesk.API.csproj package OpenTelemetry.Exporter.OpenTelemetryProtocol
dotnet add src/RepairDesk.API/RepairDesk.API.csproj package OpenTelemetry.Instrumentation.AspNetCore
dotnet add src/RepairDesk.API/RepairDesk.API.csproj package OpenTelemetry.Instrumentation.Http
dotnet add src/RepairDesk.API/RepairDesk.API.csproj package OpenTelemetry.Instrumentation.SqlClient
```

### appsettings.json - Sentry

```json
{
  "Sentry": {
    "Dsn": "",
    "Environment": "production",
    "SendDefaultPii": false,
    "TracesSampleRate": 0.05,
    "MaxRequestBodySize": "Small",
    "AttachStacktrace": true,
    "Debug": false
  },
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft.AspNetCore": "Warning",
        "Microsoft.EntityFrameworkCore": "Warning"
      }
    }
  }
}
```

Em produção, usar env var:

```powershell
$env:Sentry__Dsn="https://..."
$env:Sentry__Environment="production"
```

### Program.cs - Sentry

Adicionar no `builder`:

```csharp
builder.WebHost.UseSentry();
```

E no Serilog:

```csharp
.WriteTo.Sentry(o =>
{
    o.MinimumBreadcrumbLevel = Serilog.Events.LogEventLevel.Information;
    o.MinimumEventLevel = Serilog.Events.LogEventLevel.Error;
})
```

Nota: inicializar Sentry uma vez. Se usar `builder.WebHost.UseSentry()`, configurar o sink Serilog para complementar com breadcrumbs/eventos, sem duplicar ruído.

### Health checks reais

O endpoint atual `/api/health` diz que o processo responde, mas não prova que DB está OK.

Adicionar health checks:

```csharp
builder.Services.AddHealthChecks()
    .AddSqlServer(
        builder.Configuration.GetConnectionString("Default")!,
        name: "sqlserver",
        timeout: TimeSpan.FromSeconds(3));

app.MapHealthChecks("/api/health/live");
app.MapHealthChecks("/api/health/ready");
```

Política:

- `/live`: processo está vivo, sem DB. Usado pelo orchestrator.
- `/ready`: API + DB + dependências críticas. Usado por Better Stack.

### OpenTelemetry - Fase 2

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddSqlClientInstrumentation(options =>
        {
            options.SetDbStatementForText = false; // evitar PII/queries completas
            options.RecordException = true;
        })
        .AddOtlpExporter())
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddRuntimeInstrumentation()
        .AddOtlpExporter());
```

Env vars:

```powershell
$env:OTEL_SERVICE_NAME="repairdesk-api"
$env:OTEL_EXPORTER_OTLP_ENDPOINT="https://otlp-gateway-prod-eu-west-0.grafana.net/otlp"
$env:OTEL_EXPORTER_OTLP_HEADERS="Authorization=Basic ..."
```

### Better Stack logs

Opção simples:

- Manter Serilog file/Docker logs.
- Enviar logs via collector/agent do host.

Opção app-level:

- Usar sink HTTP/Logtail se estiver estável para .NET 10.
- Filtrar para `Warning+` em produção para não gastar ingestão.

Regra: não enviar logs `Information` de todos os requests para cloud enquanto o produto está pequeno. Uptime + Sentry resolvem 80%.

## 6. Frontend - Sentry Browser SDK

Package:

```bash
npm install @sentry/react
```

Config em `src/main.tsx`:

```ts
import * as Sentry from '@sentry/react';

Sentry.init({
  dsn: import.meta.env.VITE_SENTRY_DSN,
  environment: import.meta.env.MODE,
  tracesSampleRate: 0.05,
  sendDefaultPii: false,
});
```

Env:

```text
VITE_SENTRY_DSN=https://...
```

Não capturar:

- JWT.
- dados completos de cliente;
- IMEI;
- passwords/PINs;
- notas livres da reparação.

## 7. Dashboard de saúde pública

Recomendação: **comprar, não construir**.

Usar Better Stack status page:

- `status.repairdesk.pt`
- Componentes:
  - Web App
  - API
  - Base de dados
  - Portal cliente
  - WhatsApp notifications, quando existir
  - Backups

Porquê não construir:

- Uma status page própria pode cair junto com o sistema principal.
- Better Stack já liga monitors a incidentes.
- Clientes B2B gostam de transparência.

Texto de incidentes:

- curto;
- sem culpar provider;
- com ETA quando houver;
- post-mortem simples se >30 min ou dados afetados.

## 8. Métricas de negócio

Isto não deve ir para Sentry. Deve ir para DB/admin dashboard interno.

| Métrica | Definição | Frequência |
|---|---|---|
| Reparações criadas/dia | count por tenant/dia | diário |
| Lojas ativas | login ou reparação nos últimos 7 dias | semanal |
| Activation rate | tenant criou primeira reparação <24h após signup | semanal |
| Tempo Recebido -> Entregue | média e p90 | semanal |
| Orçamentos aprovados | approvals / orçamentos enviados | semanal |
| Receita MRR | subscrições ativas | mensal |
| Churn | cancelamentos ou 30 dias inativo | mensal |
| Uso por feature | portal cliente, PDF, WhatsApp, dashboard | semanal |

Eventos críticos de produto:

- `TenantSignedUp`
- `FirstRepairCreated`
- `FirstQuotePdfGenerated`
- `FirstPublicPortalOpened`
- `RepairDelivered`
- `SubscriptionCancelled`

Implementação inicial:

- Tabela `BusinessEvents`.
- Job semanal que agrega e envia email para Bruno.
- Mais tarde, dashboard interno `/admin/metrics`.

## 9. Alertas concretos

### P1

| Alerta | Condição | Canal | Deduplicate |
|---|---|---|---|
| API down | `/api/health/ready` falha 2 checks | Telegram/email; SMS quando pago | 15 min |
| Web down | frontend 5xx/timeout 2 checks | Telegram/email; SMS quando pago | 15 min |
| DB down | health SQL falha | Telegram/email; SMS quando pago | 15 min |
| Backup failed | heartbeat ausente >24h | Telegram/email; SMS quando pago | 1h |
| Disk critical | disco >90% | Telegram/email; SMS quando pago | 1h |
| Security/tenant leak | exception/tag manual | SMS/call | imediato |

### P2

| Alerta | Condição | Canal | Deduplicate |
|---|---|---|---|
| New backend issue | nova issue Sentry backend | Email/Telegram | 1h |
| New frontend issue | nova issue Sentry frontend | Email/Telegram | 1h |
| High 5xx | >5% requests 5xx/5min | Email/Telegram | 15 min |
| Slow API | p95 >1s por 10 min | Email/Telegram | 1h |
| Slow SQL | query >500ms recorrente | Email/Telegram | 1h |
| SSL expiring | <14 dias | Email | 24h |
| Disk warning | disco >80% | Email | 24h |

### P3

| Alerta | Condição | Canal |
|---|---|---|
| Novo signup | tenant criado | digest diário |
| Primeiro uso | primeira reparação | digest diário |
| Loja inativa | 7 dias sem login/reparação | digest semanal |
| Churn | cancelamento/inatividade 30d | digest semanal |
| MRR alterado | subscrição muda | digest semanal |

## 10. Checklist de implementação em 1 dia

### Hora 0-1

- Criar conta Sentry.
- Criar projeto `repairdesk-api`.
- Criar projeto `repairdesk-frontend`.
- Definir environment `production`.

### Hora 1-2

- Instalar `Sentry.AspNetCore` e `Sentry.Serilog`.
- Configurar DSN via env var.
- Criar endpoint temporário de teste só em dev/staging, ou lançar exception controlada localmente.

### Hora 2-3

- Instalar `@sentry/react`.
- Configurar `VITE_SENTRY_DSN`.
- Testar erro frontend.

### Hora 3-4

- Criar conta Better Stack.
- Monitor API `/api/health`.
- Monitor frontend.
- Configurar alerta email/Telegram.

### Hora 4-5

- Criar status page.
- Apontar CNAME `status.repairdesk.pt`.
- Adicionar componentes API/Web/DB/Backups.

### Hora 5-6

- Criar heartbeat `backup-daily`.
- No script de backup, chamar URL do heartbeat quando backup terminar OK.

### Hora 6-8

- Documentar runbook P1.
- Testar alerta: desligar temporariamente monitor de teste ou apontar para endpoint falso.
- Confirmar que Bruno recebe alerta em <1h.

## 11. Checklist semanal/mensal

### Semanal, 20 minutos

1. Ver Sentry issues novas.
2. Fechar issues resolvidas.
3. Ver top 5 endpoints lentos, se houver APM.
4. Confirmar backups dos últimos 7 dias.
5. Confirmar uptime semanal.
6. Ver lojas ativas e primeiras reparações.

### Mensal, 45 minutos

1. Rever custos Sentry/Better Stack/Grafana.
2. Rever quotas e sampling.
3. Testar restore de backup.
4. Ver crescimento da base de dados.
5. Rever incidentes do mês.
6. Atualizar runbook se algo correu mal.

## 12. Runbook P1 curto

Quando alerta P1 dispara:

1. Confirmar se é real: abrir API health, app e status Better Stack.
2. Ver último deploy.
3. Ver Sentry issues recentes.
4. Ver logs dos últimos 15 minutos.
5. Ver DB/container/disk.
6. Se clientes pagantes afetados por >10 min, atualizar status page.
7. Corrigir ou rollback.
8. Depois: escrever nota curta do que aconteceu.

Mensagem pública mínima:

```text
Estamos a investigar instabilidade no RepairDesk. Acompanhamento em curso.
```

Após resolução:

```text
Serviço normalizado. A causa foi identificada e estamos a aplicar medidas para evitar repetição.
```

## 13. Custos estimados

| Fase | Sentry | Better Stack | Grafana/OTel | Total |
|---|---:|---:|---:|---:|
| 0-10 lojas | 0 USD | 0 USD | 0 USD | 0 USD |
| 10-50 lojas | 0-26 USD | 0-34 USD | 0 USD | 0-60 USD |
| 50-100 lojas | 26-80 USD | 29-34 USD | 0-50 USD | 55-164 USD |

Regra de upgrade:

- Pagar Sentry quando free tier esconder erros ou precisares de mais utilizadores.
- Pagar Better Stack quando precisares de call/SMS, mais monitors, workflows ou retenção maior.
- Pagar Grafana quando performance/SQL traces forem uma dor real.

## 14. Fontes

- Sentry Serilog docs: https://docs.sentry.io/platforms/dotnet/guides/serilog/compatibility/
- Better Stack pricing: https://betterstack.com/pricing
- UptimeRobot pricing: https://uptimerobot.com/pricing/
- Grafana pricing: https://grafana.com/pricing/
- Honeycomb pricing: https://www.honeycomb.io/pricing/
- Azure Monitor cost model: https://learn.microsoft.com/en-us/azure/azure-monitor/cost-usage
- Azure Monitor pricing: https://azure.microsoft.com/en-us/pricing/details/monitor/
- OpenTelemetry .NET instrumentations: https://opentelemetry.io/docs/zero-code/dotnet/instrumentations/
- NuGet OpenTelemetry SqlClient instrumentation: https://www.nuget.org/packages/OpenTelemetry.Instrumentation.SqlClient

## 15. Decisão final

Implementar agora:

- Sentry backend.
- Sentry frontend.
- Better Stack uptime API/web.
- Better Stack heartbeat backups.
- Better Stack status page.
- Health check `/live` e `/ready`.

Adiar:

- OpenTelemetry completo.
- Grafana dashboards complexos.
- Self-hosted observability.
- Plausible/Umami, até haver website público e tráfego real.

Critério de sucesso: Bruno recebe um alerta de erro teste em menos de 1h e sabe responder sem pensar: P1 agora, P2 hoje, P3 amanhã/semana.
