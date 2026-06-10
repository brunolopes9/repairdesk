# 93 — Gap-Analysis: Moloni → Mender (ERP completo)

> **Data:** 2026-06-10 · **Autor:** Claude (sessão faturação/ERP v0.2.44→64)
> **Pedido do Bruno:** "se eu quero que o Mender seja o meu ERP completo, single source of
> truth, diferenciado para o mercado, então tem que ter muitas funcionalidades que tem o
> Moloni" — colou o menu completo do painel Moloni + link da API (https://www.moloni.pt/dev/).
> Este doc é o mapa frio: o que o Moloni tem, o que o Mender já cobre, o que falta e o que
> **deliberadamente não vamos replicar**.

---

## 0. Princípio arquitetural (a decisão que governa tudo)

**O Moloni é o motor fiscal certificado (AT n.º 2860). O Mender é o ERP de gestão por cima.**

- **Fica SEMPRE no Moloni:** emissão certificada (FT/FS/FR/NC/RG), numeração e séries,
  comunicação automática à AT, SAF-T(PT), assinatura dos documentos. Replicar isto = certificação
  própria (€15-35k de dev + auditoria, ver Doc 89). Não é diferenciador — é commodity regulada.
- **Fica no Mender:** TUDO o resto — operação (reparações/POS/stock), gestão (clientes,
  fornecedores, devices, garantias), consultas (extratos, IVA, margens, pendentes), automação
  (imports, crons, push) e a ligação às vendas/loja online. O Mender chama a API Moloni e o
  utilizador nunca precisa de abrir o painel Moloni no dia-a-dia.
- **Teste de aceitação da visão:** o Bruno só abre o painel Moloni para configuração inicial
  (séries, ligação AT) e para o contabilista exportar SAF-T. Tudo o resto acontece no Mender.

---

## 1. Mapa por área do menu Moloni

Legenda estado: ✅ coberto · 🟡 parcial · ❌ gap · 🚫 não fazer (de propósito)

### 1.1 Painel Principal

| Moloni | Mender | Estado |
|---|---|---|
| Controlo de Tesouraria (recebido vs faturado) | Cash-flow chart (S429) + caixa/Z-report + dívida (S544) | 🟡 falta gráfico "recebido vs faturado" lado a lado |
| Diário de Faturação (volume s/ IVA por mês) | Relatório Negócio (receita, margens) | ✅ |
| Montante em Dívida | KPI + filtro + idade (v0.2.63) + push vencidas (v0.2.64) | ✅ **melhor que o Moloni** (push) |
| Produtos mais vendidos | — (só "top reparações lucrativas") | ❌ → ver §2.2 |

### 1.2 Tabelas (entidades, artigos, stocks)

| Moloni | Mender | Estado |
|---|---|---|
| Clientes | Ficha completa: NIF/VIES, morada, devices, comunicações, RGPD, KPIs | ✅ **muito melhor** |
| Fornecedores | Entity + regras aprendidas (stock/despesa/categoria), intra-UE, taxa defeito | ✅ **melhor** |
| Vendedores (comissões) | Multi-user com roles; sem comissões | 🚫 solo agora; reavaliar como pilar staff (Doc 80) |
| Artigos / Categorias | Parts + Products (modelo↔variante), grades, imagens SEO | ✅ **melhor** |
| Artigos fabricados | Kits de peças + bundles c/ labor (S353/S433) | ✅ equivalente |
| Armazéns (multi) | 1 localização (LocalArmazenamento por peça) | ❌ → pilar multi-location Fixdesk (Doc 80), NÃO agora |
| Tabelas de preços | PriceTableEntry | 🟡 chega para já |
| Controlo de stocks / Contagens | Stock + movimentos + Stock Takes c/ CSV (S421/S434) | ✅ |
| Importar XLS/CSV/SAFT | CSV Molano + PDF parser + LLM + OCR foto + email ingest | ✅ **muito melhor** |

### 1.3 Documentos

