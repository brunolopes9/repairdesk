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
