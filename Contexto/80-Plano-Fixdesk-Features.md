# 80 — Plano: features FixDesk → Mender

**Data:** 2026-05-24
**Origem:** Bruno analisou fixdesk.de e quer construir features deles no Mender.
**Status:** Análise + plano. **Aguarda aprovação Bruno antes de qualquer sprint.**

---

## TL;DR

FixDesk tem ~6 grandes blocos de funcionalidade que Mender ainda não tem completos. Cerca de **60%** do que ele mostrou **já existe** em Mender (jobs, invoices, inventory, webhooks, push, audit, portal cliente, etc). Os outros **40%** são features novas mas a maior parte é executável em iterações curtas.

**O grande missing** não são features individuais — é o **package fiscal PT** (POS UI próprio + SAF-T + payment provider), **labels/print/sign hardware-adjacent**, e **multi-location + staff management** (este último é módulo grande, quase um produto à parte).

---

## Mapeamento: o que FixDesk tem vs Mender

✅ Pronto · 🟡 Parcial · 🔴 Falta

### Repair Management
| FixDesk | Mender | Notas |
|---|---|---|
| Capture jobs | ✅ | Reparações + Trabalhos |
| Quotes / Estimates | ✅ | Sprint 67 + Moloni orçamentos |
| Real-time status | ✅ | Sprint 88 portal + Sprint 147 push |
| Team assignment | 🔴 | Existe `UserId` em audit mas não atribuição estruturada |
| Customer notifications | ✅ | WhatsApp + Push + Email (Sprint 227, 173) |
| Device history | ✅ | Sprint 65, 87 (IMEI lookup, ligação venda) |
| Photos & attachments | ✅ | FotosReparacao (Sprint 64+) |
| Checklists | ✅ | DiagnosticoGuiado (Sprint 128, 142) |
| Automated workflows | 🟡 | Auto-NC, garantia automática (TenantPreferences Sprint 234). Falta: triggers |
| Archive | ✅ | Soft-delete via filters |
| Barcodes & labels | 🔴 | **NEW** — Pillar B |
| Job search + filter | ✅ | Sprint 142 + paginated |
| Manage locations | 🔴 | **NEW** — Pillar C |
| Device management | ✅ | Parts + Products |
| Export jobs | ✅ | Sprint 86 CSV |
| Team comments | 🔴 | **NEW** parcial — falta UI de discussão por reparação |
| RMA / returns | 🟡 | Sprint 110-115 garantia fornecedor; falta UI de gestão B2B |
| Access rights | 🟡 | Admin role hoje; granular adiado Sprint #342 |
| Scheduling (marcações) | 🔴 | **NEW** — dashboard "Próximas Marcações" + agendamento online |
| Dashboard | ✅ | Recharts + 5+ widgets |
| Error log | ✅ | Audit log + Sentry (Sprint 250) |
| Widgets | 🟡 | Dashboard tem widgets; falta widget para website do cliente — Pillar E |

### Accounting & Finance
| FixDesk | Mender | Notas |
|---|---|---|
| Create invoices | ✅ | Moloni + InvoiceXpress (Sprint 144, 164) |
| E-invoices (ZUGFeRD DE) | 🟡 | PT equiv = SAF-T XML — Moloni já faz. Falta documento e-FT directo |
| Tax export | ✅ | Sprint 52, 148 export CSV/ZIP |
| Income & expenses | ✅ | Sprint 176 Stock/COGS/OpEx + Despesas |
| Reports & statistics | ✅ | Relatorios IVA + Negocio + Defeito (Sprint 187) |
| Verify payments | 🔴 | **NEW** — conciliação bancária via Open Banking PT? |
| Customers & suppliers | ✅ | Clientes + Fornecedores (Sprint 120) |
| GoBD compliant (DE) | 🟡 | PT equiv = SAF-T. Sprint 174 + DL 28/2019. Moloni cumpre. Sem certificação própria |
| Audit-proof archive | ✅ | Audit log + Sprint 175 retention |

### POS & Checkout
| FixDesk | Mender | Notas |
|---|---|---|
| POS system | 🟡 | Vendas/POS UI existe mas é "envia para Moloni faturar" — sem fluxo de caixa |
| Card payments | 🔴 | **NEW** — Pillar A. Stripe vs IFTHENPAY |
| Cash payments | 🟡 | Marca paga em dinheiro mas sem caixa diária |
| TSE option (DE) | N/A | PT não tem TSE; tem SAF-T. Já compliant via Moloni |
| Fast checkout | 🟡 | POS existe mas pode acelerar |
| Digital receipts | ✅ | Recibo PDF + envio email (Sprint 68) |
| Daily sales overview | 🟡 | Dashboard tem KPIs mas não "fecho de caixa" |
| Automatic reconciliation | 🔴 | **NEW** — depende Pillar A |
| Receipt printing | 🔴 | **NEW** — Pillar B printer integration |
| Daily closing (fecho caixa) | 🔴 | **NEW** — Pillar A |

