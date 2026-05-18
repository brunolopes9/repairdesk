# Insights de mercado — Reddit r/mobilerepair + r/CRM

Data de captura: 2026-05-18

Threads consultadas (Bruno):
- `r/mobilerepair` — "What is the best all-in-one software for a repair startup" (9 meses, 16 comentários)
- `r/CRM` — "Easily customizable Open Source CRM for a small repair shop" (6 meses)

---

## 1. ⚠️ ALERTA CRÍTICO — Colisão de nome

> *"RepairDesk was the closest option for us. However, pricing based on location is hell."* — Similar-Cream-2010, r/mobilerepair

**Existe um software chamado "RepairDesk" no mercado.** É um produto comercial estabelecido (US/global) com pricing per-location.

**Implicação para nós:**
- **Não podemos usar "RepairDesk" como marca pública.** Risco de trademark + confusão de mercado.
- A memória existente `project_lopestech_roadmap` já refere "Rebrand do nome RepairDesk — Bruno + depois 5-10 clientes". Argumento agora **muito mais forte** — rebrand **antes** dos primeiros 10 clientes, não depois.
- Hoje "RepairDesk" só aparece em UI interna (Login, Layout, README, portal cliente). Substituir é mecânico assim que haja nome novo.

**Sugestões de nomes para rebrand** (a discutir):
- *Oficina.io / oficina.pt* — directo, .pt forte
- *Bancada* — onde se trabalha em hardware. .pt disponível?
- *Reparo* — verbo, simples
- *FixDesk* — mantém o "Desk" sufixo
- *Banc.pt* — short, brand-able
- *FichaPro / Ficha* — alusão à "ficha de reparação"

Bruno: agendar 30 min com brand para shortlistar 3 nomes + verificar disponibilidade (.pt domain + EUIPO trademark search).

→ Doc relevante: `26-Brand-Design-System.md` (já tem decisões sobre identidade visual).

---

## 2. Dor #1 da concorrência: pricing per location

Citação literal: *"pricing based on location is hell. We tried to explain that we have home visits for our customers so they considered as an extra location. With a total 6 location, counting the workshop as a location too, this will cost more than developing our own ERP system like Odoo"*

**Implicação para o nosso pricing:**
- O competidor cobra **por localização**. Cliente com 6 sites paga 6x.
- Cliente até preferiu construir Odoo em vez de pagar.
- **Decisão recomendada:** pricing por **tenant** (não por loja física). Tenant = entidade legal. Multi-loja dentro do mesmo tenant é **incluído**.
- Diferenciador comercial vs RepairDesk-US: "preço único, todas as tuas lojas incluídas".
- Verificar `07-Pricing-Proposta.md` e validar que isto está alinhado. Se não, ajustar.

---

## 3. Inventory cross-location é o real pain point

> *"Our biggest headache is inventory. We need one system that can unify everything: items across branches, trade-in stock, ecom stock, and workshop usage."*

**Necessidade reportada:** stock unificado entre lojas, armazém central, e-commerce.

**Estado actual RepairDesk:** stock por tenant, **não multi-loja**. Quando implementarmos multi-loja (Horizon 2 do roadmap), o **stock partilhado entre lojas** é feature de venda.

Implicação operacional:
- Para beta (1ª oficina, 1 site): não bloqueia
- Para 2ª oficina onwards: feature obrigatória se mira mais de 1 site

---

## 4. SKU management é mal feito pelos concorrentes

> *"RepairQ has SKU issues — having to manually spreadsheet keeping track of what SKUs are used and what aren't is ridiculous, we just stopped using SKUs"* — KaboodleMoon, r/mobilerepair

**Implicação:** RepairQ (concorrente directo) tem **SKU manuais** e os utilizadores **desistem** de usar. Isto é uma fraqueza real.

**Estado actual RepairDesk:** entidade `Part` tem campo SKU (Sprint 31 do Codex). **Auto-incremento de SKU** seria diferenciador.

Sugestão Codex coding futura:
- `Auto-SKU generator` configurável por tenant: prefixo + número sequencial
- Exemplo: `BAT-0001`, `BAT-0002` ... ou `ECRA-IP13PM-001`
- Bruno pode override manualmente

Esforço estimado: 1 dia. Bom candidato a futuro #C9 ou #C10.

---

## 5. Trade-ins — feature potencial (não urgente)

> *"Trade-ins (people selling their old devices to us)"* + *"Trade-in stock — Best I've found [in RepairQ]"*

**Feature:** cliente entrega telemóvel antigo, recebe crédito para reparação ou novo equipamento.

**Estado actual:** não temos. Bruno também não faz trade-ins (que eu saiba).

**Quando faria sentido:**
- Quando uma oficina cliente nossa fizer trade-ins (pouco comum em PT, mais em UK/US)
- Como diferenciador para target maior
- Horizon 2/3, não bloqueia beta

→ Adicionar ao backlog em `04-Roadmap-Detalhado.md` Horizon 2 (se ainda não estiver).

---

## 6. Necessidade ERP-style sem complexidade ERP

> *"I've looked into ERPs like Odoo, Zoho, even NetSuite, but I'm worried about complexity vs. actual usability day-to-day for the employees."*

**Tensão real:** ERP tem features, mas é complexo. Software vertical (RepairQ) é simples mas falta features.

**Oportunidade RepairDesk:** **vertical e profundo**, não horizontal. Beat ERPs no domínio "oficina" specifically. Não tentar competir como ERP genérico.

Reforça princípio do roadmap actual: **verticalização. Eletrónica primeiro. Outras categorias só quando houver tração.**

---

## 7. Solo / side-gig é um segmento sub-servido

