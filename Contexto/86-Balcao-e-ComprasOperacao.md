# 86 — Refactor IA: "Balcão" e "Compras e Operação" (Bruno 2026-05-27)

Refs no disco: `RepairDesk/POS e Vendas.png` (Balcão) e `RepairDesk/Compras e Operação.png`.
Objetivo: deixar de ter páginas-lista soltas e passar a **centros operacionais** ricos.

## ✅ Sprint 381 (feito): reestruturação da navegação
Sidebar agrupa (dropdowns), mantendo as páginas existentes como filhos:
- **Balcão** → Venda rápida (/vendas) · Caixa de hoje (/cash)
- **Compras e Operação** → Inbox de faturas (/compras) · Despesas & custos (/despesas)
Páginas ricas unificadas = próximos sprints (abaixo).

## Balcão (unificar /vendas POS + /cash) — próximo
Layout da ref: tabs **Venda rápida · Caixa de hoje · Movimentos · Fecho/Z-Report**.
- **Venda rápida**: grelha de produtos (esq) + carrinho (centro: itens, cliente, total grande,
  métodos de pagamento, botão verde **Cobrar**) + painel direito **Caixa de hoje** (saldos por
  método, previsto vs real) + ações rápidas (reforço, sangria).
- **REGRA**: bloquear venda se caixa fechada → ecrã "Abre a caixa para vender hoje" + botão abrir.
  (CashMovement/DailyClosing já existem — S300-304. Verificar estado da caixa do dia antes do POS.)
- **Movimentos**: tabela vendas + reforços + sangrias + despesas de caixa.
- **Fecho/Z-Report**: fecho diário, diferenças, export PDF (já existe Z-report S302).
- Rota nova `/balcao` (default Venda rápida) ou manter /vendas + /cash como tabs internas.

## Compras e Operação (unificar /despesas + /compras + importações) — próximo
Layout da ref: header + **4 KPIs** (A pagar · Compras · Recorrentes · Custos op.) + tabs
**Inbox de faturas · Compras aprovadas · Custos operacionais · Recorrentes · Export**.
- **Inbox de faturas**: o que chega por IMAP/upload/foto e precisa de decisão (= SupplierInvoiceImport
  pendentes, /compras?tab=pending). Linhas com fornecedor/data/valor/estado + ações Aprovar /
  Aprovar como stock (já existe a lógica S148/160).
- **Compras aprovadas**: faturas que entraram em stock/material.
- **Custos operacionais**: renda, luz, software, comunicações (Despesa categoria OpEx).
- **Recorrentes**: custos fixos previsíveis (Despesa recorrente — S308 DespesaRecorrente existe).
- **Export contabilista**: painel sempre acessível (CSV — já existe).
- Coluna direita: **Ações rápidas** (Nova fatura/despesa, Importar) · **Alertas** (a pagar,
  recorrentes a vencer) · **Resumo financeiro** (totais).

## Notas
- Dados/lógica em grande parte JÁ EXISTEM (despesas, compras/importações, caixa, Z-report,
  recorrentes). É sobretudo **reorganizar em UI rica** com as primitivas (Card/KpiCard/SectionCard).
- Sessão atual em 21 sprints — fazer cada ecrã como sprint própria, completo, não a meio.