### Miscellaneous
| FixDesk | Mender | Notas |
|---|---|---|
| Signature pads | 🔴 | **NEW** — Pillar B |
| Batch jobs | ✅ | Sprint 49 bulk emit, Sprint 181 bulk approve |

### Bruno extras (não em FixDesk explicitamente)
| Item | Estado |
|---|---|
| Send buttons unified (Email/WhatsApp/SMS) | 🟡 — WhatsApp ✓, Email partial, SMS 🔴 |
| Importar dados de FixDesk/outros | 🔴 — **NEW**, importer CSV genérico |
| Website widget embed JS | 🔴 — **NEW**, Pillar E |
| Rental devices | 🔴 — **NEW**, Pillar F |
| Form builder (edit forms) | 🟡 — EquipmentFieldTemplate Sprint 128 cobre 80% |
| Staff/employee management | 🔴 — **NEW**, Pillar D (módulo grande) |

---

## Pillars de implementação

### Pillar A — POS PT + Payments (4-6 semanas)
**Objectivo:** caixa registadora completa com payment provider PT, fecho diário, conciliação.

**Sprints sugeridas:**
- 300. POS UI cash drawer + fecho diário (DailyClosing entity)
- 301. CashMovement entity (entrada/saída de caixa, sangria)
- 302. Payment provider selector: IFTHENPAY (PT, MBWay+Multibanco) vs Stripe vs Manual
- 303. Webhook payment confirmation → marcar venda paga automaticamente
- 304. Daily closing PDF — Z-report PT compliant
- 305. SAF-T re-validation flow (já gerado via Moloni, só confirmar)

**Decisão pendente Bruno:**
- **Provider preferido?** IFTHENPAY (PT, ~1%+IVA por transacção, MBWay) é a recomendação. Stripe (~1.4%+0.25€) é caro para tickets pequenos mas universal.

### Pillar B — Labels/Print/Sign hardware-adjacent (2-3 semanas)
**Objectivo:** etiquetas, códigos, impressão directa, assinatura digital.

**Sprints sugeridas:**
- 310. SignaturePad component (canvas HTML5) + storage anexo à reparação
- 311. Barcode/QR generator (lib `bwip-js` ou `qrcode`) + templates devices/parts/jobs
- 312. PrintNode integration (cloud printing, free 50 prints/mês, ~5€/mês prod)
- 313. Print templates configuráveis per-tenant (orçamento, fatura, label, recibo)
- 314. Print queue + dashboard "Última impressão"

**Decisão pendente:** PrintNode (cloud, mais simples) vs CUPS local (mais complexo, no Hetzner VPS).

### Pillar C — Multi-location (3-4 semanas)
**Objectivo:** um tenant pode ter várias lojas físicas, cada uma com a sua caixa + numbering.

**Sprints sugeridas:**
- 320. Entity Location (Code, Nome, NIF override, Endereço, FK Tenant)
- 321. Migration: todos os recursos existentes com `LocationId NULL`. Default location auto-criada para LopesTech
- 322. Reparações/Vendas/Trabalhos têm `LocationId` opcional
- 323. Number ranges per location (prefix `A`, `B`, `C` no número fatura)
- 324. UI Definições > Locations (CRUD)
- 325. Filtros dashboard + relatórios per location

### Pillar D — Staff management (6-8 semanas — módulo grande)
**Objectivo:** o tenant gere empregados, horas, salário, produtividade. Inspirado no caso BestCall.pt.

**Sprints sugeridas:**
- 330. Entity Employee (Nome, Email, Phone, Status, Role, FK Location, FK AppUser opcional)
- 331. TimeClock entity (ClockIn, ClockOut, EmployeeId) + UI mobile-friendly para picar
- 332. Reparações com `AssignedEmployeeId` opcional
- 333. Produtividade per employee (reparações fechadas, € facturado, horas)
- 334. Salário config (€/h ou fixo mensal) + cálculo mensal
- 335. Internal messaging — comments em reparações já existe, mas falta chat 1-1 ou canal team
- 336. UI Definições > Equipa (CRUD employees + ver produtividade)
- 337. Dashboard widget "Equipa" — quem está clock-in, alertas se faltar

**Decisão pendente Bruno:**
- **Standalone module ou Mender for Teams?** Vender como add-on (€10/funcionário/mês) ou incluir no tier Pro?
- **Mensagens internas:** built-in vs integração Slack/Discord?

### Pillar E — Customer-facing acquisition (3-4 semanas)
**Objectivo:** trazer clientes para o tenant — embed widget no site dele + scheduling online.

**Sprints sugeridas:**
- 340. Website widget — script JS `<script src="mender.pt/widget/{tenant-code}.js">` que injecta botão "Pedir orçamento" no site do tenant
- 341. Endpoint público `POST /api/public/widget/intake` (rate-limited) cria pre-job
- 342. Approval queue no admin — tenant aprova/rejeita pedidos
- 343. Scheduling — Cliente final marca data/hora; admin vê calendar
- 344. Dashboard "Próximas Marcações" widget