> *r/CRM thread*: solo operator, "IT repair shop side of regular job", quer:
> - contactos com custom fields
> - assets (laptops, computadores) com fields tipo CPU/RAM/storage
> - work orders ligados a cliente
> - **self-hosted ou open source**

**Implicação:**
- Segmento "solo / part-time" existe e procura activamente.
- Resolveram com vTiger CRM self-hosted (free), KrayinCRM, ou Frappe/ERPnext.
- **Estes não são software de oficina** — são CRMs adaptados. Há buraco para um produto vertical SaaS PT a preço acessível.
- **Pricing tier "Solo" €9-15/mês** poderia capturar este segmento sem desvalorizar tier €19/39/89 actual (em `07-Pricing-Proposta.md`).

---

## 8. Custom fields em equipamento é necessidade real

> *"Assets/items - customizable to keep track of hardware of a client. Such as fields with laptop brand/model/type and for computers fields like motherboard/processor/memory/storage/videocard"*

**Estado actual RepairDesk:** `equipamento` é string livre + `imei` opcional. Sem custom fields.

**Implicação:** lojas de IT (não só telemóveis) precisam de fields tipo: marca/modelo/tipo/CPU/RAM/storage. Adicionar **custom fields configuráveis por tenant** seria diferenciador, especialmente para o segmento IT-repair.

**Esforço estimado:** moderado. Schema `EquipmentField` por tenant + UI no detalhe da reparação. 2-3 dias Codex.

---

## 9. "Software desadaptado em 2025" é dor universal

> *"ServiceM8 especially feels outdated, and the integrations are painful"* + *"sticking with ServiceM8 feels like we're working on 2008 software in 2025"*

**Implicação:** mercado quer software **moderno** em UI/UX. ServiceM8, RepairTrax (mentioned negatively), GLPI (mentioned as "intensive") — todos sentem-se antigos.

**Vantagem RepairDesk:** stack moderno (.NET 10, React 19, Tailwind v4) + design system actual (sprints 30/33 do Codex). Continuar a polir UX é parte do moat.

---

## 10. Integração com contabilidade é "lock-in feature"

> Reddit user: *"Integração TOConline / Sage / PHC — empresas adoram integrações, reduz trabalho contabilístico, cria lock-in, aumenta retenção"*

**Implicação:** integração com software de contabilidade português é **retenção, não aquisição**. Cliente que liga RepairDesk ao TOConline dele não muda de fornecedor facilmente.

**Decisão actual:** já temos `35-Faturacao-Decisao-Final.md` para integração com providers de **facturação** (Moloni / InvoiceXpress) que já comunicam à AT. Integração com **contabilidade** (TOConline, Sage, PHC, Primavera) seria fase seguinte.

Adicionar à roadmap Horizon 2.

---

## 11. PWA > app nativa (concordância)

> Reddit comment: *"Tu provavelmente NÃO precisas app nativa durante muito tempo. Uma boa PWA: resolve 80-90%, custa MUITO menos, mantém stack única, evita React Native/Flutter."*

**Validação externa** do que `24-PWA-Offline.md` já diz. PWA primeiro. App nativa só pós-tracção.

---

## 12. Outras ideias dispersas

| Ideia | Valor | Quando |
|---|---|---|
| Customer loyalty / points | Médio | Horizon 2 (após beta) |
| Agendamento online (cliente marca hora) | Médio-alto | Pós-beta |
| Push notifications web | Alto | Pós-beta, mas tecnicamente fácil |
| AI diagnóstico via sintomas | Alto teaser, baixa execução | Horizon 3 (sem dados, é placebo) |
| Base de conhecimento interna (sintoma → solução) | Alto a longo prazo | Horizon 3 (precisa 1000+ reparações) |
| Pagamento MBWay no portal cliente | Alto valor | Horizon 2 (precisa KYC SIBS) |

---

## Acções concretas a tirar daqui

### Imediato (esta semana)
1. **Decidir rebrand do nome** — risco trademark com "RepairDesk" US. Bruno: shortlistar 3 nomes, verificar .pt + EUIPO.
2. **Validar pricing por tenant (não per location)** em `07-Pricing-Proposta.md`. Confirmar que não estamos a copiar o erro do concorrente.
3. **Considerar tier "Solo" €9-15** para o segmento side-gig (r/CRM thread).

### Próximo sprint (após Codex #C6)
4. **Auto-SKU generator** para stock (resolve dor #4 RepairQ). 1 dia de trabalho.
5. **Custom fields em equipamento** para o segmento IT-repair. 2-3 dias.

### Pós-beta
6. **Trade-ins** (segmento UK/US, opcional para PT)
7. **Integração com contabilidade PT** (TOConline / Sage / PHC) — feature de retenção
8. **Push notifications web** — fácil, alto-valor

### Confirmar / actualizar docs existentes
- `04-Roadmap-Detalhado.md` — adicionar Auto-SKU + Custom fields + Trade-ins
- `07-Pricing-Proposta.md` — confirmar pricing-por-tenant; considerar tier Solo
- `26-Brand-Design-System.md` — secção sobre rebrand urgente
- Memória — actualizar `project_lopestech_roadmap` com nota sobre rebrand acelerado

---

## Reflexão crítica (minha)

A descoberta do **conflict de nome com "RepairDesk" US** é a coisa mais importante deste batch. Bruno tem investido meses num nome que **não pode usar publicamente**. Quanto mais cedo rebrand, menos custo. Hoje o nome aparece em:
- Logo no Login + Layout
- README + landing
- Domain (eventual `repairdesk.lopestech.pt`)
- Documentos no PDF orçamento (footer "Gerado pelo RepairDesk")
- Portal cliente público (footer)

Trabalho de migração: ~2-3h se feito antes de mais branding. Após beta com clientes pagos? Custos de comunicação + URLs partidos.

**Recomendação forte:** rebrand antes do beta launch.
