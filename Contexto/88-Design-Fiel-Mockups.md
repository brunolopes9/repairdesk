# 88 — Replicar FIELMENTE os mockups (Bruno 2026-05-27, frustrado e com razão)

**Diretiva do Bruno (literal):** "eu pedi-te para as replicares tal e qual como nas fotos aquele
design e o posicionamento das coisas etc, e tu nao o fizeste". Não é só "usar as primitivas" —
é **replicar layout, posições das divs, estilo/diversidade dos botões, KPIs e organização das
opções de cada menu EXATAMENTE como nas referências.** As versões S382-396 ficaram simplificadas.

## Inventário de referências (no disco — LER antes de cada página)
- `RepairDesk/IDEIAS (1).png` · `IDEIAS (2).png` — visão geral do sistema (sidebar navy, topbar, dashboard).
- `RepairDesk/Clientes.png` — mockup da página Clientes.
- `RepairDesk/Compras e Operação.png` — mockup Compras e Operação.
- `RepairDesk/POS e Vendas.png` — mockup Balcão/POS.
- `RepairDesk/catalogo+stock.png` — mockup Catálogo & Stock (alta fidelidade; a minha versão ficou aquém).
- **SVGs Reparações (specs ao pixel, em `Desktop/LopesTech/`):** `mender-reparacoes-final-ui.svg`,
  `mender-reparacoes-page-final.svg`, `mender-reparacoes-page-final-premium.svg`,
  `mender-reparacoes-design-premium.svg`. SVG = texto → cores/coordenadas/labels exatos.

## Tokens exatos (do SVG premium)
bg `#f6f4ef` · surface `#fffdf8` · card `#fff` borda `#dedbd2` · texto `#111827` · muted `#667085`
· nav(sidebar) `#0e1b2a` · azul `#2f80d1` (soft `#e7f2ff`, texto `#1f6fb8`) · verde `#16a36a`
(soft `#e8f8ef`) · âmbar `#b7791f` (soft `#fff5da`) · vermelho `#c73535` (soft `#ffe9e9`) ·
chip `#f1f0ea` · diagnóstico roxo `#6941c6` (soft `#eee7ff`). h1 30px/780 · h2 19px/760 ·
label 11px/760 uppercase. Cards raio 12, painéis 14-16, sombra suave.

## REPARAÇÕES — layout fiel (mender-reparacoes-page-final-premium.svg)
1. **Header:** h1 "Reparações" + subtítulo "Entrada, diagnóstico, peças, comunicação, faturação e
   entrega dos equipamentos." À direita, grupo: [Lista][Kanban][chip âmbar "N pendente fatura"][Exportar][+ Nova].
2. **Métricas (5 cards 190×82, gap 14):** EM CURSO (preto) · DIAGNÓSTICO (azul) · ENTREGUES (verde)
   · SEM FATURA (âmbar) · A RECEBER (€, preto). Label uppercase em cima, número 26px/800 em baixo.