| Moloni | Mender | Estado |
|---|---|---|
| Faturas / Simplificadas / Faturas-Recibo | Emissão via API c/ escolha de tipo, IVA por linha, M13 margem | ✅ |
| Notas de Crédito | Auto-NC ao cancelar + documentCancel quando possível | ✅ |
| Recibos (liquidações) | Emitir + listar + ligar à origem + clear ao anular | ✅ |
| Notas de Débito | — | 🚫 caso de uso raríssimo; emitir no painel se um dia for preciso |
| **Guias de transporte/remessa** | — | ❌ → §2.4 (API Moloni tem; relevante quando a loja enviar volume) |
| Orçamentos | Orçamento Moloni unificado + re-emitir + converter | ✅ |
| Faturas Pro forma / Consignação | — | 🚫 sem caso de uso |
| **Avenças (faturação recorrente)** | Só DESPESAS recorrentes; faturação a clientes não | ❌ → **#1 do §2** |
| Consultas de mesa (restauração) | — | 🚫 não é o mercado |
| Docs de fornecedores (registar compras) | Imports c/ parser + aprovação stock/despesa + ledger | ✅ **melhor** |
| Pedidos de garantia | RMA fornecedor + garantias DL 84/2021 + PDF + portal | ✅ **muito melhor** |

### 1.4 Consultas (o coração do pedido do Bruno)

| Moloni | Mender | Estado |
|---|---|---|
| Extrato de Vendas / Compras | **Extrato unificado PDF** (v0.2.61): vendas+compras+despesas num doc | ✅ **melhor** (unificado) |
| Análise de Vendas (por artigo/cliente) | Negócio (receita/margens) + top reparações | 🟡 → §2.2 |
| Margens de Lucro | Lucro bruto vs operacional + por reparação + margem 2ª mão | ✅ |
| Mapas de IVA | Relatório IVA trimestral c/ dedutível auto, margem M13, IVA exato, drill-down, PDF/CSV | ✅ **muito melhor** |
| Listagem de pendentes (vendas) | Em dívida (v0.2.63/64) | ✅ |
| Listagem de pendentes (compras) | — (compras pagas na hora; sem contas-correntes de fornecedor) | 🚫 até haver crédito de fornecedor real |
| Histórico de Clientes | Ficha cliente (gasto total, reparações, devices, comunicações) | ✅ |
| Histórico de Fornecedores | Espalhado: hub compras + taxa defeito + imports | 🟡 → §2.3 |
| Mapa de encontro do IVA | IVA liquidado vs dedutível vs a entregar/crédito | ✅ |
| Mapa de Retenções | — | 🚫 Bruno não tem retenções na fonte |

### 1.5 POS

| Moloni | Mender | Estado |
|---|---|---|
| POS + movimentos caixa | POS Vendas + CashMovement + fecho dia + Z-report PDF + MBWay | ✅ |
| Multibanco refs | IFTHENPAY (⚠️ MB preso a sandbox — não expor sem contrato prod) | 🟡 conhecido |
| Lojas e terminais (multi) | 1 loja | ❌ → pilar multi-location (Doc 80), futuro SaaS |
| Mesas / Moloni Orders / Display / slideshows | — | 🚫 restauração, não é o mercado |

### 1.6 A. Tributária + Configurações + Marketplace

| Moloni | Mender | Estado |
|---|---|---|
| Comunicação AT / estado docs / séries | Fica no Moloni (princípio §0) | 🚫 por design |
| SAF-T(PT) / inventário existências | Fica no Moloni; contabilista exporta lá | 🚫 por design |
| Impostos/taxas, séries, templates doc | Auto-descoberta de IDs no Mender; gestão no Moloni | ✅ como deve ser |
| Métodos pagamento / prazos vencimento | Métodos no POS; prazos implícitos (30d dívida) | 🟡 prazo por cliente um dia? baixa |
| Mensagens SMS | WhatsApp + Email c/ templates por estado | ✅ **melhor** (sem custo SMS) |
| Assinaturas digitais (docs) | — (assinatura na ENTREGA é outra coisa: pilar signature pads Doc 80) | ❌ média → já planeado no Doc 80 |
| EDI / Marketplace / Apps | — | 🚫 sem caso de uso |

