# Dores Reais — extraídas de Reddit, reviews e do próprio Bruno

Este ficheiro contém **problemas concretos** que utilizadores reais têm. Cada dor é uma feature potencial. Quando construímos uma feature do roadmap, **deve mapear** para uma dor real aqui.

---

## A. Workflow real — Oficina de eletrodomésticos em Espanha (r/software)

> "Hello, we are a small business located in Spain, we are an appliance repair business. My boss is the technician, and I'm on the office. We're fairly overworked..."

**Workflow actual deles (problemático):**

1. **Atendimento** — pega telefone, anota morada/localização/nº telefone, agenda visita no Google Calendar
2. **Visita técnica** — técnico vai, diagnostica (se conseguir), tira fotos, faz documento à mão com NIF, gera fatura da deslocação e estimate
3. **Reporting (2-3 dias depois)** — técnico manda **WhatsApp** com: foto do documento + foto da etiqueta do aparelho + descrição curta com avaria, peças necessárias, códigos de spare parts e preços
4. **Office** — vê WhatsApp, mete peças no carrinho do fornecedor, confirma preço com cliente por WhatsApp, encomenda
5. **Chegada de peças** — identificadas por nº telefone do cliente na etiqueta
6. **Agendamento 2ª visita** — office liga cliente, agenda, escreve "bring spare parts" no Google Calendar, **imprime evento e cola com fita-cola**
7. **Visita reparação** — técnico vai, faz, cliente paga em cash (não têm dataphone)
8. **Estado financeiro** — técnico às vezes esquece marcar pago/não-pago, office não consegue saber

**Dores explícitas:**

| # | Dor | Solução no RepairDesk |
|---|---|---|
| A1 | "Perguntas via WhatsApp ficam inevitavelmente sem resposta e esquecidas quando chegam fotos novas" | **Sistema de mensagens interno persistente** entre office e técnico — pergunta fica "aberta" até ter resposta. Notificação que NÃO desaparece. |
| A2 | "Não há notificações permanentes" | **Notificações in-app + push** com estado pendente até serem actuadas. Não SMS frágil. |
| A3 | "Muitas vezes não sei quanto tempo a reparação vai demorar" | **Estimativa de tempo por tipo de reparação** baseado em histórico. ML aprende com o tempo. |
| A4 | "Pico de erros é não notificar via escrito e mandar SMS sem confirmação" | **Templates por estado** com confirmação read-by-customer. |
| A5 | "Lately I've started to write the price of the spare part in the calendar per his request, but it's not going well" | **Materiais como objectos individuais** com **5 estados:** *researching / to be offered / on shop list / ordered / arrived*. Preço por linha. |
| A6 | "Cliente não tem cash, técnico marca como pago ou unpaid" | **App mobile do técnico** com botão "marcar pago/não pago" + métodos de pagamento. Sync com office em real-time. |
| A7 | "Trabalho duplicado entre Google Calendar + WhatsApp + loja de peças + papel" | **Tudo num sítio só.** Adicionar visita → cria calendar event. Confirmar peça → vai à lista de encomendas. |

**Funcionalidade IDEAL deles (literal):**

> "When I pick up the call, I take all client data, add it to the software, add a new job with a job ID number, add the visit on the software and the software makes the calendar event.
>
> The technician makes photos and short description of the issue on his visit, and adds the required materials each as their own object (with 5 status... researching, to be offered, on shop list, ordered, and arrived) and the required labor time for the repairs.
>
> The technician can type a question, and I would get it as a notification that would remain until answered.
>
> I edit the materials, adding their code, and if they've been ordered, the expected date of arrival, or answer and ask questions, that the technician would get as a notification until answered.
>
> If a client asks, I only have to check the software, rather than check emails, spare part provider website, etc.
>
> After materials arrive and I mark them as received, the 'generate invoice' button becomes clickable..."

**Plataforma:** *"We're looking for an android app with a web panel if possible, so the technician uses his phone, and I use the web panel."*

→ **Insight chave:** os técnicos querem APP MÓVEL com câmara + descrição rápida + materiais como linhas + chat-question persistente. O office quer WEB com tudo organizado.

---

## B. Hobbyistas e pequenas oficinas (r/mobilerepair, sobre Reparo software)

**O que querem:**

