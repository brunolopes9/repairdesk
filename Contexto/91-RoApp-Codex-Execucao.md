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

## Guardrails

- Nao copiar ROAPP horizontal demais. Mender deve continuar vertical para lojas/reparacao em Portugal.
- Evitar features de teatro: payroll, chamadas telefonicas e ads ficam para depois.
- Priorizar o que reduz trabalho diario no balcao e aumenta confianca do cliente.