---

## 2. Recomendações priorizadas (o que construir, por ordem)

### #1 ALTA — Faturação recorrente / avenças (o gap mais valioso)
O Bruno está a entrar em **software com mensalidade** (manutenção de sites, SaaS). Hoje cada
mês teria de criar o Trabalho e emitir FT à mão. Proposta enxuta:
- `Avenca` (ClienteId, Descricao, ValorCents, IvaRate, Periodicidade, ProximaEmissao, Ativa)
- Cron mensal: cria o Trabalho + emite a FT via Moloni (ou, modo conservador, cria e manda
  push "avença pronta a emitir — 1 clique") → entra direto no ciclo dívida→recibo já live.
- UI: secção "Avenças" na ficha do cliente + lista própria. **Esforço M (2-3 sprints).**
- Nota: começar em modo "push 1-clique" e só passar a emissão 100% automática depois de o
  Bruno confiar (emissão fiscal automática sem olhos = risco que ele deve aceitar explicitamente).

### #2 ALTA — Análise de Vendas: produtos mais vendidos + por cliente
Já temos os dados (VendaItem/Reparações). Página/secção em Relatórios: top artigos por
quantidade e por margem, top clientes por receita, no período. **Esforço S (1 sprint).**
Fecha o último cartão do Painel Moloni que falta.

### #3 MÉDIA — Histórico de Fornecedor consolidado
Uma vista por fornecedor: compras (imports aprovados), total gasto, taxa de defeito, regras
aprendidas, intra-UE. Tudo já existe espalhado — é agregação. **Esforço S-M.**

### #4 MÉDIA — Guias de transporte via API Moloni
Obrigatórias quando transportas mercadoria (AT). Relevante quando a loja online despachar
volume a sério. API Moloni suporta (`billsOfLading`/`transportGuides`). **Esforço M.**
Gatilho: primeiras vendas online com envio regular.

### #5 MÉDIA — Assinatura digital na entrega (pilar Doc 80)
Cliente assina no ecrã ao levantar a reparação → anexa ao recibo de entrega PDF. Já está
no plano Fixdesk (Doc 80) — manter lá a prioridade.

### #6 BAIXA — Tesouraria "recebido vs faturado" no Dashboard
Gráfico mensal com 2 séries (faturado = docs emitidos; recebido = FR/FS + recibos). Dados
já existem na lista única. **Esforço S.** Cosmético-útil.

### 🚫 Não fazer (e porquê)
SAF-T próprio e comunicação AT (certificação — commodity, fica no Moloni) · restauração
(mesas/orders/display) · EDI · marketplace · retenções · IVA de caixa · comissões de
vendedores (até haver staff de vendas) · multi-armazém AGORA (é o pilar multi-location do
Doc 80 — fazê-lo nessa sequência, não isolado) · Notas de Débito.

---

## 3. Estado da paridade (resumo executivo)

Das ~40 funcionalidades do menu Moloni com relevância para o negócio:
- **✅ cobertas ou melhores no Mender: ~26** (clientes, fornecedores, artigos, stocks,
  imports, todos os docs de venda do dia-a-dia, recibos, NC, orçamentos, extratos, IVA,
  margens, pendentes+push, POS+caixa, comunicações)
- **🟡 parciais: 4** (análise de vendas, histórico fornecedor, tesouraria gráfico, tabelas preço)
- **❌ gaps reais: 4** (avenças ★, guias de transporte, assinaturas, multi-location/terminais)
- **🚫 deliberadamente fora: ~10** (fiscal certificado + nichos que não são o mercado)

**Leitura honesta:** o Mender já É o ERP do dia-a-dia — em consultas e automação está à
FRENTE do Moloni (extrato unificado, IVA com margem/exato, push de dívida, imports com IA).
O que falta é pouco e está priorizado. A "paridade total" com o menu Moloni não é o objetivo
certo: o objetivo é o Bruno (e os tenants futuros) nunca precisarem de abrir o painel.

> Próximo passo sugerido: #1 Avenças (modo push 1-clique) — decide e digo CONTINUA.