| # | Dor | Solução |
|---|---|---|
| B1 | "Não preciso enviar SMS aos clientes, só preciso de tracking básico" | Modo "lite" / free tier sem comunicações pagas |
| B2 | "Sem internet, queria que funcionasse offline" | PWA com offline mode (cache + sync quando online) |
| B3 | "Queria imprimir tickets com o meu logo" | **Logo upload** + customizar template do ticket impresso |
| B4 | "Queria customizar T&Cs no ticket (defender-me legalmente)" | **Termos personalizáveis** que aparecem impressos |
| B5 | "Queria adicionar os meus próprios dispositivos" | Database de devices editável + add own |
| B6 | "Quero customizar os estados de reparação" | **Estados configuráveis** por loja |
| B7 | "Quero imprimir barcodes para tickets e items" | **Print barcodes** dedicado |
| B8 | "Mac version please" | Web/PWA chega — não construir nativo |
| B9 | "Translation to Spanish please" | i18n desde o início (português, espanhol, inglês) |

---

## C. Auto repair shops (r/mechanics)

**Dores partilhadas em vários posts:**

| # | Dor | Solução |
|---|---|---|
| C1 | "TekMetric duplicou o preço de $99→$199 sem aviso" | Pricing **estável e transparente**. Aviso 60 dias antes de qualquer mudança. |
| C2 | "AutoLeap drops jobs and customers randomly" | **Reliability** — testes E2E, monitoring, alerts |
| C3 | "AutoLeap has 12-month lock-in" | **Sem lock-in.** Pagamento mensal cancelável. Export completo dos dados. |
| C4 | "AutoLeap says they have no obligation to provide functional program" | **SLA público** com uptime garantido |
| C5 | "AllData has server issues, slow, estimates take forever" | **Performance first.** Lazy loading, paginação real, sem scroll infinito. |
| C6 | "QuickBooks integration in [shop monkey, autoleap] doesn't work properly" | **Integrações testadas com testes E2E** que correm diariamente |
| C7 | "Identifix wiring diagrams are ridiculously inaccurate for Ford/Dodge" | (Não aplicável — não fazemos wiring) |
| C8 | "Don't expect much from support, due to amount of work" | **Suporte rápido** (< 4h em horário útil PT) |
| C9 | "Mitchell 1 / ProDemand UI is ugly and dated" | **Design moderno**, mobile-first |
| C10 | "Software won't integrate with webserver if not cPanel" | **Self-hostable** com Docker (já temos) |

---

## D. Inventory pain (r/automotive)

| # | Dor | Solução |
|---|---|---|
| D1 | "Thousands of parts inventoried based on memory" | **Inventário com barcode** + scan via app |
| D2 | "I need a legit system to label items with specific location" | **Bin locations** (Local-A1, Local-B3) |
| D3 | "What's the cheapest setup for spreadsheet-style?" | Export CSV gratuito desde dia 1 |
| D4 | "Fishbowl is good but expensive" | Inventário básico **incluído** no plano entrada |

---

## E. Self-hosted/independent (r/selfhosted)

| # | Dor | Solução |
|---|---|---|
| E1 | "Quero self-host, mas software open-source repair-specific não existe" | **Versão self-host com Docker** (já temos) |
| E2 | "Zammad/osTicket é genérico demais para reparações" | **Workflow específico** de reparação (já temos) |
| E3 | "Quero pagar uma única vez, não SaaS recorrente" | Plano **lifetime self-hosted** + suporte opcional |
| E4 | "Bons tickets systems são raros e bons" | Posicionamento como **ticketing especializado para reparações** |

---

## F. Tracking pessoal de manutenção (r/Cartalk) — NICHO B2C

Este é um **mercado diferente** (consumer, não B2B). Mas vale a pena anotar:

| # | Dor consumer | Oportunidade |
|---|---|---|
| F1 | "Receipts in glovebox, system falls apart" | App **consumer-grade** para tracking pessoal |
| F2 | "Google spreadsheet" | App com sync cloud, gráficos, lembretes |
| F3 | "Apps: Fuelly, aCar, Drivvo, AutoCare, Autozis" | (concorrência B2C) |
| F4 | "Notebook in glovebox" | Mercado offline ainda existe |
| F5 | "Carfax CarCare deletes records when car sold" | **Histórico portátil** que segue o utilizador, não o veículo |

