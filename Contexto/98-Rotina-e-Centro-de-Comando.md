# 98 — Rotina, tarefas, finanças e trading: o Mender como centro de comando (11/11/2026)

Pedido do Bruno (11/11): tudo num só sítio, com avisos que **insistem**. O ponto de partida é a rotina diária. Este documento junta:
- o plano do Claude do Mender;
- as ideias do ChatGPT que foram aproveitadas;
- as regras fiscais acordadas com o Claude fiscal («bruno-trader-impostos-5a»).

## 1. Problema
O Bruno tem muitas frentes ao mesmo tempo: trading, LopesTech (reparações), software/Mender, conteúdo, Cortex, ginásio, alimentação e meditação. Sem blocos nem limites, o dia perde-se:
- uma ideia leva a horas no Claude;
- falha o ginásio e as refeições;
- fica no PC depois das 18:00, com stress e mau sono.

O Mender não deve servir para fazer mais coisas. Deve **dizer qual é a próxima coisa** e criar fricção para quebrar o plano.

## 2. Fases
| Fase | O quê | Estado |
|---|---|---|
| **F1 — Rotina (Sprint 615)** | Itens recorrentes (refeições, suplementos, meditação, ginásio, trading, trabalho, limite, pessoal, peso de N em N dias); avisos por níveis; ecrã «Agora»; Feito / +15 / Saltar com motivo; peso com histórico | **feito** |
| F2 — Tarefas e Inbox (Sprint 617) | Tarefas com foco Hoje (máx. 3 pendentes por pessoa), Semana e Ideias; botão 💡 «Ideia» no topo (Alt+I); cartão «Prioridades de hoje» na Rotina | **feito** |
| F3 — Material para reparações (Sprint 618) | A lista «Peças a encomendar» já existia (Sprint 594). Novo: push em dias úteis às 10:40 com as peças por encomendar e às 11:15 o último aviso (insistente) — o fornecedor aceita até às 11:30. Só sai se houver peças | **feito** |
| F4 — Google Calendar | O Mender já publica um feed ICS (`PublicCalendarFeedController`) que o Google subscreve. Falta importar o calendário Google (endereço secreto iCal, colado pelo Bruno) para a vista do Mender. Escrita no Google em tempo real (OAuth) só se o ICS não chegar | a seguir |
| F5 — Faturas por email | Gmail/Outlook → regra de reencaminhamento só para os remetentes conhecidos → endereço de receção → webhook de ingest que já existe (Sprint 173) → por aprovar. Sem OAuth às caixas inteiras (acordado com o Claude fiscal). Duplicados por SHA do PDF + NIF + nº | a seguir |
| F6 — Motor fiscal (sujeito passivo misto) | ver §4 | a seguir, com o Claude fiscal |
| F7 — Diário de trading (TradeZella + Notion) | pre-session, daily/trade recap, prints, importação de trades da corretora, estatísticas por hora/par. Último, por pedido do Bruno | depois |

## 3. Rotina — decisões (Sprint 615)
- **Pessoal**: `RotinaItem` / `RotinaRegisto` têm `UserId`; cada utilizador só vê os seus. Os avisos vão só para os dispositivos desse utilizador (`StaffPushJob.UserId`).
- **Recorrência**: dias da semana (bit = `1 << DayOfWeek`) ou de N em N dias desde uma data (ex.: peso de 14 em 14 dias). Hora de Lisboa.
- **Níveis de aviso**:
  1. antes (minutos configuráveis, ex.: «15,5» no fecho do PC);
  2. à hora, com os botões «✓ Feito» e «+15 min»;
  3. insistência aos +15 e +30 min, que fica no ecrã (`requireInteraction`) e vibra mais forte;
  4. fim do bloco, com o que vem a seguir;
  5. fim de um adiamento.
- **Idempotência**: as chaves dos avisos já enviados ficam em `RotinaRegisto.Avisos`. Primeiro grava, depois envia. Se o servidor esteve parado, só sai o aviso mais recente dos últimos 10 min, nunca uma rajada.
- **Limite (fecho do PC)**: adiar obriga a dizer porquê. Fica registado para se ver o padrão.
- **Botões da notificação**: o service worker abre `/rotina?acao=feito|adiar&item&dia` e a página faz a ação. No iPhone não há botões na notificação; tocar abre a página.
- **Auditoria**: `RotinaRegisto` não entra na auditoria (é atividade pessoal e muda a cada aviso).

## 4. Regras fiscais acordadas (para a F6) — fonte: Claude fiscal + Situação Fiscal Integrada (11/11)
- CAE 66190 é o principal desde 09/11/2026 (o 1519 terminou). Secundários: 47401, 58290, 95101, 95102 e 62100 (este desde 01/09/2026).
- IVA normal trimestral, **misto com pro rata de 20%**, só a partir do 4.º trimestre de 2026.
- Cada compra ou despesa passa a ter uma **afetação**: LopesTech (100%), Trading (0%) ou Mista (pro rata). Na última declaração do ano faz-se a regularização da pro rata definitiva (campos 40/41).
- **Payouts das prop firms**: isentos pelo art. 9.º n.º 27 e localizados fora de PT (B2B). Fora da UE têm direito à dedução (art. 20.º n.º 1 b) v)); na UE não têm. Fica configurável; validar com a contabilista.
- **Regime da margem** (DL 199/96): calculado por bem, margem negativa conta como 0, a fatura leva a menção legal e não discrimina o IVA.
- **Autoliquidação de serviços estrangeiros** (Google, Meta, Anthropic, FX Replay): liquida e deduz conforme a afetação. A FX Replay é Trading.
- **Coeficientes de IRS por linha**: bens 0,15; serviços com CAE 0,35. Aplica-se o corte do art. 31.º n.º 10 (2.º ano). O mínimo de existência não se aplica a 2026.
- Retirar o lembrete «passar a 66190 em jan/2027», porque já foi feito.

## 5. Vendas «sem fatura» (registo interno) — Sprint 616, decisão do Bruno
- O Bruno emite sempre fatura nas vendas do momento (balcão/Moloni). Algumas reparações seguem outro enquadramento, combinado com a contabilista Susana, e são faturadas fora do Mender (AT/Moloni). O Mender é registo interno, como um Excel: a declaração oficial sai da AT, não daqui.
- Na venda: botão **«Registar sem fatura»**. A venda fica com a etiqueta «Sem fatura». O motivo é opcional; por omissão fica «Sem fatura (registo interno)».
- Efeitos:
  - não entra no apuramento de IVA do Mender;
  - não aparece nos avisos «por faturar» do painel;
  - o lucro conta o valor inteiro (cobrado − custo das peças, sem IVA).
- Transparência:
  - o apuramento mostra quantas vendas e que total ficaram de fora;
  - a alteração fica na auditoria;
  - «Desfazer» reverte.
- Se a venda passar a ter fatura (nº registado ou documento Moloni), volta automaticamente a contar no IVA. Não se pode marcar uma venda já faturada.
