# 90 — Análise competitiva RoApp vs Mender (28-05-2026)

**Contexto:** Bruno fez trial do RoApp (https://roapp.io) e tirou 49 screenshots
em `Desktop/LopesTech/RepairDesk/RoAPP/`. Pedido: análise crítica, sem deixar passar nada
útil, mas sem copiar o que é redundante ou pior do que já temos. Cumpre `CLAUDE.md`:
pensar como engenheiro sénior + product engineer + SaaS founder.

> **Veredicto rápido:** O RoApp é um produto maduro, multi-vertical (oficinas, sapatarias,
> relojoarias…), com excelente cobertura horizontal. **Mas não tem PT-specific compliance**
> (Miguel confirmou: nem eles nem a B2Brouter cobrem ATCUD + emissão certificada PT).
> Para um vertical "telemóveis em Portugal", o Mender **já ganha em B2C-PT** e **perde em
> features genéricas** (booking calendar, chats omnichannel, payroll, ecommerce connect).
> Estratégia: **roubar o que faz sentido para o vertical PT, ignorar o resto.**

---

## 1. Resposta direta às perguntas do Bruno

### 1.1 Sidebar sempre aberta, mas estreita, com submenu à direita
**Sim. Vale a pena copiar.** No RoApp a sidebar tem ~190 px (label + ícone alinhados à
esquerda) e quando entras em **Settings**, abre uma **segunda coluna** com sub-itens
(General, Employees, Locations, Statuses, Notifications, Directories, Forms editor,
Print templates, Public pages, Finance, Prices, Marketing, Tags, Ecommerce Connect,
Chats, Telephony, Integrations, API, Subscription, Referral program). Funciona bem porque:
- Vês onde estás (no menu principal) **E** o sub-contexto.
- Não tens de navegar 2 cliques para chegar à sub-página.
- O espaço da página de trabalho continua amplo.

**Como aplicar ao Mender:** o padrão duplo-sidebar funciona naturalmente para `Definições/*`
(que já tem 7 sub-páginas). Hoje fazemos `/definicoes` como landing + sub-routes; podemos
manter as routes mas mostrar a segunda coluna sempre que `pathname.startsWith('/definicoes')`.
A sidebar primária mantém-se como S397.

### 1.2 Profile menu (avatar canto superior esquerdo)
No RoApp o avatar serve para sair (logout). O **edit-profile** (nome, email, telefone,
language, password) vive em `Settings > Employees` ou num menu dropdown do nome do
utilizador. Não vi ecrã dedicado de "My profile" no que abri, mas existe na lista
Employees ao editar o próprio.

**Como aplicar:** Mender já tem header com nome + Sair. **Falta-nos** um ecrã
`/definicoes/perfil` (ou modal) com: nome, email, telefone, idioma (PT/EN), foto, mudar
password. Isto já era pedido sénior óbvio — incluir no roadmap.

### 1.3 Payroll (Finance > Payroll Calculation / Payroll Accruals)
**O que é:** **cálculo de salários** dos funcionários. RoApp calcula:
- **Payroll Calculation:** valor a pagar (horas trabalhadas + comissões por serviço + bónus).
- **Payroll Accruals:** acréscimos (impostos, retenções, fundo compensação trabalho).

Funciona porque cada serviço tem "exceptional commissions" por funcionário (vi nas Services).

**Para ti hoje:** **não é prioridade** — és solo. **Quando contratares** (junho 2027?),
isto evita Excel à parte. Por agora ignora.

### 1.4 Connected apps / Telegram bot / Chats
**O que é:** RoApp tem um módulo `Chats` que liga **WhatsApp, Telegram, Messenger, Instagram,
Viber, SMS (via Twilio)** num inbox unificado. O cliente fala com a loja por qualquer canal
e tu respondes do Mender. Cria documentos directamente da conversa (ex.: ticket de
reparação a partir de uma mensagem WhatsApp).

**Telegram bot** = canal de Telegram da empresa (não conta pessoal). Cliente envia mensagem
e tu vês no inbox.

**Para Mender:** isto é **valioso** e diferenciador. Já tens `/definicoes/automacoes` com
n8n para email IMAP — extender para WhatsApp/Telegram via webhook é viável.
**Prioridade: média-alta** (próximo trimestre, depois das prioridades imediatas).

### 1.5 Páginas com menu horizontal interno (tabs)
**Sim, copiar.** RoApp usa este padrão em todo o lado:
- `My company` → Dashboard / Employee hours / Services / Products / Bundles
- `Finance` → Transactions / Payment links / Refunds / Balances / Payroll Calculation / Payroll Accruals
- `Inventory` → Stock / Devices / Purchase orders / Postings / Reservations / Conversions / Transfers / Stock takes / Write-offs / Returns
- `Reports` → Activity log / Finance / Tickets / Inquiries / Inventory / Marketing / Company Insights / Assortment analysis

Já fazemos isto em alguns sítios (Balcão tem Venda/Caixa/Fecho; Compras tem Inbox/Histórico).
**Generalizar.** Por exemplo `Catálogo` poderia ter abas `Stock físico / Stock virtual /
Loja online / Sem conteúdo / Stock crítico` (já tem tabs aliás), e `Relatórios` poderia
agrupar mais coisas em tabs em vez de páginas separadas no menu.

### 1.6 Tickets vs Reparações
**Tickets ≠ Reparações exactamente. Mas é o conceito genérico equivalente.**

No RoApp `Ticket` é qualquer **ordem de serviço** (reparação de telemóvel, reparação de
relógio, conserto de sapato, instalação, …). O sistema é multi-vertical por design.
Tu vês também `Workflows` que é um board kanban de **Tickets + Estimates** juntos (status
New / In progress / Pending / Delivery / Done). E `Inquiries` que são pedidos/leads que
ainda não viraram ticket.

**Conclusão:** o teu termo **Reparações** está **bem** se o Mender é vertical telemóveis.
Não trocar para "Tickets" — perde-se a clareza vertical. Se um dia o Mender for
multi-vertical (sapatarias…), aí discute-se nomenclatura.

**O que sim copiar:** o conceito `Inquiries` (pedidos não-confirmados, leads) é diferente
de Reparação. Hoje em Mender temos "pedidos online" do widget público — está perto. Mas
poderíamos formalizar uma fase **"Inquiry → Estimate → Ticket"** (lead → orçamento aprovado
→ reparação aberta). Ver §3.

### 1.7 Bookings e calendário
**O que é:** Agendamento de serviço a uma hora específica + cliente + funcionário. Tem
**vista calendário diário** (slots horários 09:00, 09:30, 10:00…) e mensal. Cliente
agenda online por uma `public page`. Pode ligar a Google Calendar/Outlook (vi
`Integrations` em Settings — não confirmei a integração específica).

**Mender já tem:** o **Agendar/Booking online** público (S389) com horários por dia. Isto
gera reparações. **O que falta:**
- Vista calendário **dentro do Mender** (não só formulário público).
- Bookings de outros tipos além de reparações (consulta, recolha, entrega).
- Sync com Google Calendar.

**Prioridade:** média. A funcionalidade de booking online já existe; falta o calendário
visual interno.

---

## 2. Inventário completo de features RoApp (que vi)

### 2.1 Menu principal (sidebar permanente)
1. **Welcome** — onboarding/getting-started
2. **My company** — dashboard de KPIs + Employee hours (turnos/timesheets) + catálogo de Services e Products + Bundles (combos)
3. **Tasks** — gestor de tarefas internas (não tickets) com assignee, due date
4. **Inquiries** — pedidos/leads (board kanban: New, In progress, Pending, Closed, Dropped off)
5. **Bookings** — agendamentos (vista tabela + calendário)
6. **Workflows** — kanban geral de Tickets + Estimates (New, In progress, Pending, Delivery, Done)
7. **Sales** — vendas balcão (POS)
8. **Invoices** — faturas com `Invoicing system` + `E-invoice status` (Peppol, etc.)
9. **Finance** — Transactions, Payment links, Refunds, Balances (Bank/Cash/Credit Card), Payroll Calculation, Payroll Accruals
10. **Inventory** — Stock, Devices (assets/equipamentos), Purchase orders (+ Client backorders, Products to reorder), Postings, Reservations, Conversions, Transfers, Stock takes, Write-offs, Returns
11. **Contacts** — clientes
12. **Chats** — inbox omnichannel (WhatsApp, Telegram, Messenger, Instagram, Viber, SMS Twilio)
13. **Calls** — telefonia integrada (provavelmente Twilio-style)
14. **Reports** — Activity log / Finance / Tickets / Inquiries / Inventory / Marketing / Company Insights / Assortment analysis
15. **Settings** — sub-menu vertical largo (~22 secções)

### 2.2 Settings sub-secções (todas vistas)
General · Employees · Locations · Inventory · Statuses · External notifications · Internal
notifications · Directories · Forms editor · Print templates · Public pages · Finance ·
Prices and discounts · Marketing · Tags · Ecommerce Connect (BETA) · Chats · Telephony ·
Integrations · API · Subscription · Referral program

### 2.3 Funcionalidades não-óbvias detectadas
- **"Training" mode toggle** no topo (sandbox safe vs live). Útil para demo/testes sem
  poluir dados reais.
- **Help chat widget** próprio (Intercom-style, com avatares dos engenheiros deles).
- **Forms editor visual** — drag-drop para criar campos custom em Tickets/Estimates/Inquiries/Contacts/Products/Devices.
- **Print templates editáveis** — talões/recibos/relatórios.
- **Subdomain branding** (`https://tenant.roapp.io/booking/`) — multi-tenant white-label.
- **Referral program** com link de partilha + 10% de desconto recíproco.
- **Bundles** (combos de produtos+serviços com preço único).
- **Devices** = equipamentos do cliente (track de IMEI/serial number, dono, garantia, histórico).
- **Stock takes** — inventário físico (contagem) com reconciliation.
- **Write-offs** — abates de stock formalizados.
- **Conversions** — converter um produto noutro (ex.: stock para peça-de-uso-próprio).
- **Reservations** — reservar stock para um cliente sem ser venda ainda.
- **Backorders / Products to reorder** — gestão proactiva de re-encomenda.
- **Exceptional commissions** por funcionário e serviço.
- **Ad campaigns** em Sales (atribuir venda a campanha de marketing).

---

## 3. Matriz "RoApp tem / Mender tem"

| Feature | RoApp | Mender | Comentário |
|---|---|---|---|
| **Compliance PT (ATCUD/SAFT/QR)** | ❌ (confirmado pelo Miguel) | ✅ via Moloni | **Vantagem decisiva nossa** |
| Sidebar permanente + submenu | ✅ | ✅ (mas sem submenu duplo) | Copiar duplo-sidebar |
| Dashboard KPIs | ✅ | ✅ | Equivalente |
| Cash flow chart | ✅ | ❌ | Copiar (Doc 88 §IDEIAS 1) |
| Tickets/Reparações | ✅ multi-vertical | ✅ telemóvel-first | Manter vertical |
| Workflows kanban geral | ✅ | ✅ (S398-399) | Equivalente |
| Inquiries (leads pré-ticket) | ✅ | ❌ formal (só widget público) | **Copiar — formalizar funil lead→orçamento→reparação** |
| Bookings + calendário | ✅ vista calendário | ⚠️ só formulário | Adicionar vista calendário interno |
| Tasks (internas) | ✅ | ❌ | Considerar (low effort, real value) |
| Sales / POS | ✅ | ✅ (Balcão) | Equivalente |
| Invoices | ✅ + Peppol | ✅ Moloni B2C | Mender melhor em B2C-PT, RoApp melhor em Peppol B2G/EU |
| Payroll | ✅ | ❌ | **Skip** — só relevante quando contratar |
| Bank/Cash accounts | ✅ | ⚠️ parcial (Caixa) | Considerar — Mender só tem caixa diária |
| Stock | ✅ amplo (10 sub-tabs) | ✅ (foco peças+produtos) | Equivalente |
| Devices (assets do cliente) | ✅ | ⚠️ (campo IMEI em Reparação) | Considerar tabela `Devices` separada |
| Stock takes | ✅ | ❌ | **Copiar — inventário físico anual é obrigação** |
| Write-offs / Conversions | ✅ | ⚠️ via ajustes ad-hoc | Formalizar |
| Reservations | ✅ | ❌ | Médio interesse |
| Chats omnichannel | ✅ | ❌ | **Copiar — diferenciador** |
| Calls / Telefonia | ✅ | ❌ | Skip por enquanto |
| Ecommerce Connect | ✅ (Shopify-style) | ✅ shop bridge custom | Equivalente, mas Mender tem deeper bridge |
| Forms editor visual | ✅ | ⚠️ campos personalizados S193 | Estender |
| Print templates editáveis | ✅ | ❌ (PDFs fixos no QuestPDF) | Médio — útil para múltiplos tenants |
| Public booking pages com subdomain | ✅ | ⚠️ rota pública sem subdomain | Considerar multi-tenant signup |
| Referral program | ✅ | ❌ | Skip por agora |
| Bundles | ✅ | ❌ (só Kits internos de peças) | Considerar — vender combos no balcão |
| Ad campaigns tagging | ✅ | ❌ | Skip |
| Training mode toggle | ✅ | ❌ | Skip |
| Help chat widget | ✅ | ❌ | Skip (custo Intercom) |
| Profile editing (nome/email/password) | ✅ | ❌ formal | **Copiar — gap óbvio** |

---

## 4. Roadmap priorizado: o que copiar do RoApp

### Tier 1 — Must-steal (próximas 2-4 semanas)
1. **Dual-sidebar para Definições** — sidebar primária (224 px) + secundária (sub-menu)
   sempre que estás em `/definicoes/*`. Padrão de Settings do RoApp.
2. **Página/modal "O meu perfil"** — nome, email, telefone, idioma, foto, mudar password.
   Gap óbvio que limita signup multi-tenant.
3. **Stock takes** (inventário físico) — registar contagem manual + diff vs sistema +
   ajuste automático. Bruno vai precisar para fechar 2026.

### Tier 2 — High value, médio esforço (1-3 meses)
4. **Inquiries → Estimate → Reparação funnel formalizado** — separar "pedido/lead" de
   "reparação aberta". Hoje vai tudo para Reparações; gap conceptual.
5. **Bookings calendar view** — vista calendário interna (dia/semana/mês) sobreposta à
   tabela actual de Agendamentos.
6. **Devices (asset registry)** — tabela separada de equipamentos do cliente
   (IMEI/serial, dono, garantia, histórico de reparações). Hoje IMEI vive na Reparação;
   um Device é uma entidade que viveria entre reparações.
7. **Tasks internas** — gestor simples de tarefas (assignee, due date, descrição). Útil
   para ti tipo "fazer follow-up Sergio" ou "pedir peça X".

### Tier 3 — Estratégico (3-6 meses, quando o Mender for SaaS)
8. **Chats omnichannel** — WhatsApp Business + Telegram bot + Instagram DM no inbox interno.
   Diferenciador real. Começa com Telegram (mais simples) ou WhatsApp (mais usado em PT).
9. **Bank account tracking** (não só Caixa) — registar conta bancária, transferências,
   conciliação. Substitui Excel paralelo.
10. **Print templates editáveis** — quando tiveres 5+ tenants pagantes, cada um quer o
    seu talão. Hoje o template é hard-coded no QuestPDF.

### Tier 4 — Skip (não fazer)
- Payroll, Calls/telefonia, Referral program, Ad campaigns, Training mode, Help chat
  widget (todos baixo ROI para o teu estágio).

---

## 5. Onde o Mender JÁ é melhor que o RoApp

Honesto, não complacente:

- **Compliance PT** — Moloni integrado, ATCUD funciona, garantia DL 84/2021. RoApp
  literalmente não cobre isto (confirmado pelo Miguel).
- **AT-aware** — `IBillingProvider`, NIF lookup, NC automática, bulk-emit faturas. RoApp
  é genérico EU.
- **IMEI lifecycle** — TAC auto-detect, Luhn validation, link venda↔reparação por IMEI.
  RoApp tem `Devices` mas sem awareness IMEI específico.
- **Reparações específicas** — IsCogs, COGS/OpEx separation, garantia 3 anos vendas vs 2
  anos reparações, PecasUsadas com ledger. Tudo isto é "telemóveis-em-PT" puro.
- **AI features** — extract-pdf fornecedor, OCR vision para faturas papel, alt-text para
  imagens shop, fuzzy SKU matching. RoApp não mostra IA visível.
- **n8n / IMAP forwarding** — automação para emails IMAP forward → ingest. RoApp não tem.
- **Shop bridge profundo** — webhooks tipados, SDK TS, ai-lens/ai-assistant external
  endpoints. RoApp tem Ecommerce Connect mas é Shopify-style básico.

**Mensagem para ti:** estás à frente em **vertical depth (PT + telemóveis)** e atrás em
**horizontal breadth (genéricos)**. A jogada certa é continuar a aprofundar o vertical e
copiar SÓ as features horizontais que claramente faltam (sidebar dupla, perfil, stock
takes, inquiries, calendar booking).

---

## 6. Conclusão estratégica

**Não vais ganhar ao RoApp em features horizontais** — eles têm anos de avanço e cobrem
N verticais. Tens **uma vantagem clara**: vertical PT-telemóveis + compliance AT. **Não a
percas tentando ser tudo para todos.**

A frase do Miguel — *"não cobrimos todo o fluxo PT, precisam de software adicional para
ATCUD"* — é o teu **moat**. Mender = ERP de oficina **com** compliance PT nativa via
Moloni. RoApp = ERP genérico **sem** compliance PT. Em PT-B2C, ganhas.

**Próximas decisões para o Bruno:**
1. Qual destes Tier 1 começamos primeiro? Recomendo **dual-sidebar Definições** (pequeno
   + impacto visual imediato no premium-feel).
2. Quando avanças para multi-tenant signup (registar nova oficina online), o **"O meu
   perfil"** deixa de ser opcional.
3. Stock takes — quando fizeres inventário físico próximo (fim 2026?), vais ter de fazer
   em Excel se não estiver pronto.

---

## 7. Bónus do `features.txt` (site roapp.io)

Análise do features.txt (6841 linhas de copy do site, lido por grep).

### 7.1 Integrações canónicas RoApp (lista oficial deles)
- **Pagamentos:** SumUp · Square (com Tap-to-Pay) · Mollie · Stripe
- **Contabilidade:** Xero · QuickBooks (2-way sync)
- **Telefonia:** Twilio Voice (calls online com transcrição AI)
- **SMS:** Twilio · SMSAPI
- **Chats:** WhatsApp Business · Facebook Messenger · Instagram Direct · Viber · Telegram
- **Marketing:** Google Contacts · Mailchimp-like
- **No-code:** Zapier · Make · próprios Webhooks
- **Email:** 2-way Gmail (coming soon) · 2-way Outlook (coming soon)
- **E-invoicing:** B2Brouter (Peppol delivery — sem cobertura PT confirmada pelo Miguel)

**Comparação Mender:** temos Moloni nativo (PT) + InvoiceXpress (planeado). Não temos
pagamentos online próprios — para o ecommerce bridge confiamos no IFTHENPAY (MBWay +
Multibanco PT) já integrado (S303). Falta SumUp/Square para Tap-to-Pay físico. Para
chats omnichannel, falta tudo (Tier 3).

### 7.2 Roadmap PÚBLICO do RoApp ("coming soon") — oportunidades para nós ficarmos à frente

| Feature "coming soon" do RoApp | Estado Mender | Oportunidade |
|---|---|---|
| **Engineer time on the job** | ✅ JÁ temos (Time Tracker S349) | **Estamos à frente.** Vender isto. |
| **Recurring service contracts** | ❌ não temos | Tier 4 (irrelevante p/ B2C oficina) |
| **Automated overdue reminders** | ❌ não temos | **Tier 2 valioso** — cron que envia email/SMS para faturas vencidas há X dias |
| **GPS / route view** | ❌ não temos | Skip (field service, não oficina) |
| **Custom checklists** | ⚠️ parcial (campos personalizados S193) | **Tier 2** — formalizar como checklist por tipo de reparação (ex.: diagnóstico iPhone 15 → 5 itens fixos a marcar) |
| **Deposits/prepayments p/ no-show** | ❌ não temos | Tier 3 — sinal antes de booking confirmado (anti-no-show) |
| **Message templates auto-replies** | ⚠️ parcial (Email 1-click S348 tem templates) | Estender ao WhatsApp quando chegar |
| **Customer segments** | ❌ não temos | Tier 3 — segmentação para campanhas |
| **2-way Gmail/Outlook** | ❌ não temos | Tier 4 (IMAP forward para fornecedores já cobre o caso de uso real) |
| **Tap-to-pay / mobile wallets** | ❌ não temos | Tier 3 — SumUp Tap-to-Pay no Android |

### 7.3 Features RoApp descobertas só no features.txt
- **Bin Locations:** sub-stock por prateleira/gaveta dentro de cada warehouse. Útil para
  oficinas com muitas peças (cada peça tem `Warehouse > Bin A1`). Mender hoje tem só
  `Part.localArmazenamento` (texto livre). **Tier 2 considerar.**
- **Roster Management:** escalonamento de turnos de funcionários (semanal). Tier 4 — só
  com equipa.
- **AI tools deles:**
  - Call/voice transcription (Whisper-style). Nós não temos calls.
  - Product recognition by image (foto → nome+descrição+background removal). **Tier 3
    interessante** — usar Claude Vision como já fazemos para alt-text.
  - AI-assisted CSV imports (Excel → estrutura detectada). **Nós já fazemos isto com
    fuzzy SKU matching (S157)** e Claude Haiku parser (S163).
  - Suggested replies in chats. Só com chats omnichannel ligados.
- **Refurbished Devices flow:** registar device refurbished → vendê-lo. Mender já tem
  via `Product.Origin = Refurbished` (S197) + IMEI tracking.
- **Customer-facing booking 24/7:** já temos (S389 Repair Request Widget S354).
- **Multi-location:** RoApp permite gerir várias lojas. Mender hoje é mono-tenant
  multi-location parcial (já temos `Location` em vários sítios). Formalizar quando o
  Bruno abrir 2ª loja.

### 7.4 Lições estratégicas finais
1. **A nossa stack PT é defensável** — Miguel confirmou que B2Brouter não cobre PT. Toda
   a parte AT/Moloni/ATCUD é nossa.
2. **RoApp tem mais larga, nós temos mais profunda.** Não tentar igualar largura. Manter
   a profundidade vertical (telemóveis + AT + Moloni + DL 84/2021 + Garantia 3 anos + IMEI).
3. **Algumas features deles ainda nem existem** (estão "coming soon"). Implementar antes
   deles em PT = positioning de "ERP mais completo para oficinas PT".
4. **A integração com no-code (Zapier/Make/Webhooks)** é importante para SaaS. Mender já
   tem Webhooks (S101-104) e está bem aqui. Adicionar conector Zapier oficial seria um
   selo nice-to-have para o site comercial.

---

## Ligação a outros docs

- [[89-Billing-Compliance]] — agora **confirmado pelo Miguel** (ver update).
- [[88-Design-Fiel-Mockups]] — alguns deltas já estão capturados (sidebar, donut, cash
  flow).
- [[project_competitor_roadmap]] (memória) — atualizar com features RoApp.