→ **Decisão:** B2C consumer não é o nosso foco. Mas o **Passaporte do Equipamento** (histórico que o cliente do RepairDesk tem do seu telemóvel) cobre uma parte disto, e diferencia.

---

## G. Bruno (próprio uso real, dogfooding)

Dores que o Bruno encontrou ao usar o RepairDesk:

| # | Data | Dor | Estado |
|---|---|---|---|
| G1 | 2026-05-08 | "Pedi telefone obrigatório no cliente — clientes do Messenger não têm" | ✅ Resolvido (telefone opcional) |
| G2 | 2026-05-09 | "Demasiados estados — quero só 4 visíveis" | ✅ Resolvido (Recebido / Diag Concluído / Reparado / Entregue) |
| G3 | 2026-05-10 | "Despesa de uma reparação aparecia noutra" | ✅ Resolvido (filtro frontend missing) |
| G4 | 2026-05-10 | "Dashboard ignora despesas linked, lucro falso" | ✅ Resolvido parcialmente (eliminou-se custo manual, só conta despesas linked) |
| G5 | 2026-05-11 | "PDF orçamento sem o meu logo / NIF / morada / CAE / IBAN" | ⏳ Pendente (Sprint próxima) |
| G6 | 2026-05-11 | "Botão 'Reabrir' não fazia nada, ficava preso" | ✅ Resolvido (endpoint dedicado 2026-05-13) |
| G7 | 2026-05-12 | "Scroll vertical infinito no dashboard e reparações é mau quando tiver 50 reparações" | ⏳ Pendente |
| G8 | 2026-05-12 | "Quero ver lucro REALIZADO separado das despesas pendentes em peças" | ⏳ Pendente (refactor dashboard) |
| G9 | 2026-05-12 | "Quero lucro POR CATEGORIA (reparações vs websites vs software vs junta)" | ⏳ Pendente |
| G10 | 2026-05-12 | "Bons editar a data de uma despesa onde me enganei" | ✅ Resolvido (edit despesa) |
| G11 | 2026-05-12 | "Após Entregue + Pago não devia editar nada" | ✅ Resolvido (lock 3-tier: aberto/frozen/encerrado) |
| G12 | 2026-05-12 | "Inútil ter botão 'Guardar', auto-save" | ✅ Resolvido (debounce 1.2s) |

**Métricas que o Bruno quer ver:**
- Receita total
- Receita recebida (já pago)
- Receita pendente (entregue mas não pago)
- Lucro realizado (só pagos)
- Investimento em stock (peças encomendadas, não aplicadas)
- Custos pendentes
- Ticket médio
- Margem média por reparação
- Lucro por categoria (Reparação / Website / Software / Junta / Hardware / Serviços)
- Reparações concluídas (período)
- Reparações em espera de peça
- Valor parado em peças

---

## H. Dores transversais a vários posts

| Dor recorrente | Mencionado em | Solução |
|---|---|---|
| **Reliability** (software perde dados) | r/mechanics × 3 | Backups automáticos, audit logs imutáveis, soft delete |
| **Pricing surprises** | r/mechanics × 2 | Pricing público, garantido 12 meses |
| **Lock-in contratual** | r/mechanics × 1 | Mensal cancelável, export grátis |
| **Suporte distante / lento** | r/mechanics × 3, Capterra × 1 | Suporte PT em horário útil + KB pública |
| **Customer communication is ugly** | r/mechanics × 2, r/software × 1 | Templates branded, WhatsApp-first |
| **Mobile vs desktop** | r/software × 1, Reparo × várias | Web + PWA + apps nativas (em fases) |
| **Multi-vendor parts ordering** | r/mechanics, r/automotive | Integrar com Mobiltrust, Tudo4Mobile, etc (fase 3) |
| **Quanto tempo demora?** | r/software, r/Cartalk | Estimativa por tipo de reparação (histórico) |

---

## Como usar este ficheiro

1. **Antes de propor feature:** verificar se mapeia para uma dor aqui. Se não mapeia, é vaidade — provavelmente não vale a pena fazer agora.
2. **Quando aparece feedback novo do Bruno ou de outros utilizadores:** adicionar à secção G (Bruno) ou criar nova letra.
3. **Quando uma dor for resolvida:** marcar com ✅ + sprint/data.
4. **Backlog priorizado:** dores com mais frequência (vários posts) > dores únicas. Bruno (dogfooding) tem prioridade sempre.

---