### Pillar F — Polish operacional (2-3 semanas)
**Objectivo:** fechar gaps menores.

**Sprints sugeridas:**
- 350. RentalDevice entity (equipamento que tenant empresta enquanto repara o do cliente)
- 351. Send unified — modal "Enviar para cliente" com escolha Email/WhatsApp/SMS
- 352. SMS provider integration (Twilio ou MessageBird — €0.05-0.07/SMS PT)
- 353. Form builder mais flexível (já há EquipmentFieldTemplate; falta tipos: dropdown, date, file)
- 354. Importer de FixDesk/outros (CSV genérico com mapping fuzzy via Claude)
- 355. RMA management UI (Sprint 110+ tem entity, falta workflow)

---

## Sequenciamento recomendado

**Premissa:** Bruno quer vender a outras oficinas PT, não só LopesTech. O que dá **mais valor de venda** primeiro:

### Fase Alpha (3 meses, ~12 semanas)
**Foco:** completar core para vender beta paga a 3-5 oficinas PT.

1. **Pillar A — POS PT + Payments** (4-6 sem) — sem isto, não é vendável como POS
2. **Pillar B — Labels/Print/Sign** (2-3 sem) — diferenciação visível, vendendor adora demos
3. **Pillar E.1 — Send unified Email/WhatsApp/SMS** (~1 sem) — quick win, alta percepção

### Fase Beta (3 meses, ~12 semanas)
**Foco:** completar oferta multi-loja.

4. **Pillar C — Multi-location** (3-4 sem) — abre mercado de oficinas com 2+ lojas
5. **Pillar F — RMA management + Rental devices** (2-3 sem) — operacional
6. **Pillar E — Website widget + scheduling** (3-4 sem) — aquisição clientes para os tenants

### Fase Pro (futuro, depende do mercado)
**Foco:** subir ticket médio com add-on de staff management.

7. **Pillar D — Staff management** (6-8 sem) — vender como add-on Pro/Enterprise (€20-40/mês extra)

### Items adiados deliberadamente
- E-invoices ZUGFeRD nativo (Moloni já cobre)
- Verify payments / Open Banking PT (esperar 2027 quando PSD3 + SEPA estiver mais maduro)
- TSE (não aplicável PT)
- GoBD certification (não aplicável PT — temos DL 28/2019 já coberto via Moloni)

---

## Decisões para Bruno aprovar antes de eu começar

1. **Sequência:** aprovas a ordem A → B → C → F → E → D? Ou queres trocar?
2. **Payment provider:** IFTHENPAY (PT, MBWay+MB) ou Stripe (global, caro) ou ambos?
3. **PrintNode (cloud) ou CUPS local** para impressão?
4. **Staff management** é add-on pago ou incluído no tier Pro existente (Sprint 167b)?
5. **SMS provider:** Twilio (mais barato global), MessageBird (mais barato EU), ou nenhum por agora?
6. **Importer de FixDesk:** vale o esforço se o objectivo é vender a oficinas que NÃO usam FixDesk (concorrente)? Talvez genérico CSV chegue.
7. **Stake fiscal:** queres avançar com **certificação AT própria** (€15-35k dev, 3-6 meses) ou continuar via Moloni para sempre? (Memory `feedback_certificacao_fiscal_pt`)

---

## Estimativa total

| Fase | Semanas | Custo dev (se Codex+Claude a tempo inteiro) |
|---|---|---|
| Alpha (A + B + E.1) | 7-10 sem | ~70-100h Bruno revisão |
| Beta (C + F + E) | 8-11 sem | ~80-110h Bruno revisão |
| Pro (D) | 6-8 sem | ~60-80h Bruno revisão |
| **Total** | **21-29 sem (~6 meses)** | |

Realidade: provavelmente 9-12 meses com pausas para sales, dogfooding, fixes. **Não recomendo paralelizar tudo** — uma fase de cada vez, lançar a clientes, recolher feedback.

---

## Sugestões adicionais minhas

1. **Reparações como Job entity unificada.** FixDesk usa "Job" para tudo. Mender tem `Reparacao` + `Trabalho`. Considerar merge num futuro (refactor grande) — ou aceitar dualidade.

2. **Mender for Teams como produto separado.** Pillar D pode crescer para um produto autónomo (oficinas + call centers + qualquer SMB com empregados). Não inviável.

3. **Website widget é canal de aquisição para os tenants — e venda direta para ti.** O widget é gratuito mas exibe "Powered by Mender" → marketing viral.

4. **POS PT mobile-first.** Tablet na bancada com câmara para barcode + signature pad. Mais usável que desktop.

5. **AT Portal Direct API** (a longo prazo): em vez de via Moloni, integrar directamente — economia recorrente de €13-30/mês por tenant. Mas é 3-6 meses dev e certificação obrigatória. Vale a pena só depois de 50+ tenants pagantes.

[[project-mender-fixdesk-inspiration]] [[project-lopestech-roadmap]] [[feedback-certificacao-fiscal-pt]]
