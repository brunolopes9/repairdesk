# RoAPP -> Mender: execucao Codex

Data: 2026-05-29

## Ja confirmado no codigo

- Sidebar/menu: ja foi trabalhado pelo Claude. Nao tocar sem nova decisao.
- Perfil do utilizador: existe em `/definicoes/perfil` com nome, telefone e alteracao de password.
- Stock take / inventario fisico: existe de ponta a ponta (`Sprint421_StockTake`).
- Tarefas internas: existe de ponta a ponta (`Sprint422_InternalTasks`).
- Agendamentos: ja existe semana/lista, criacao, estados e overlay de reparacoes com ETA.
- Pedidos online: existe base em `/pedidos-online` / repair requests.

## Implementado agora por Codex

### Agendamentos exportaveis para calendario

Objetivo ROAPP copiado: calendario integravel com ferramentas externas.

Implementacao:
- `GET /api/appointments/export.ics?from=...&to=...`
- Gera ficheiro iCalendar (`text/calendar`) com eventos reais.
- Frontend `/agendamentos` ganhou botao `Exportar calendario` para descarregar o intervalo visivel.
- O ficheiro pode ser importado em Google Calendar, Apple Calendar ou Outlook.

Validacao:
- `dotnet test backend/tests/RepairDesk.Tests/RepairDesk.Tests.csproj --filter AppointmentApiTests`
- `npm.cmd run build` em `frontend`

### Equipamentos do cliente

Objetivo ROAPP copiado: ficha do cliente com equipamento/historico reutilizavel.

Implementacao:
- `GET /api/clientes/{id}/equipamentos?take=20`
- Deriva equipamentos a partir de reparacoes e vendas com IMEI/serial, sem criar nova tabela.
- A ficha de cliente mostra equipamentos recentes com contadores de reparacoes/vendas.
- O modal `Nova reparacao` sugere equipamentos recentes do cliente e preenche equipamento + IMEI com um clique.

Validacao:
- `dotnet test backend/tests/RepairDesk.Tests/RepairDesk.Tests.csproj --filter ClientesApiTests`
- `npm.cmd run build` em `frontend`

### Pedidos online como mini-inbox de triagem

Objetivo ROAPP adaptado: "inquiries/chats" sem prometer integracao omnicanal falsa.

Implementacao:
- `/pedidos-online` ganhou contadores por estado: por tratar, convertidos, rejeitados.
- Cada pedido mostra acoes rapidas de contacto: ligar, WhatsApp com mensagem pre-preenchida e email.
- O telefone e formatado para leitura PT; WhatsApp normaliza 9 digitos para `351`.

Validacao:
- `npm.cmd run build` em `frontend`

### Pedidos online: UX de rejeicao + SLA 48h

Objetivo ROAPP adaptado: fazer a inbox comportar-se como uma fila operacional,
com menos prompts nativos e mais foco no que esta a envelhecer.

Implementacao:
- Rejeitar pedido usa modal proprio com contexto do pedido e motivo interno
  opcional, em vez de `window.prompt`.
- `/pedidos-online` mostra contador "Atrasados" para pedidos pendentes ha mais
  de 48h.
- Tab Pendentes ganhou filtro rapido "Atrasados 48h" para limpar follow-ups
  antigos antes que leads arrefecam.

Validacao:
- `npm.cmd run build` em `frontend`

### Pedidos online: follow-up/deadlines de leads

Objetivo ROAPP copiado/adaptado: "lead deadlines" para nao deixar pedidos
ficarem esquecidos depois de uma chamada, WhatsApp ou email.

Implementacao:
- `RepairRequest.FollowUpAt` + migration `Sprint448_RepairRequestFollowUpAt`.
- `PUT /api/repair-requests/{id}/triagem` guarda notas, prioridade e data de
  follow-up na mesma acao.
- `POST /api/repair-requests/manual` tambem aceita follow-up no momento em que
  o staff regista o lead offline.