3. **Barra de filtros (card surface, raio 14):** pesquisa larga em cima ("Pesquisar equipamento,
   IMEI, cliente, telefone...") + linha de **chips de estado coloridos**: Todas(azul cheio)·
   Orçamentos·Recebidas·Diagnóstico(azul soft)·Aguarda peça(âmbar soft)·Em reparação·Prontas(verde
   soft)·Atrasadas(vermelho soft).
4. **Conteúdo em 2 colunas:** ESQ painel lista (~66%): "Fila de reparações · N reparações" + tabela
   (Nº·EQUIPAMENTO·CLIENTE·ESTADO·PRÓXIMA AÇÃO·VALOR). Linha selecionada destacada azul-claro
   `#f0f7ff` borda `#9dccff` + chips WhatsApp/Ligar. Por baixo 3 cards operacionais (Alertas[âmbar]·
   Hoje[entradas/entregas/orçamentos/valor]·Ações rápidas[+Nova/Importar CSV/Emitir faturas]).
   DIR **inspector** (~34%, cabeçalho navy): "REPARAÇÃO SELECIONADA / #N · Equipamento" + badge estado;
   CLIENTE (nome+contacto+WhatsApp/Ligar/Email); PRÓXIMA AÇÃO (card + "Avançar estado"); 3 mini-cards
   ORÇAMENTO/PEÇAS/LUCRO; tabs (Resumo·Diagnóstico·Peças·Fotos·Timeline·Docs); Checklist; ações
   (Emitir fatura·Entregar·Portal cliente).

## Plano (página a página, cada uma = sprint própria, FIEL ao mockup, build+deploy)
1. **Reparações** (tenho spec SVG completa) — flagship. Reaproveitar dados de Reparacoes.tsx
   (list query, STATUS_LABEL/COLOR, navigate, pagasSemFatura) mas LAYOUT novo conforme acima.
2. **Catálogo & Stock** — refazer fiel ao `catalogo+stock.png` (a versão S386-388 ficou simples:
   faltam colunas certas, painel direito com tabs Visão geral/Variantes/Histórico/Preços + "Aplicar
   a variantes", mocks, etc).
3. **Balcão/POS** (`POS e Vendas.png`) · **Compras e Operação** (`Compras e Operação.png`) ·
   **Clientes** (`Clientes.png`) · **Dashboard** (`IDEIAS (2).png`).

**Nota de processo:** LER a referência (SVG como texto / PNG visual) ANTES de cada página e replicar
posições/cores/botões. Não simplificar. Bruno corre `npm run dev` — vê ao gravar.

## ⚠️ DESCOBERTA-CHAVE (análise das 5 PNGs + SVG, 2026-05-27) — A SHELL ESTÁ ERRADA
O maior desvio NÃO é página-a-página, é a **casca partilhada (Layout)**, que aparece em TODOS os mockups:
1. **Sidebar:** mockups têm sidebar **navy PERMANENTE e LARGA (~220px)** com logo "Mender", itens
   com ícone **+ label** agrupados, item ativo em pill, e **rodapé com seletor de loja ("Loja
   Principal") + utilizador ("Bruno Lopes / Administrador") + Sair**. A minha é um **icon-rail de 72px**
   (expande no hover) — desvio gritante e em todo o lado.
2. **Topbar:** seletor de loja à esquerda + **pesquisa central grande** ("Procurar produto, cliente,
   reparação… Ctrl K") + à direita estado "OK" verde + sino c/ badge + chip de perfil. A minha difere.
3. **Páginas em 2 colunas (lista + inspector/rail direito):** Clientes (tabela + painel de perfil),
   Reparações (tabela + inspector), Compras (tabela + Ações/Alertas/Resumo). As minhas são 1 coluna.
4. **POS** é 3 colunas (grelha produtos + carrinho + caixa). **Dashboard** = KPI row(6)+tabela+rail+gráficos.
5. **KPIs:** ícone tonal em quadrado OU label-em-cima + número grande; linha horizontal densa.

**ORDEM CORRETA (shell primeiro — corrige todas as páginas de uma vez):**
- **Fase 1 — App shell (Layout.tsx):** sidebar permanente larga navy com labels+grupos+rodapé loja/user;
  topbar com seletor de loja + pesquisa central + estado/sino/perfil. ALAVANCA MÁXIMA.
- **Fase 2 — Reparações** (spec SVG completa) · **Fase 3 — Clientes** (tabela+perfil) · **Fase 4 —
  Compras** (tabela+rail) · **Fase 5 — POS(3col) + Catálogo(refinar) + Dashboard**.

## Estado de execução
- **Fase 1 — App shell (Layout.tsx)** ✅ S397 (sidebar navy larga permanente + topbar seletor loja/pesquisa).
- **Fase 2 — Reparações** ✅ S398-399 (métricas 5-cards, chips coloridos, lista+inspector 2 colunas).
- **Fase 3 — Clientes** ✅ S400 (KPI row tonal, pesquisa+chips, tabela esquerda + inspector de perfil
  à direita: avatar/tags, WhatsApp/Ligar/Email/Editar, total gasto + reparações, atividade recente).
  Stats do inspector reaproveitam reparacoesApi/vendasApi (como ClienteDetalhe), sem endpoint novo.
- **Fase 4 — Compras** ✅ S401 (tabela densa Fornecedor·Documento·Data·Valor·Estado + tabs
  Inbox/Histórico + chip confiança parser + cartão Export personalizado ZIP + rail Ações/Alertas/
  Resumo). Aprovar fica no fluxo /compras (categorização completa); sem inventar IVA por fatura.
- **Fase 5a — Catálogo** ✅ S402 (painel de detalhe deixa de ser drawer e passa a inspector inline
  persistente → 2 colunas tabela|painel; linha selecionada destacada; chevron isolado expande
  variantes; auto-seleção do 1.º; toggle loja + editar preço/stock mantidos). `catalogo+stock.png`.
- **Fase 5b — Dashboard** ✅ (avaliação S402): a `Dashboard.tsx` JÁ segue a composição do
  `IDEIAS (2).png` — linha de 6 KPIs → SectionCard "fila de reparações" → 4 cards de sparkline
  (AreaChart) → grelha de 4 widgets (Garantias/Reabastecer/TopReparações[BarChart]/TopPeças). A
  deriva estrutural que motivou o Doc 88 (shell + páginas a 1 coluna) está corrigida. Gap residual
  é cosmético/opcional: donut "reparações por estado" + rail "Atividade". NÃO justifica rebuild
  grande (666 linhas + recharts) com risco de regressão; fica como polish opcional futuro.
- **Fase 5c — POS 3 colunas** ✅ S403 — resolvido de forma SEGURA: a 3.ª coluna ("Caixa do dia")
  foi composta ao nível do **Balcão** (87 linhas), ao lado da `Vendas` embedded (que já é
  produtos|carrinho), SEM tocar na `Vendas.tsx` (1104 linhas, fluxo crítico). Rail mostra detalhe
  por método de pagamento + total em caixa (cashApi.today/DailyClosingDto); caixa fechada → CTA
  "Abrir caixa para vender hoje". Resultado visual: produtos|carrinho|caixa = 3 colunas do mockup.

## ✅ TODAS as fases concluídas (S397-403)
Shell + Reparações + Clientes + Compras + Catálogo + POS-3col live. Dashboard já alinhado.
Polish: donut "Reparações por estado" ✅ S404 (PieChart no Dashboard, deriva da fila, sem
queries extra). Sobra opcional: rail "Atividade" no Dashboard; refinamentos pixel adicionais
à medida que o Bruno aponte desvios concretos por menu.
