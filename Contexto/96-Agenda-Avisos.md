# 96 — Agenda: marcar rápido e avisos no telemóvel (Sprint 611, nov/2026)

## Porquê
O Bruno marca serviços por telefone/mensagem («Leonardo — PS5, hoje às 18h», «Ricardo, quarta 10h»). O módulo de agendamentos (Sprint 371/418/443) já tinha semana, lista, estados, reagendar e feed para Google/Apple Calendar, mas:
- o formulário tinha 6 campos e um seletor de data/hora lento → risco de desistir de usar;
- não havia nenhum aviso — era preciso abrir o Mender para saber o que vinha a seguir;
- o Dashboard não mostrava a agenda.

## O que mudou
- **Marcar rápido** (`pages/agendamentos/MarcarRapido.tsx`): nome + «o quê» + um toque no dia (Hoje, Amanhã, próximos 5 dias, outro) + um toque na hora (09:00–19:00, outra). Telefone, equipamento e duração (60 min por defeito) em «Mais detalhes». Abre como folha de baixo no telemóvel. Substitui o formulário antigo.
- **Botão + fixo** na página Agendamentos no telemóvel; no telemóvel a página abre na **Lista** (a grelha de 7 colunas não cabe).
- **Cartão «Agenda» no Dashboard** (`AgendaProximos.tsx`): hoje e próximos 7 dias (agendados/confirmados), com «Marcar».
- **Avisos no telemóvel** (push de staff, `AgendaAvisosService` + `AgendaAvisosHostedService`, de minuto a minuto):
  - lembrete **~1 hora antes** de cada agendamento — «⏰ 18:00 Leonardo — daqui a 45 min»;
  - **resumo do dia às 08:00** (Lisboa) se houver agendamentos nesse dia;
  - **resumo da semana à segunda às 08:00** se houver agendamentos na semana.

## Regras e decisões
- Só agendamentos **Agendado/Confirmado** entram (cancelados e «não compareceu» não avisam).
- Lembrete uma única vez: `Appointment.LembreteEnviadoEm`. **Reagendar limpa** o campo → novo lembrete à nova hora. Agendamentos começados há mais de 5 min não são avisados (servidor parado).
- Resumos uma única vez por empresa: tabela `AgendaAvisos` (chave `dia:AAAA-MM-DD` / `semana:AAAA-Www`, índice único `TenantId+Chave`) — reinícios do servidor não repetem nem saltam o resumo (o antigo padrão «de 24 em 24 h desde o arranque» repetia/saltava).
- Se o servidor estiver parado às 08:00, o resumo ainda sai até às 10:00; depois já não.
- Sem agendamentos no dia/semana → sem notificação (sem ruído).
- O trabalho corre fora de pedidos (sem empresa ativa): o repositório ignora os filtros de tenant e filtra à mão.
- Config: `Agenda:Avisos:Enabled` (default true).

## Fora deste sprint (possível a seguir)
- Escolher a antecedência do lembrete por utilizador.
- Lembrete por SMS/WhatsApp ao cliente (precisa de consentimento e de um fornecedor).
- Vista de dia no telemóvel com linha do tempo.
