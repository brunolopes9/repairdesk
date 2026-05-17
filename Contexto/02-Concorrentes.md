# Análise de Concorrentes — RepairDesk SaaS

Documento vivo. Atualizar à medida que descobrimos coisas novas. Última atualização: 2026-05-13.

---

## 1. RepairDesk (Lahore, Paquistão) — `repairdesk.co`

**O que são:** o "incumbente". 3000+ lojas pelo mundo.

**Pricing público (verificado por Codex 2026-05-15):**
- $99 / **loja** / mês (Essential — 5 users incluídos)
- $149 / **loja** / mês (Growth)
- Advanced — custom
- Add-ons pagos: payments, communication, marketing

⚠️ **Correção 2026-05-15:** anteriormente este documento dizia "$99/user/mês". Errado. O site oficial cobra **por loja**, não por user. Isto muda a comparação competitiva: o RepairDesk não é tão caro quanto eu pensava por user, e o modelo correcto a seguir é também **por loja**, não por user (cf. `07-Pricing-Proposta.md`).

**Features que destacam no marketing:**
- POS (Point of Sale)
- Repair tickets
- Inventory management
- Employee management
- Reporting
- Multi-store
- "Unified Communication Ecosystem" — calls, emails, texts, social num só sítio
- 40+ integrations
- RepairDesk Payments (payment terminals)
- Self check-in
- Google Reviews integration

**Indústrias que servem:** cell phone, computer, jewelry, watch, drone, mail-in, power tools, bicycle, camera, small engine, heavy duty, shoe, tailor, wireless.

**Pontos fortes percebidos:**
- Marca consolidada no mercado US
- Imenso volume de features
- Marketplace de integrações
- Hardware (terminals POS, cash drawers)
- Estabilidade

**Vulnerabilidades / queixas reais (Reddit, Capterra, reviews):**
- "Distante do chão da loja"
- "Enterprise-ised, cheio de legacy"
- UX antiga
- Suporte distante (Lahore, fuso horário muito diferente da Europa)
- Updates limitados aos clientes US
- Sem MBWay, SAFT-PT, IVA PT
- Sem certificação de software de faturação portuguesa
- $99/**loja**/mês continua caro para pequenas oficinas portuguesas (≈€90, ≈10% do salário mínimo PT 2026 que é €920). Não é argumento decisivo de preço; o nosso argumento principal é **localização PT** e **UX moderna**, não preço.

