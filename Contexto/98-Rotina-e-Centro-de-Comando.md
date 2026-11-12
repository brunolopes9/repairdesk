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
| F4 — Google Calendar (Sprint 619) | Ver no Mender (Agendamentos) os eventos do Google/Outlook/iCloud pelo «endereço secreto iCal»: só leitura, sem OAuth. Endereço cifrado (ISecretProtector), nunca devolvido; só https e domínios conhecidos (anti-SSRF), redireções verificadas, 5 MB/15 s; cache 10 min; erro por calendário. A exportação do Mender para o Google já existia (feed ICS) | **feito** |
| F5 — Faturas por email (Sprints 620 + 632) | **Recomendado: Cloudflare.** faturas@lopestech.pt → Email Routing → Worker «mender-faturas» (código em `deploy/cloudflare/mender-faturas.js`), que faz POST do email inteiro (MIME) para `/api/external/email-ingest/mime` com `X-Para` e `X-Webhook-Secret`. O Mender valida o segredo contra o hash da empresa (Definições → Faturas por email → «Gerar segredo», que só aparece uma vez), tira os PDFs para a importação inteligente e avisa por push. Email sem PDF ou com falha → o Worker reencaminha para o Gmail do Bruno (ENCAMINHAR), por isso nada se perde. Montado a 12/11 (Worker + variáveis MENDER_URL/ENCAMINHAR + regra ativa). **Falta:** o Bruno gerar o segredo e colá-lo como MENDER_SECRET (Secret) no Worker; regras Gmail/Hotmail para os fornecedores habituais. Alternativa: caixa IMAP | **feito (falta o segredo)** |
| F6 — Motor fiscal (sujeito passivo misto) | **F6a feito (Sprint 622):** afetação por despesa (LopesTech/Trading/Mista) e pro rata (Tenant.ProRataPercent/ProRataInicio) no IVA dedutível, incluindo as despesas em autoliquidação. **Por fazer (F6b+):** afetação por omissão no fornecedor + país da entidade que fatura; mapeamento de autoliquidação UE (16/17) vs fora da UE (3/4 + 98); TipoOperacao nas vendas (margem por bem; payout fora da UE → campo 8, na UE → 9 configurável); regularização anual da pro rata (40/41); coeficiente do 1519 por data (0,75 até 09/11, depois 0,35); corte do art. 31.º n.º 10 só no 0,35/0,75 | **em curso** |
| F8 — Assistente por voz/chat (consenso com o Claude da Cortex, 12/11) | Entradas: microfone no PWA (Web Speech API, pt-PT) e nota de voz no Telegram, num **bot próprio** (o do estúdio usa long polling; dois leitores dão conflito), transcrita no Hetzner com faster-whisper (modelo carregado uma vez — ver `~/estudio/conversa.py`). Cérebro: serviço lateral «mender-assistente» com o **Claude Agent SDK** autenticado com CLAUDE_CODE_OAUTH_TOKEN (Max), **Sonnet**, sessão mantida (resume), fila de 1 pedido, teto diário de pedidos. Ferramentas via API do Mender (chave de serviço com scope «assistente»): criar tarefa/ideia, marcar rotina feita e registar peso (executa logo, com «Desfazer» até 5 min); marcar agendamento e mudar a hora de uma publicação (com confirmação); leituras (agora/hoje/amanhã, peças, agenda, lucro e IVA só para o Bruno); nada de vendas, faturas nem apagar. Tudo auditado. Limite da Max: mesma deteção do estúdio (regex + chave `limite-<DATA>`, ler `~/estudio/data/alertas.json` em só leitura); sem fallback para a API. Só para uso pessoal do Bruno — clientes do SaaS teriam de usar a API com chave | a seguir |
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

### 4.1 Validação do Claude fiscal (11/11, instruções oficiais da DP) — modelo da F6
- **Afetação** por compra/despesa (override por linha): LopesTech (100%), Trading (0%), Mista (pro rata). Por omissão: stock/peças = LopesTech; FX Replay, prop firms e challenges = Trading; Anthropic, Google Workspace e alojamento = Mista (ou LopesTech, se forem só para clientes).
- **ProRataInicio = 2026-11-09** (efeitos da alteração). Antes disso: as mistas deduzem por inteiro se ligadas à atividade tributada; as de trading nunca deduzem.
- **Regularização anual** (última DP do ano, passo manual explícito):
  - definitiva = bases com direito à dedução (incl. campo 8) ÷ todas as bases ativas (1+3+5+7+8+9);
  - excluir as do art. 23.º n.º 5 e os subsídios não tributados;
  - arredondar às unidades, por excesso;
  - diferença × IVA das mistas → campo 40 ou 41, mais o Anexo de Regularizações.
