# 84 — Análise de Gap: lista do concorrente vs Mender real

2026-05-27. Bruno despejou a matriz de features de um concorrente (estilo RepairDesk/Fixdesk/
Shopmonkey) e pediu "implementar em detalhe". Antes de codar, mapeia-se cada item contra o que
**já existe** no Mender. Legenda: ✅ feito · 🟡 parcial · ❌ novo.

## Reparações / Tickets
| Feature | Estado | Nota |
|---|---|---|
| Repair ticket management | ✅ | core do produto |
| Device repair history | ✅ | histórico por cliente/equipamento |
| History of tickets and services | ✅ | |
| Online estimate acceptance | ✅ | portal cliente aprova orçamento |
| E-Signature | ✅ | Sprint 344 signature pad |
| Kanban view | ✅ | /reparações tem vista kanban |
| Multiple repairs on an invoice | 🟡 | vendas/trabalhos agregam; faltam várias reparações numa fatura |
| Device auto-detection by IMEI | 🟡→❌ | IMEI valida (Luhn) + lookup interno; falta TAC→modelo automático |
| Online Booking / Scheduler / Appointments | ❌ | não existe agendamento |

## Inventário / Stock
| Feature | Estado | Nota |
|---|---|---|
| Inventory management | ✅ | Parts + Products + movimentos |
| Stock level control | ✅ | qtdMinima + alerta + push (S367) |
| Labels & barcodes / price tags | ✅ | Sprint 347 |
| Bundles / Kits | ✅ | Sprint 353 |
| Automatic price calculation | 🟡 | painel preço&lucro existe; auto-cálculo por margem não |
| Purchase orders | 🟡 | supplier-invoice import existe; PO formal não |
| Serial accounting | 🟡 | IMEI por VendaItem; contabilidade serial completa não |
| Stocktaking (inventário físico) | ❌ | |
| Bin locations | ❌ | precisa multi-location |
| Multiple warehouses / location-based tax/price | ❌ | precisa multi-location (não existe) |

## Clientes / CRM
| Feature | Estado | Nota |
|---|---|---|
| Client & lead management | ✅ | clientes + pedidos online = leads |
| Customer reviews (5 pontos) | ✅ | avaliação no portal |
| NPS / Like-Dislike | 🟡 | score 5-pontos feito; NPS/like-dislike não |
| Client tags | 🟡 | tags existem em reparações; tags de cliente não |
| Automatic notifications & reminders | ✅ | push (S365-367) + WhatsApp + email |
| Scheduled SMS / 2-way SMS | ❌ | sem provider SMS |

## Funcionários
| Feature | Estado | Nota |
|---|---|---|
| Roles / allowed actions | 🟡 | 4 roles definidas (Admin/Tech/Cashier/ReadOnly); só RequireAdmin aplicado |
| Clock in / out / timesheets | 🟡 | time tracker por reparação (S349); clock-in de turno não |
| Duty rosters / Payroll / Salary | ❌ | |
| Task manager | ❌ | |

## Finanças
| Feature | Estado | Nota |
|---|---|---|
| Cashboxes & tax management | ✅ | POS + caixa + Z-report (S300-304) |
| Invoices | ✅ | Moloni + InvoiceXpress (certificado PT) |
| P&L / reports / KPI dashboard | ✅ | relatórios negócio/IVA/produtividade |
| Prepayment / deposits / refunds | 🟡 | pagamentos parciais não formalizados |
| Multicurrency | ❌ | EUR-only (Portugal) |
| Ticket reports / lead conversion report | 🟡 | dados existem; relatórios dedicados não |

## Integrações
| Feature | Estado | Nota |
|---|---|---|
| API + webhooks | ✅ | external API + SDK TS + webhooks assinados |
| Marketplaces / online store | ✅ | external checkout + feed + ecommerce |
| Zapier / Make | 🟡 | n8n integrado; conectores nativos Zapier não |
| Email | ✅ | Sprint 348 |
| WhatsApp / Messenger | 🟡 | links WhatsApp; não 2-way; Messenger parcial |
| Payment gateways | 🟡 | IFTHENPAY (MBWay/Multibanco); Stripe/PayPal não |
| QuickBooks / Xero | ❌ | Moloni/InvoiceXpress cobrem PT (melhor cá) |

## AI
| Feature | Estado | Nota |
|---|---|---|
| Item recognition / OCR fatura | ✅ | Claude Vision (S164) + alt text (S166) |
| Product import | ✅ | CSV Molano + parser PDF fornecedor |
| Background removal | ❌ | |
| Call/voice transcripts, suggested replies | ❌ | |
| AI Receptionist / chatbot no site (Claude API) | ❌ | **ideia do Bruno — bom ROI marketing, barato** |

## Apps / POS
| Feature | Estado | Nota |
|---|---|---|
| PWA instalável (mobile + desktop) | ✅ | S194 + push S365-366 |
| Native iOS/Android apps | ❌ | PWA cobre 90% sem custo de loja de apps |

---

## Recomendação (opinião honesta, não "sim senhor")

**~55% desta lista já está feito.** Não reconstruir nada do ✅.

**NÃO perseguir paridade de 60 features sozinho.** O concorrente tem equipas. Metade da lista
serve segmentos que a LopesTech ainda não tem (multi-warehouse, payroll, multicurrency, native
apps, QuickBooks). Construir isso agora = meses a fazer features que ninguém te pede.

**Prioridade proposta (3 baldes):**

1. **Diferenciador (faz-te único):** IMEI auto-detect (TAC→modelo) + cruzar com BD de roubados
   (CheckMEND/PSP). É a tua ideia de [[project_imei_autoridades]] — ninguém no nicho PT tem isto.
2. **Tira-te fricção diária (dogfooding):** aplicar roles a sério (Tech/Cashier não veem tudo),
   appointments/booking online, prepayment/sinais.
3. **Marketing barato e rápido:** chatbot no site com Claude API (já tens a key + infra LLM).
   Atende leads 24/7, qualifica, cria pedido online. Alto valor percebido, baixo custo.

**Adiar até teres clientes a pedir:** payroll, multicurrency, multi-warehouse/bin locations,
native apps, QuickBooks/Xero, VoIP/2-way SMS, Stripe.

Decisão do Bruno: escolher 1-2 destes para a próxima sprint em vez de atacar a lista toda.