# ANEXO: Quotes literais Reddit/Capterra com análise

## I. r/mobilerepair — "Reparo - Offline repair tracking software"
*Autor: Kossano, técnico de microsolderagem polaco, escreveu software gratuito Windows offline.*

### Quote 1 — Filosofia anti-SaaS
> "I realized there's no free software available for people like me. **People who don't need to:** Send emails or SMS to their customers about current state of repair. Track their finances to a single penny. Have their database available from around the world."

**Análise:** existe um **subconjunto de utilizadores que NÃO QUER as features que nós vendemos**. Hobbyistas, micro-oficinas, pessoas a começar.
**Implicação para nós:** ter um plano **Free** muito limitado (sem SMS/email, sem cloud, sem tracking financeiro avançado) **ou** uma versão self-host com licença lifetime para este nicho. **Minha opinião: NÃO fazer isto agora.** Estes utilizadores não pagam — não vale a pena. Focar em B2B com volume.

### Quote 2 — Features mínimas que ele queria
> "Work on any Windows PC without the internet. Keep customer details. Create and print a repair ticket which I can give out to the customer. General track of finances. Simplified inventory system. Printing barcodes."

**Análise:** tracking + ticket impresso + inventário simples + **barcodes impressos** = MVP.
**Implicação:** Já temos tudo excepto **print barcodes** (peças + tickets). Feature pequena, valor real.

### Quote 3 — Comentário de Efficient-Swim6679
> "How do we add the work done, parts and prices item by item into the repair process?"
> Kossano respondeu: "Currently you can use notes for the repair. The new version which is in the making will have this option."

**DOR REAL:** linhas de trabalho/peças com preço **dentro do ticket de reparação**, não em notas livres.
**Implicação para nós:** já fazemos isto com despesas imputadas → mas devíamos ter "mão de obra" como linha também, não só peças.

### Quote 4 — luckyspic / netpastor: pedidos de Mac + tradução Espanhol
> "I'd love to work on this with you on porting it over to Mac"
> "Mac version please" — netpastor
> "Translation to Spanish please" — netpastor

**Análise:** comunidade open-source contribui se o software for free. Mas para SaaS, **não dependemos** disso.
**Implicação:** i18n desde o início (PT, ES, EN); Mac/iOS via PWA, não nativo.

### Quote 5 — AudienceFabulous4551
> "what does VAT mean?"

**Análise:** utilizadores fora da EU não percebem "VAT" (IVA). Mostra que **i18n não é só linguagem — é terminologia fiscal/local**.
**Implicação:** quando expandirmos, terminologia tem de adaptar ao país (IVA-PT, VAT-UK, Sales Tax-US, GST-AU, IGV-PE, etc.).

### Quote 6 — shrimpyt
> "I've been doing a couple of repairs on the side, but would like to start to grow it."

**Análise:** mercado de **hobby → pequena loja** existe e quer ferramentas profissionais a preço acessível.
**Implicação:** plano **starter €9-15/mês** (1 user, 50 reparações/mês) para este perfil.

---

## J. r/selfhosted — "Looking for selfhosted software for repair shop"

### Quote 7 — paulknulst (recomenda ticketing tools)
> "I would recommend using: Zammad, OpenSupports."

### Quote 8 — bosse (admite que tools genéricas servem mal)
> "You can adapt Request Tracker to do this..."

### Quote 9 — comment anónimo deletado, citado por outros
> "Sadly it's because they all want to make money, and managing ticketing systems is complicated."

**Análise:** mercado self-hosted **EXISTE** mas pequeno. Tools genéricas (osTicket/Zammad) são **adaptadas com esforço** porque não há nada repair-specific.
**Implicação:** existe oportunidade **self-hostable + Docker** repair-specific. **Já temos isto** (RepairDesk em Docker). Vale a pena posicionar publicamente.
**Minha opinião:** NÃO investir em onboarding self-host cedo. Focar em SaaS. Mas manter Docker funcional como diferenciador para early adopters técnicos.

---

## K. r/software — Spanish appliance repair (sarattenasai)

**Este post é o mais valioso. Já está coberto em detalhe na secção A. Repito quotes-chave que mostram dores:**

### Quote 10 — fricção do WhatsApp como ferramenta de operação
> "I often answer when he asks things, and I ask questions often that **get inevitably unanswered and forgotten** when the next pictures and short description are sent."