- Ao converter em reparacao/trabalho ou rejeitar, o follow-up e limpo.
- `/pedidos-online` mostra card "Follow-up", badge no pedido, campo
  `datetime-local` na triagem inline e filtro rapido para follow-ups vencidos.

Validacao:
- `dotnet test backend/tests/RepairDesk.Tests/RepairDesk.Tests.csproj --filter RepairRequestsApiTests`
- `npm.cmd run build` em `frontend`

### Pedidos online: pesquisa por contacto/equipamento

Objetivo ROAPP copiado/adaptado: encontrar leads por telefone/nome rapidamente,
sem depender da pesquisa global.

Implementacao:
- `/pedidos-online` ganhou pesquisa local por nome, telefone, email,
  equipamento, avaria e canal.
- A pesquisa normaliza acentos e tambem compara apenas digitos no telefone, para
  encontrar `933 938 716` mesmo se o staff escrever `933938716`.

Validacao:
- `npm.cmd run build` em `frontend`

## Implementado depois pelo Claude (S436-S442)

Seguimento direto da sugestao do Codex: transformar /pedidos-online em
inbox/funil real. Stack contig em main `dev`:

- **S436** triagem inline: NotasInternas + Prioridade (Baixa/Normal/Alta/Urgente).
  PUT /repair-requests/{id}/triagem. Pendentes ordenam por prioridade desc.
- **S437** segundo caminho de conversao: POST /converter-em-trabalho. Cria
  Trabalho (status=Orcamento) em vez de Reparacao. Botoes "Reparacao" vs
  "Orcamento" no card. Para cliente que so quer estimativa.
- **S438** Origem enum: Widget (default)/Telefone/Email/WhatsApp/Balcao/Outro.
  Filtro dropdown na inbox. Badge inline "via Telefone".
- **S439** manual create: POST /repair-requests/manual + modal "+ Novo pedido"
  no header. Permite registar leads offline (telefone, balcao). Origem != Widget
  obrigatorio (esse e exclusivo do endpoint publico anonimo).
- **S440** tests para S436-S439: 6 novos (triagem, converter-em-trabalho,
  manual create, validacoes). 496/496 verde. Roles matrix snapshot
  actualizada (d115e549260ec748).
- **S441** cron ReadyForPickupHostedService: deteta reparacoes Estado=Pronto
  com EstadoSince > 5 dias (cliente nao veio buscar). Digest staff push +
  alerta no Dashboard. Pattern S392/S428/S430.
- **S442** breakdown "Por canal · 30d" no header da inbox. Faz S438 ganhar
  valor analitico sem backend extra (deriva de dados ja carregados).

Inbox agora cobre o funil completo: lead chega (widget OU manual) -> triagem
(prioridade + notas) -> converter em Reparacao OU Orcamento, ou rejeitar com
motivo. Origem permite identificar canais que rendem; cron alerta quando
clientes nao vem buscar.

### Calendar v2 + UX menu (S443-S447)

- **S443** Calendar feed token + endpoints + UI (subscricao Google/Apple Cal).
  Tenant.CalendarFeedToken (32 chars rotavel), GET /api/automacoes/calendar-feed
  + POST regenerate (Admin), GET /api/public/calendar-feed/{token}.ics
  (AllowAnonymous, rate-limited). Subscricao refresca sozinha (~12h Google,
  ~5min Apple).
- **S444** Sidebar submenus colapsaveis (localStorage rd.nav.openParents.v1).
  Bruno feedback: "Balcao tinha 3 submenus sempre abertos a ocupar espaco".
- **S445** Fix critico do menu: /stock e /produtos estavam orfaos desde S388
  (saíram do menu mas Bruno precisava de aceder direto). Solução: parent
  "Catálogo & Stock" colapsável com Visão geral + Stock + Produtos + Contagens
  físicas (Inventário renomeado para distinguir de Stock).
- **S446** Calendar feed inclui reparações com PrevistoEntregueEm — Bruno
  passa a ver no Google Cal agendamentos + telemóveis a entregar juntos.
- **S447** 6 tests para calendar feed (502/502 verde).

## Proximas features ROAPP que valem a pena

