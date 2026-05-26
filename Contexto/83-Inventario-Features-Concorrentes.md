# Doc 83 — Inventário features Mender vs concorrentes (Fixdesk, Repero)

Bruno fez braindump (2026-05-26) de features de 2 SaaS concorrentes que quer no Mender.
Este doc mapeia **o que já existe vs o que falta**, classificado por valor (P0/P1/P2)
e esforço aproximado. Serve como roadmap de longo prazo — não vai ser feito tudo de uma vez.

Legenda estado:
- ✅ Já existe e funciona
- 🟡 Existe mas precisa polish / UX cleanup
- ❌ Falta — preciso construir

Legenda prioridade:
- **P0** — Bruno usa-o pessoalmente OU bloqueia onboarding de tenant novo
- **P1** — alto valor, diferenciador, dá para fazer em 1 sprint
- **P2** — long tail, pós-beta

## Pillar 1 — POS & Pagamentos

| Feature | Estado | Onde / Falta | Prio |
|---|---|---|---|
| Vendas POS (carrinho + cobrança) | ✅ | `pages/vendas/Vendas.tsx`, Sprint 44/304 | — |
| Cash management (movimentos, abrir/fechar caixa) | ✅ | `CashService`, Sprint 300 | — |
| Daily closing + Z-report PDF | ✅ | Sprint 302 (QuestPDF) | — |
| MBWay + Multibanco via IFTHENPAY | ✅ | Sprint 303 (Fases A→D) | — |
| Multi-payment por invoice (dinheiro + cartão parcial) | ❌ | Entity `Payment` já existe mas 1:1 com Venda. Falta UI multi-payments | P1 |
| Card payments (terminal físico via Stripe Terminal / SumUp / Adyen) | ❌ | Stripe Terminal caro fora US; SumUp/MyPOS PT mais barato. Decisão futura. | P2 |
| Cash deposit no início + saldo no fim | ❌ | Multi-payment cobre | P1 |
| Certificação AT PT (DL 28/2019) | 🟡 | Delegado a Moloni/InvoiceXpress. Sem certificação própria do Mender. | P0 (pós-beta) |
| Daily sales overview | ✅ | Z-report inclui | — |
| Reconciliation automática | 🟡 | CashMovement regista, mas sem report reconciliação cartão↔caixa | P2 |

## Pillar 2 — Quotes / Orçamentos / Invoices

| Feature | Estado | Onde / Falta | Prio |
|---|---|---|---|
| Orçamento Moloni (PDF + número) | ✅ | `EmitirOrcamentoMoloni` | — |
| Orçamento próprio PDF | ✅ | `OrcamentoPdfService` | — |
| Fatura Moloni / InvoiceXpress | ✅ | `IBillingProvider` | — |
| Nota de Crédito + cancelar fatura | ✅ | Sprint 47/48 | — |
| Bulk emit faturas | ✅ | Sprint 49 | — |
| Customer accepts/declines quote via link no email | ❌ | Falta endpoint `/api/public/quote/{slug}/accept` + email template | P1 |
| Convert Quote → Invoice 1-click | 🟡 | Existe `converterOrcamentoEmFatura` mas sem promoção UI | P1 |
| Multiple invoices per repair | ❌ | 1:1 actual. Refactor grande. | P2 |
| Multiple quotes per repair (várias opções ao cliente) | ❌ | Diferenciador Repero. | P2 |
| Stand-alone invoices (sem reparação) | ✅ | Vendas existe | — |
| E-invoice ZUGFeRD (DE) | ❌ | Não aplicável PT. SAFT-PT export já existe. | — |

## Pillar 3 — Documentos & Comunicação Cliente

| Feature | Estado | Onde / Falta | Prio |
|---|---|---|---|
| Recibo PDF (venda) | ✅ | Sprint 81 | — |
| PDF Garantia digital | ✅ | Sprint 64 | — |
| Send orçamento/fatura por email (botão 1-click) | 🟡 | Backend existe (`BillingProvider` dispara), mas falta UI "Enviar agora" | P1 |
| Send por WhatsApp (pré-preencher mensagem) | 🟡 | `WhatsAppMenu` existe mas falta integração com PDFs como anexo | P1 |
| Send SMS para clientes (status update) | ❌ | Estrutura push existe (Sprint 147) mas não SMS. Vonage/Twilio integration. | P2 |
| Custom email templates per situação | ❌ | Diferenciador Repero. Falta CRUD templates + variáveis `{cliente_nome}` etc. | P1 |
| Schedule reminders future appointments | ❌ | Diferenciador Repero (manutenção 6m depois). | P2 |
| **Signature Pad** (cliente assina digital ao entregar/levantar) | ❌ | Esta sessão Sprint 344. | **P0** |