**Análise:** WhatsApp é canal **frágil** para operação. Mensagens enterram-se. Por isto temos de ter **mensagens internas persistentes** dentro do RepairDesk com estado pendente/respondido.

### Quote 11 — duplicação de trabalho
> "I often make the mistake of either notifying via phone (without any written confirmation, nor an estimate) or sending a whatsapp with the price of the spare part. Lately I've started to write the price of the spare part in the calendar per his request, but **it's not going well**."

**Análise:** quando uma ferramenta não serve, as pessoas inventam workarounds (escrever no calendar). Workarounds falham.
**Implicação:** se nosso software for fácil, **substitui workarounds**. Senão, é só mais uma camada que ninguém usa.

### Quote 12 — esquecer pago/não-pago
> "There are a lot of clients that the technician doesn't mark as paid nor as unpaid, and I can't know, nor can I track payments outside of manually doing so."

**Análise:** o estado **"pago vs não pago"** é fonte de stress real. Bruno também queixou-se disto.
**Implicação:** já temos "marca como pago" automático ao Entregar. Falta:
- **Lista clara "a receber"** (entregue mas não pago) — dor crítica
- **Lembrete recorrente** ao Bruno se está há > 7 dias sem receber
- **Botão "lembrar cliente"** que envia WhatsApp formal

### Quote 13 — visão deles do ideal (literal de operação)
> "The technician makes photos and short description of the issue on his visit, and adds the required materials each as their own object **(with 5 status... researching, to be offered, on shop list, ordered, and arrived)** and the required labor time for the repairs."

**Análise:** quando uma peça é encomendada, tem estados próprios. Não é "tenho/não tenho".
**Implicação:** **entidade `EncomendaPeça`** com estados: a investigar / a oferecer / na lista para encomendar / encomendado / chegou. Linked à Reparação.
**Minha opinião:** isto é importante mas **complexo**. Pode ser simplificado para 3 estados (a precisar / encomendado / chegou) sem perder valor.

### Quote 14 — notificações persistentes
> "The technician can type a question, and I would get it as a notification that would remain until answered."

**Análise:** **mensagens internas com estado** (lida/respondida vs aberta). Diferente de SMS efémero.
**Implicação:** sistema de **mensagens internas thread-based** com badge no app até serem respondidas. Pode ser feature crítica para oficinas com >1 pessoa.

### Quote 15 — visão Android + Web painel
> "We're looking for an android app with a web panel if possible, **so the technician uses his phone, and I use the web panel.**"

**Análise:** **divisão de papéis**: técnico mobile, office desktop. Ambos os UIs têm de existir e ser optimizados.
**Implicação:** nosso PWA atual serve as duas vistas. Mas devíamos **diferenciar UX** quando user é técnico vs admin. Por exemplo: técnico vê primeiro a lista das "minhas reparações de hoje", admin vê dashboard agregado.

---

## L. r/Cartalk — pessoas tracking carro pessoal

### Quote 16 — BannedFoeLife (spreadsheet)
> "google spreadsheet, it helps that you can open it from any device so it's always there on your phone a few clicks away even when you're at your mechanic."

### Quote 17 — Outrageous_Arm8116
> "I have a file folder for each of my cars. I keep the actual hard copy of every oil change, inspection, parts replacement I have done. **I find this better than a simple list because some repairs are warrantied** and I want to be able to show the mechanic the work was done. **As a bonus, when you sell the car the new buyer will appreciate the history.**"

**Análise:** **histórico = valor de revenda + garantia**.
**Implicação consumer:** se gerarmos um "passaporte" do equipamento que o cliente leva quando vende → cria viralidade.
**Implicação B2B:** loja que mantém histórico bom retém clientes.

### Quote 18 — Solavagary (pediu template)
> "Would you be able to send me a pic or template of urs?"

**Análise:** as pessoas **copiam soluções**. Marketing pode partilhar templates abertos como conteúdo (SEO).
**Implicação marketing:** publicar templates Excel "Histórico de reparação para impressão" como lead magnet.

### Quote 19 — Aggravating-Brick-33 (construiu app próprio!)
> "I used to that as well but it got messy and I built my own app for easier tracking with pricing and reminders. Here is the app: https://apps.apple.com/eg/app/car-maintenance-mileo-ai/id6746068653"