1. Inbox omnicanal
   - Comecar com "Conversas" simples: email recebido, WhatsApp manual/link, notas de chamada.
   - Depois ligar Instagram/Messenger/Telegram quando houver credenciais reais.
   - Nao fazer integracoes falsas so para parecer completo.
   - Primeiro passo ja feito: `/pedidos-online` como mini-inbox operacional.

2. Funil de pedidos
   - Pedido online -> triagem -> orcamento -> reparacao.
   - Hoje ja existe base; falta transformar em cockpit operacional.

3. Calendario v2
   - URL privado/subscricao `.ics` por tenant para subscrever no Google Calendar.
   - Lembretes por email/push/WhatsApp antes da marcacao.

4. Templates de impressao/documentos
   - Entrada de equipamento, recibo de entrega, etiquetas, termos de garantia.
   - ROAPP e forte nisto; Mender pode ganhar por ter Moloni/AT/RGPD melhor.

## Sessão 2026-05-30 — Loops Comunicações + Devices (S450-S473, 25 sprints)

Dois loops completos sobre a fundação do Doc 91:

### Loop A — Comunicações cliente (Doc 91 ponto 1, S452-S460 + S471)
- **S450/S451** PDFs Comprovativo entrada + Recibo entrega (ponto 4)
- **S452** Entity `ReparacaoComunicacao` (Tipo Nota/Telefone/WhatsApp/Email/SMS/Visita
  + Direção Inbound/Outbound/Interna + Texto) com endpoint nested
  `/api/reparacoes/{id}/comunicacoes`
- **S453** Vista agregada `/api/clientes/{id}/comunicacoes` + secção na ficha cliente
- **S454** (Codex paralelo) Follow-up date + pesquisa local em PedidosOnline
- **S455** 6 tests (CRUD + tenant isolation + validação)
- **S456/S457** CTAs WhatsApp contextuais por estado (Diag azul, AP âmbar, Pronto verde):
  click abre `wa.me/?text=` pré-preenchido + cria Outbound automaticamente
- **S458** Cron `ClienteNotificarPendingHostedService` (24h, 8h threshold): push
  staff quando reparações em estado comunicável sem Outbound desde EstadoSince
- **S459** CTAs usam templates do tenant (`TenantPreferences.Communication.
  TemplatesByState` — S398) em vez de hardcoded. Placeholders substituídos
- **S460** Widget Dashboard "X clientes a avisar" (reuso lógica S458)
- **S471** Email CTA (par do WhatsApp via `mailto:`) usando mesmo template

### Loop B — Device asset registry (Doc 90 Tier 2 #6, S461-S473 sem 470)
- **S461** Entity `Device` persistente: TenantId+ClienteId+Tipo+Marca/Modelo+
  Apelido+IMEI+Serial+Cor+DataAquisicao+GarantiaFabricanteUntil+Arquivado.
  IMEI único per-tenant. CRUD endpoint
- **S462** UI gestão na ficha cliente (grid cards, modal create/edit, badge
  Shield ✓/✗ por validade)
- **S463** 9 tests (CRUD + IMEI duplicado 422 + cliente 404 + tenant isolation)
- **S464** Endpoint `GET /api/devices/by-imei/{imei}` (204 inexistente, 200 com
  ClienteNome) para auto-link
- **S465** Banner azul no modal Nova reparação: "Este IMEI é do {Apelido} de
  {Cliente}" + botão pré-preenche cliente
- **S466** Secção "Outros equipamentos do cliente" no ReparacaoDetalhe
- **S467** Widget Dashboard purple "X garantias fabricante a expirar (30d)" —
  cross-sell oportunidade (vender garantia loja antes da fabricante acabar)
- **S468** Cron `DeviceGarantiaFabricanteExpiryHostedService` + 3 tests endpoint
- **S469** Devices REGISTADOS aparecem primeiro no autocomplete do modal
  (dedupe por IMEI vs derived de reparações/vendas)