**Quotes de utilizadores satisfeitos** (do site deles, viés esperado):
- Tonya Parker (N8's): "very easy to use, helps keep records of sales tax"
- Miguel Brito (Spa City iRepair): "Unified Communication Ecosystem cater all needs"
- Tim Phelps (Techy Company, 53 locations): "track sales, performance, inventory"
- Christopher Smith (Savannah iDoctor): "faster check-ins, smooth payments"

---

## 2. RO App (Reino Unido / Polónia)

**O que são:** SaaS europeu moderno, focado em vários verticais de reparação + serviços. **Concorrente mais próximo do que queremos ser.**

**Pricing público:**
- €15/mês (single user, single location, entrada)
- €29/mês (2+ locations)
- €69/mês (com onboarding personalizado)
- Free trial 7 dias

**Resultados que mostram (médias clientes 1 ano):**
- 30% crescimento anual
- 3× mais rápido a receber pagamentos
- 80% menos tempo em tarefas rotineiras
- 5× performance da equipa
- €1500 poupados em controlo de inventário
- 50% aumento satisfação cliente

**Features que TÊM e nós AINDA NÃO temos** (gap analysis):

### Críticas (deveriam estar nas próximas sprints)
- [ ] **Online Booking** — cliente agenda hora 24/7
- [ ] **Scheduler / Calendário** com drag-drop
- [ ] **Kanban view** das reparações
- [ ] **Device auto-detection por IMEI**
- [ ] **E-Signature** em estimates aprovados pelo cliente
- [ ] **Mobile app para empregados** (iOS + Android) — não PWA
- [ ] **Mobile app para owner** (iOS + Android) — dashboard remoto
- [ ] **Tablet app**
- [ ] **POS app** (mobile/tablet)
- [ ] **Bin locations** (warehouse com prateleiras identificadas)
- [ ] **Stocktaking** (inventário físico) — 4 métodos
- [ ] **Bundles** (peças agrupadas: ecrã+pelicula+cola)
- [ ] **Location-based prices**
- [ ] **Tags em clientes**
- [ ] **Scheduled SMS**
- [ ] **2-way SMS / VoIP / Email** integrados
- [ ] **Public API**

### IA features que eles têm (e cobram extra na maior parte)
- [ ] Transcrição de chamadas e voice messages
- [ ] Summary de chamadas
- [ ] Sentiment analysis
- [ ] Reconhecimento de produto por foto + remoção de fundo
- [ ] Import de Excel de fornecedor com IA (parse automático)
- [ ] Respostas sugeridas em chats

### Integrações que eles têm
- [ ] WhatsApp, Facebook Messenger, Instagram (eles têm; nós só temos link WhatsApp)
- [ ] QuickBooks Online, Xero, Fakturownia (contabilidade)
- [ ] Stripe, Mollie, Square, SumUp (pagamentos)
- [ ] Marketplaces / Online stores
- [ ] Zapier, Make (no-code)

### Multi-localização
- [ ] Gerir várias lojas de uma conta única
- [ ] Performance por localização
- [ ] Standardizar workflows

**Onde nós já estamos à frente / podemos ficar:**
- Português nativo (eles têm tradução)
- SAFT-PT (eles não)
- MBWay (eles não)
- AT webservices PT (eles não)
- Workflow específico de reparação (eles são genéricos demais — também servem cleaning, HVAC, alfaiataria)
- Verticalização: nós focamos em telemóveis/eletrónica; eles tentam tudo
- Open source / self-hostable (futuro?) vs SaaS-only

**Vulnerabilidades:**
- Português é tradução mole, não nativo
- Sem suporte a IVA PT / regimes portugueses
- Generalista demais → UX vertical fraca
- AI features pagas extra (call assistant)
- Suporte UK/PT — não Lisboa/Porto

**Pricing comparativo:**

| Tier | RO App | Nosso preço-alvo |
|---|---|---|
| Solo | €15/mês | €19/mês |
| 2+ locations | €29/mês | €29/mês |
| Tailored onboarding | €69/mês | €49/mês one-time |
| Unlimited tickets | €29/mês | incluído desde solo |
| API access | €15/mês extra | €15/mês extra ou plano top |

---

## 3. BytePhase

- $499 / user / mês
- 4.9/5 em Capterra (15 reviews)
- Posicionamento premium
- Não relevante como concorrente directo — outro mercado

---

## 4. RepairCMS Ultimate

- $99 / user / mês
- 5.0/5 em Capterra (6 reviews — pouco volume)
- Free trial e free version
- "Highest rated" badge na Capterra
- Vale a pena espreitar features e UX

---

## 5. PC Repair Tracker

- $125 / ano (flat, não por user)
- 4.2/5 (5 reviews)
- Modelo antigo: software local Windows, não cloud
- Reviews negativas: "V9 quebrou invoices", "support não responde", "afraid of webmin"
- **Insight:** existe mercado para licença anual barata, não SaaS recorrente
- Para hobbyistas e oficinas micro

---

## 6. Repair Spots

- $44.99 / user / mês
- 5.0/5 (5 reviews)
- Pouco conhecido — vale a pena ver

---

## 7. Reparo (Kossano, software gratuito) — `discord.gg/reparo`

**O que é:** software OFFLINE Windows feito por técnico-developer paquistanês para hobbyistas / pequenas oficinas. Free. Sem internet. Sem features pagas.

**Features que eles destacam (e nós devíamos pensar em copiar):**
- ✅ Repair tracking com imagens
- ✅ Inventory system
- ✅ Devices database (Apple — adicionar próprios)
- ✅ **Estados customizáveis** (cada loja define os seus)
- ✅ Print repair tickets
- ✅ **Print barcodes** (tickets E itens inventário)
- ✅ **Customizar campos obrigatórios** do ticket
- ✅ Finanças & Accounting (resumo por estado e data)
- ✅ Light/Dark themes
- ✅ **Upload do próprio logo**
- ✅ **Adjust T&Cs** (termos personalizáveis no ticket impresso)
- ❌ Funciona só em Windows (mac/mobile = roadmap deles)
- ❌ Sem SMS/email para cliente (intencional — para reduzir custos)

**Insights:**
- Há mercado para versão offline / self-hosted / one-time-license — não é o nosso foco mas é nicho real
- **Customização** (estados, campos, logos, T&Cs) é valorizada e nós não temos ainda
- **Print barcodes** é feature simples e útil
- Discord como suporte funciona para comunidades técnicas

---

## 8. Outros para investigar

- **Shopmonkey** (auto repair, US)
- **Tekmetric** (auto repair, US, $99-199/mês, queixas de price hike)
- **AutoLeap** (auto repair, US, **MUITAS reviews negativas** — bugs, 12-month lock-in, "dropping jobs")
- **Shopview** (heavy duty trucks)
- **Fullbay** (heavy duty)
- **AllData** (auto repair, "slow, server issues")
- **Mitchell 1** / **ProDemand** (auto repair info)
- **Identifix** (wiring diagrams — "ridiculously inaccurate" Ford/Dodge)
- **My Gadget Repairs** (mencionado em comparações)
- **Repairshopr** (US, ainda popular)

---

## 9. Templates B2C (não competimos directamente, mas inspiram)

Para a parte de **portal cliente** (tracking estilo Uber Eats):
- **Uber / Uber Eats** — tracking visual perfeito, estados claros, ETA
- **Glovo** — chat in-app com staff
- **Mercedes-Benz / BMW Service** — agendamento de manutenção, histórico de viatura
- **Apple Service** — status updates por SMS, follow-up qualidade
- **Worten Resolve** — atendimento omni-channel

Para a parte de **tracking pessoal de equipamento (passport)**:
- **CarFax** — história completa de um carro pelo VIN
- **Strava** — histórico de bicicleta com fotos
- **MyChart (saúde)** — histórico médico