**Análise:** quando ferramenta falha repetidamente, **utilizadores constroem o seu próprio**. Validação de dor real.

### Quote 20 — CafeRoaster (vs CarFax)
> "**CarFax CarCare** ... it's not as fast or easy to use as a spreadsheet. ... it deletes DIY records once you mark the car as no longer owned, so as to prevent fraudulent entries entering public data."

**Análise CRÍTICA:** software corporativo (CarFax) **apaga dados do utilizador** para "proteger integridade do sistema". Esta decisão prioriza CarFax sobre user. **NÃO fazer isto.**
**Implicação:** **dados são SEMPRE do utilizador**. Soft-delete, export, ownership clara. RGPD.

### Quote 21 — JustAnotherDude1990 (hack email)
> "I have an email to myself titled vehicle maintenance log and just keep replying back to it with the date and mileage and what I did."

**Análise:** **email-to-self** é hack popular. Útil porque Gmail tem search e está sempre acessível.
**Implicação:** **import por email** — utilizador encaminha email para `repair+abc123@repairdesk.app` e cria entry automaticamente. Killer feature para hobbyistas.

---

## M. r/mechanics — Spruxed opening shop

### Quote 22 — GreasyGinger24 (TekMetric)
> "I use TekMetric. Probably not using to it's full extent but it's very user friendly. Their starter tier is only $99 a month."

### Quote 23 — Davidlp9498 (TekMetric price hike)
> "I was just looking to get Tekmetric not even 2 months ago and it was 100 a month. I think there was a price hike recently due to popularity growth. **My buddy just got his recently and it's 100 a month**"

### Quote 24 — jpoangney (Tekmetric)
> "Yeah. **No way I'm going to use a company that doubles their price over night!**"

**Análise:** **price hikes irritam profundamente.** Confiança quebrada. Cliente sai.
**Implicação:** pricing **público, claro, garantido por 12 meses** sem aumento surpresa. Comunicar mudanças com 60 dias de antecedência.

### Quote 25 — EducationAdept71 (AutoLeap, switching back)
> "I recently switched to auto leap and **want to pull my hair out**. The software is terrible. **Switching to tekmetric**, I heard a lot of good things about them. They are also buying my contract and still have a money back guarantee if I don't like it."

**Análise:** "Buying my contract" — TekMetric **paga o lock-in do concorrente** para conseguir cliente. Estratégia agressiva de aquisição.
**Implicação:** quando entrarmos no mercado, **assumir contratos** de concorrentes pode acelerar onboarding.

### Quote 26 — Acceptable-Month4134 (AutoLeap horror story)
> "Autoleap has been a nightmare for us for months! Tickets drop jobs, customers and vehicles at random. They state they fixed the problem but a month after resolution we were still experiencing it. Also read their master service agreement. **They have no obligation to provide a functional program but you will be locked in for a 12 month contract** and good luck getting out of it even for reasons that are their fault."

**Análise CRÍTICA:** SaaS que se protege legalmente DE NÃO funcionar. Vergonha.
**Implicação:** TOS público com **SLA real** + **rescisão livre mensal** + **export grátis**. Posicionamento: "o anti-AutoLeap".

### Quote 27 — Critical-Narwhal-933
> "Seriously? I feel like I need to take action against Autoleap. **What a piece of crap software that definitely is not what the salesman portrayed it to be.**"

**Análise:** sales agressiva → cliente desiludido → vingança pública. Reddit é onde vingança vai.
**Implicação:** marketing **honesto**, demos REAIS (não vídeos polidos), trials sem cartão de crédito.

### Quote 28 — Mikey3800 (Quickbooks vs SaaS)
> "We use Quickbooks desktop for accounting and invoicing. We tried shop monkey, auto leap, and one other, and **no one liked them at all**. I also absolutely hate the idea of paying a monthly subscription on software. **I think it is a scam.** Especially the price they want, and it not completely working right."

**Análise CRÍTICA:** existe **público anti-SaaS** que prefere desktop one-time. Não é o nosso core mas é mercado.
**Implicação:** **opção self-host com licença lifetime** (€999 one-time?) para este perfil. **Minha opinião: só pensar nisto na fase 4+.**

### Quote 29 — cptn_510 (AutoLeap satisfeito — minoria)
> "Since nobody else chimed in yet about AutoLeap, that's what we use. ... had them for going over 2 years now and they're constantly been updating ... obviously they have a referral program."