- **Autoliquidação:**
  - serviços da UE → base 16, IVA 17, dedução 24 × afetação (não vai a 3/4 nem 97);
  - serviços de fora da UE → base 3, IVA 4, Quadro 06 «SIM» + campo 98, dedução 24 × afetação;
  - bens da UE → 12/13, dedução 21–24 (inventários no 22);
  - fatura com IVA estrangeiro → sem autoliquidação; o IVA é custo.
  - **O fornecedor guarda o país da entidade que fatura** (Anthropic Ireland vs PBC, OpenAI, Vercel…).
- **Vendas:**
  - Normal → 1/5/3 + 2/6/4.
  - Margem → base = margem ÷ 1,23 nos campos 3/4, sem IVA discriminado na fatura; margem negativa por bem = 0.
  - Payout com cliente fora da UE → **campo 8** (com dedução; entra no numerador da pro rata).
  - Payout com cliente na UE → nem 7 nem 8; proposta **campo 9**, configurável, **a validar com a Susana**, tal como a recapitulativa.
  - Payouts de antes de 09/11 → mesmo mapeamento por localização, sem pro rata.
- **IRS:**
  - 1519 a 0,75 até 09/11/2026 (posição da AT; o CAAD defende 0,35 — o Mender mostra só um aviso);
  - corte do art. 31.º n.º 10 só no 0,35 e no 0,75;
  - regra dos 15% só sobre o 0,35 e o 0,75 (4.104 € OU SS, se maior, + pessoal, rendas, e-fatura);
  - IRS Jovem 75% com limite de 55 × IAS;
  - sem mínimo de existência em 2026.

### 4.2 Fornecedores do Bruno — entidade, tratamento e afetação (Claude fiscal, 11/11, a partir das faturas)

**Serviços de fora da UE** → campos 3/4 + 98, dedução no 24 × afetação:
- Anthropic (PBC, EUA; o «EU VAT IE…» é só o OSS) — Mista.
- TradersConnect (Reino Unido; fatura em USD, câmbio BCE) — Trading.
- FX Replay (presumo Inc., EUA — confirmar na fatura; USD) — Trading.

**Serviços da UE** → campos 16/17, dedução no 24:
- Hetzner (DE) — LopesTech (Mista se também alojar trading).
- 5G-M/Earth Trading (IT): os diagramas são serviço (16/17); as ferramentas são bens (12/13) — LopesTech.

**Bens intracomunitários** → campos 12/13; dedução no 22 (inventário) ou 24 (outros); tudo LopesTech:
- 4Phones, iPartsTrade, MobileSentrix e Eleshop (NL);
- Utopya (FR);
- LC Phones/POCINHAS (LU) — nota de crédito Honor X7b por refletir;
- LTronics (IE);
- Tectake (ES) — retificativa N1147902; devem 8,78 €;
- PowerPlanet/LEASK, KaQuu, Inter Movil e SpainSellers (ES);
- IluminaShop (ES) — confirmar se veio com IVA ES;
- VEVOR via Amazon (FR, 0%).

**Com IVA estrangeiro** (custo, não vai à DP):
- Amazon com vendedores DE/UK;
- AliExpress (Bailisun, Geeboon) — ver IOSS/importação.

**Nacionais:**
- peças: Tudo4Mobile, Microwire, Robert Mauser — LopesTech;
- material: Leroy, IKEA, Brico Depot, FCL, Volter — LopesTech;
- CTT: parte isenta;
- MEO: Mista;
- refeições: não dedutível (art. 21.º);
- Pingo Doce (secador-ferramenta): LopesTech.

**Avisos:**
- Volter: fatura em nome de «João Lopes» com o NIF do Bruno → pedir 2.ª via.
- Moloni: IVA PT normal.

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