## Pillar 4 — Labels & Barcodes

| Feature | Estado | Onde / Falta | Prio |
|---|---|---|---|
| Label generator (peças, reparações, jobs) | ❌ | Diferenciador Fixdesk. QuestPDF + bwipjs (Code128/QR). | P1 |
| Print direct para impressora térmica | ❌ | Configurável per tenant. WebUSB ou ESC/POS via worker. | P1 |
| Barcode scanner support (USB) | 🟡 | Funciona em input fields HTML por defeito. Falta UI dedicada "scan to find" | P2 |

## Pillar 5 — Inventory melhorado

| Feature | Estado | Onde / Falta | Prio |
|---|---|---|---|
| Stock peças básico | ✅ | `pages/stock/Stock.tsx` | — |
| Bulk upload CSV | ✅ | Sprint 62/153 | — |
| Warehouse locations (onde está armazenado cada item) | ❌ | Multi-location é Pillar 7. Bin location é sub-feature. | P2 |
| Link serial numbers / IMEI a peças/componentes | 🟡 | IMEI existe em Venda/Reparacao mas não em PartMovimento | P2 |
| Inventory groups (bundles de materiais) | ❌ | Diferenciador Repero. Útil para "Kit reparação X" | P1 |
| Stock lookup por nome cliente | ❌ | Cross-search via reparações onde foi usado | P2 |
| Previsão reabastecimento IA | ✅ | Sprint 186 | — |

## Pillar 6 — Reparações & Workflows

| Feature | Estado | Onde / Falta | Prio |
|---|---|---|---|
| Estado custom per tenant | ❌ | Actualmente enum fixo. Bom diferenciador. | P1 |
| Status history log | ✅ | `ReparacaoEstadoLog` + Sprint 132 timeline | — |
| Repair tags (urgente, em-garantia, prateleira-3) | ❌ | Diferenciador Repero (versátil) | P1 |
| Checklists templates (passos por tipo reparação) | ❌ | Diferenciador Repero+Fixdesk | P1 |
| Photos & attachments | ✅ | `ReparacaoFoto` + R2 storage | — |
| Custom fields equipamento | ✅ | Sprint 122-123 EquipmentFieldTemplate | — |
| Automated workflows (trigger ao mudar estado) | 🟡 | Webhooks disparam mas sem if-then-else UI | P2 |
| RMA management (devoluções fornecedor) | ❌ | Já planeado Sprint futuro (memory garantia-fornecedor) | P1 |
| Pickup orders / delivery notes | ❌ | Workflow novo. PT chamado "Guia de Transporte". | P2 |
| Reminders/scheduling appointments | ❌ | Bookings — pillar grande | P2 |
| Tag co-workers em comments (@nome) | ❌ | Diferenciador Repero | P2 |
| Repair requests via website widget | 🟡 | `ExternalCheckoutController` existe para shop, mas não para repair intake | P1 |
| Convert Job → Quote → Invoice → RMA fluxo | 🟡 | Quote/Invoice OK, Job→RMA falta | P2 |

## Pillar 7 — Multi-tenant & Multi-location

| Feature | Estado | Onde / Falta | Prio |
|---|---|---|---|
| Multi-tenant isolation | ✅ | `ITenantContext`, RLS via `AppDbContext` global filter | — |
| Multi-location dentro do tenant (Viseu + Coimbra) | ❌ | Entity `Location` nova. Caixa + numeração + staff per location. Big. | P1 |
| Number ranges per location (FT-VS-001 vs FT-CB-001) | ❌ | Depende de Location | P2 |

## Pillar 8 — Employee management (NEW pillar)