**Análise:** comentário POSITIVO sobre AutoLeap. **Programa de referrals** menciona explicitamente. Algumas pessoas estão satisfeitas. **Não toda a gente odeia o concorrente.**
**Implicação:** ter humildade. Mesmo um produto "mau" tem fãs por hábito ou network effects.

### Quote 30 — alexshurly
> "Mitchell for invoicing, Identifix for repairs."

**Análise:** as oficinas combinam ferramentas porque nenhuma faz tudo bem. Stack típica: software A para Y, software B para Z.
**Implicação:** **integrações > monolitismo**. Aceitar que clientes vão usar accounting noutro lado, etc.

---

## N. r/automotive — Inventory pain

### Quote 31 — OP (Spruxed equivalent)
> "We have **thousands of different parts that are all being inventoried pretty much entirely based off memory**. This is just not viable anymore..."

**Análise:** memória + crescimento = falha. Caracter universal.

### Quote 32 — tetractys_gnosys (cheapest)
> "The cheapest thing I can think of would be to just have a spreadsheet, spend some time getting everything entered into it with a unique SKU or UPC and quantity..."

**Análise:** spreadsheet é o **baseline** que temos de superar com clara mais-valia.

### Quote 33 — SlamedCards (IRS / inventory rules)
> "Many shops don't really maintain inventory **since most shops are below revenue rules from IRS to maintain an accurate one**. So you could be ok, especially if you use cash accounting."

**Análise:** regras fiscais influenciam **se vale a pena** ter inventário detalhado.
**Implicação para PT:** investigar Art. 31º CIRS / Decreto-Lei 198/2012 — para regime simplificado, inventário não obrigatório. Implicação: inventário do nosso software pode começar **opcional** sem castigar quem não quer.

---

## O. Capterra PC Repair Tracker reviews

### Quote 34 — Joao F. (CEO, IT Services, **provavelmente PT**)
> "No problem with the software at all. The only downside to it is the latest update (V9) that **won't function properly on the invoices section** ... **Didn't get any interest from developer to help** or even request an amount to work on it. ... So, don't expect much from support."

**Análise:** software pago anual ($125/ano) com bug crítico em invoices, support não responde. Cliente quer migrar ou construir o próprio.
**Implicação:** mercado **PT-falante** real e insatisfeito. Joao F. é potencial early customer.

### Quote 35 — Yvonne E.
> "In the beginning it was off to a great start like most companies that promotes their software. When i started to slow down my computer it was time to replace it with something better."

**Análise:** **performance degrades** com volume → cliente sai. Performance é feature crítica.

---

## P. r/mechanics — AutoLeap reviews concentradas

### Quote 36 — MoveBoth6111 (Tekmetric Canada problem)
> "I am looking for alternatives mainly because their sales guy seemed to have forgotten to mention that **Tekmetric is not setup to work with the Canadian taxation system (discounts), oops.** I came to discover that four months later I signed with them for a year."

**Análise:** falta de **adaptação fiscal local** quebra confiança.
**Implicação:** **IVA-PT, descontos PT, retenções PT** desde a v1.

---

## Padrões transversais que emergem (síntese crítica)

Após releitura completa, padrões de queixa **mais comuns** (≥3 menções diferentes):

1. **Pricing surprises / lock-in** (TekMetric, AutoLeap, "monthly subscription scam") — 5+ menções
2. **Software lento ou que perde dados** (AllData, AutoLeap, PC Repair Tracker V9) — 4+ menções
3. **Suporte distante / inexistente** (PC Repair Tracker, AutoLeap, várias) — 4+ menções
4. **Workarounds inventados (calendar, papel, WhatsApp, email-to-self)** — 5+ menções
5. **Esquecer marcar pago/não-pago** — 2 menções (Bruno + sarattenasai)
6. **Comunicação cliente fragmentada por canais** — 3+ menções
7. **Histórico de equipamento valorizado mas mal suportado** — 3+ menções (CarFax DIY apaga, Reddit "appreciate history")
8. **Software não adaptado fiscalmente ao país** — 2 menções (Tekmetric Canada, "VAT" question)
9. **Adaptar workflow → workarounds** — 4+ menções

**Conclusão prática:** estes 9 padrões são onde o RepairDesk pode **realmente diferenciar-se**. Tudo o resto é polish ou table-stakes.