- **S470** 6 KPIs no header cliente (era 4): + Equipamentos + Contactos 30d
- **S472** Footer brand no DeviceCard: "🔧 N reparações com este IMEI" + link
  "ver última" (reuso S65 historicoImei)
- **S473** Banner azul "Registar este equipamento" no ReparacaoDetalhe quando
  IMEI não tem Device match. Heurística marca/modelo do equipamento

**Link bidireccional Reparação↔Device descobrível em 5 sítios:** ficha cliente
(gestão), detalhe reparação (outros equipamentos + registar), modal Nova
reparação (banner IMEI + autocomplete), Dashboard (cross-sell garantia).

### Saúde técnica
- **535/535 tests verde** (528 pré + S455:6 + S463:9 + S468:3)
- Frontend build verde em todos os commits
- Roles matrix snapshot atualizado 3× (9f1bcd → d0397b → 63b91bc9 → 9f36bdc7 →
  e703c47b por DeviceController, by-imei, devices-garantia-a-expirar)
- Patterns reusados: HostedService cron (5× now: S392/S428/S441/S458/S468),
  audit log, IClassFixture EF + Bearer

### Pendente próximo
- Push origin (94+ commits ahead — Bruno aguarda confirmação)
- Vitest setup frontend (testes UI) — futuro
- Doc 90 Tier 3 (Chats omnichannel real, AI replies) — fora scope hoje

## Sessão 2026-05-31 — Clientes: preferências de contacto e consentimento (S479)

Feature ROAPP-inspired implementada pelo Codex para preparar CRM/omnichannel sem
fazer integrações falsas:

- **S479** `Cliente` ganhou `ContactoPreferido` (`Telefone`, `WhatsApp`, `Email`,
  `Sms`), `AceitaMarketing` e `NaoContactar`.
- API cria/edita/lista/exporta/importa CSV estes campos. `NaoContactar=true`
  força `AceitaMarketing=false`.
- UI Clientes: formulário com seleção de canal preferido + consentimento, KPIs
  "Marketing OK" e "Não contactar", filtros rápidos, badges na lista, inspector
  e ficha do cliente.
- Migration `Sprint479_ClienteContactPreferencesAndReparacaoCategoria` adiciona
  estes campos em `Clientes` e também materializa `Reparacoes.Categoria`, que já
  existia no modelo mas estava pendente no snapshot/migration drift de S475.
- Teste novo `ContactPreferences_CreateUpdate_PersistemNoDto`.
- Validação: `dotnet test` backend **536/536 verde** + `npm run build` frontend
  verde.

## Sessao 2026-06-01 - Clientes: etiquetas e segmentos CRM (S480)

Feature ROAPP-inspired implementada pelo Codex para transformar Clientes de uma
lista simples numa base CRM segmentavel:

- **S480** entidades `ClienteTag` e `ClienteTagAssignment` com `TenantId`, nome e
  cor por etiqueta. Migration `Sprint480_ClienteTags`.
- API nova `/api/cliente-tags`: listar para users autenticados; criar/editar/apagar
  apenas Admin. Novo `PUT /api/clientes/{id}/tags` para associar etiquetas.
- `/api/clientes` aceita `tagId` para filtrar segmentos. `ClienteDto` inclui
  `Tags` para lista, detalhe e inspector.
- UI Clientes: chips de etiquetas na tabela e ficha, filtros rapidos por etiqueta,
  editor "Gerir" no inspector lateral, e criacao inline de novas etiquetas como
  `VIP`, `Empresa`, `Lead online`, etc.
- Teste novo `ClienteTags_CreateAssignAndFilter_ReturnsTaggedCliente`.
- Matriz de roles atualizada para `ClienteTagsController` e `ClientesController.SetTags`.
- Validacao: `dotnet test` backend **538/538 verde** + `npm run build` frontend
  verde.

## Guardrails

- Nao copiar ROAPP horizontal demais. Mender deve continuar vertical para lojas/reparacao em Portugal.
- Evitar features de teatro: payroll, chamadas telefonicas e ads ficam para depois.
- Priorizar o que reduz trabalho diario no balcao e aumenta confianca do cliente.