| Feature | Estado | Onde / Falta | Prio |
|---|---|---|---|
| Roles granulares (Admin/Tech/Cashier/ReadOnly) | ✅ | Sprint 311 | — |
| Ownership reparação per user | ✅ | Sprint 343 (AssignedToUserId) | — |
| Time tracker per reparação (start/stop) | ❌ | Diferenciador Repero. Reusa `Reparacao.HorasGastas` mas falta tracker UI | P1 |
| Productividade per user (reparações/mês, lucro gerado) | ❌ | Relatório novo cruzando Reparacao + AssignedToUserId + Lucro | P1 |
| Cálculo horas + salários | ❌ | Caso BestCall.pt. Entity `WorkSession` + `Salary`. | P2 |
| Mensagens entre staff | ❌ | Chat interno. Big. | P2 |

## Pillar 9 — Reports / Export / Stats

| Feature | Estado | Onde / Falta | Prio |
|---|---|---|---|
| Dashboard KPIs | ✅ | `DashboardController` | — |
| Relatório IVA | ✅ | Sprint 53/178/182 | — |
| Estatísticas (faturas pagas/em aberto, encomendas) | 🟡 | Existe parcial em dashboard | P2 |
| Income & expenses tracking | ✅ | Despesas + Vendas + Dashboard | — |
| Export 500-1000 faturas em minutos | ✅ | Bulk emit Sprint 49 | — |
| CSV export | ✅ | Vendas, Reparações | — |
| Custom reports period | 🟡 | Filtros básicos. Falta date range arbitrário em todos | P2 |

## Pillar 10 — Onboarding / Migração

| Feature | Estado | Onde / Falta | Prio |
|---|---|---|---|
| Bulk upload CSV (clientes, peças) | ✅ | Sprint 62, 153 | — |
| Bulk upload reparações (de Repero/RepairShopr) | ❌ | Importer com mapping flexível. Útil para SaaS sales. | P1 |
| Date format configurável per tenant | ❌ | Diferenciador Repero (DD/MM vs MM/DD) | P2 |
| Multi-language (PT, EN, ES) | 🟡 | `i18n` parcial — UI tem strings PT hardcoded em muitos sítios | P2 |

## Pillar 11 — Customer Portal & Status Lookup

| Feature | Estado | Onde / Falta | Prio |
|---|---|---|---|
| Portal cliente público (slug) | ✅ | `/r/{slug}` + Sprint 88-98 | — |
| Cliente vê fotos da reparação | ✅ | Sprint 88 | — |
| Cliente vê histórico estado | ✅ | Timeline | — |
| Cliente "Reclamar garantia" | ✅ | Sprint 94 | — |
| Custom fields mostrados no portal | 🟡 | Existe estrutura. Falta toggle "mostrar/esconder ao cliente" | P2 |
| Repair request form embed (iframe website cliente final) | ❌ | Diferenciador Fixdesk+Repero | P1 |

## Pillar 12 — Misc & Polish

| Feature | Estado | Onde / Falta | Prio |
|---|---|---|---|
| Webhooks (parts.*, phones.*, vendas) | ✅ | Sprint 101-104 | — |
| Audit log rico + filtros + export | ✅ | Sprint #C20 | — |
| Backups + restore | ✅ | Sprint #C21, R2 backup | — |
| Sentry error tracking | ✅ | Sprint 250 | — |
| Mobile responsive | ✅ | Sprint 50, Sprint #C17 | — |

---

## Recomendação P0/P1 — próximos 3 sprints

**Sprint 344 (atual):** Signature Pad (this session)
**Sprint 345:** Bulk import reparações de Repero/RepairShopr (importer flexível)
**Sprint 346:** Repair Tags + Custom statuses
**Sprint 347:** Label & Barcode generator + print configurável
**Sprint 348:** Send 1-click (email + WhatsApp pré-preenchido com PDF)
**Sprint 349:** Time tracker per reparação + relatório produtividade

Sprints depois, em ordem de valor descendente:
- Multi-location infraestrutura
- Repair request form embed
- Inventory groups
- Custom email templates
- Schedule reminders future
- Tag co-workers em comments
- Salary/Worksession management (caso BestCall)
- Card payments terminal físico (decidir provider)
- Cliente accepts/declines quote via email link
- Multi-payment per invoice
- Convert Job → RMA fluxo
- Bin/warehouse locations
- Status custom per tenant

**Decisão estratégica:** muitas destas features SOZINHAS já bateriam concorrentes
em portugal — o pacote completo é um SaaS sério. Mas não vou tentar entregar tudo
de uma vez. **Sprint a sprint, validamos com tenants beta antes de avançar.**
