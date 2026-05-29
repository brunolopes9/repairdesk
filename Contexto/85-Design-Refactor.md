# 85 — Refactor de Design + Dashboard rico (mockup Bruno 2026-05-27)

Bruno: "o design atual está horrível", quer refactor para ficar como o mockup — SaaS moderno,
dashboard rico, responsivo mobile/tablet/desktop, melhor UI/UX. Mockup mostra os 3 tamanhos.

## Visão (do mockup)
- **Shell**: sidebar navy escuro com ícones+label, secção loja no fundo, topbar com switcher de
  loja + pesquisa global (Ctrl K, já existe CommandPalette) + sino notificações + perfil.
- **Dashboard** denso e organizado em blocos:
  - **Linha KPI (6)**: Reparações em curso · Valor a receber · Stock crítico · Pedidos online ·
    Atrasadas (SLA) · Tempo médio reparação. Cada card com valor grande + delta "vs ontem".
  - **Fila operacional** (tabela): Prioridade (Alta/Média/Baixa) · ID/Estado · Cliente/Equipamento ·
    Técnico · Próxima ação (+ "há X min/h").
  - **Ritmo 7 dias**: Receita · Entregues · Lucro estimado · Tempo médio — cada um com sparkline + delta %.
  - **Ações rápidas** (Nova reparação, Abrir caixa, Importar fatura, Nova venda, Novo trabalho) +
    **Próximos passos** (orçamentos a enviar, follow-ups, trabalhos por concluir).
  - **Receita últimos 7 dias** (barras) · **Reparações por estado** (donut) · **Top produtos**.
  - **Rodapé stats**: Clientes ativos · Orçamentos pendentes · Trabalhos em execução · Resultado do dia.
- **Mobile/Tablet**: KPIs em grelha 2-col, blocos empilhados, bottom-nav (já existe).

## Dados: o que já existe vs novo
Backend já tem `DashboardService` + endpoints `/dashboard`, `/kpis-hoje`, `/financeiro`,
`/alertas`, `/tendencia`, `/top-reparacoes`. Logo a maioria dos blocos já tem dados.
**Novo/derivado a acrescentar** (provável endpoint `/dashboard/overview`):
- **Valor a receber** = Σ reparações Prontas/Entregues não pagas + orçamentos aprovados por cobrar.
- **Tempo médio reparação** = média (Entregue/Reparado − Recebido) últimos N dias (há time entries S349).
- **Atrasadas (SLA)** = NOVO conceito. Precisa de definição: prazo-alvo por estado ou dias em curso
  > X. v1 simples: reparações em curso há mais de N dias (config) = "atrasadas".
- **Fila operacional** = reparações ativas ordenadas por prioridade derivada (tempo no estado /
  atraso) + "próxima ação" derivada do estado (Orçamento→enviar; AguardaPeça→peça; Pronto→contactar).
  Prioridade/próxima-ação NÃO são campos — são derivados (não inventar entidades já).

## Sistema de design
- Manter Tailwind v4. Definir tokens consistentes: superfícies (card, borda, hover), raio (xl),
  sombras subtis, tipografia (números tabulares para KPIs), espaçamento. Sidebar navy já existe.
- Brand color já existe (brand-600 sky). Manter; afinar contraste dark mode.
- Componentes reutilizáveis: KpiCard (valor+label+delta+ícone+sparkline opcional), SectionCard,
  StatPill, SparklineMini. Já há KpiCard parcial (Recharts) — consolidar.

## Plano (sprints — cada um fechado e testável)
1. **S372 — Backend `/dashboard/overview`**: juntar num DTO os KPIs + fila operacional derivada +
   ritmo 7d (reutiliza financeiro/tendencia). Testar. (backend-only, sem risco de UI a meio.)
2. **S373 — Componentes de design**: KpiCard novo, SectionCard, SparklineMini, tokens. Isolados.
3. **S374 — Dashboard rebuild**: montar a página com o layout do mockup (grelha responsiva),
   consumindo /overview + endpoints existentes. Substitui a Dashboard.tsx atual.
4. **S375 — Polish responsivo + rollout** do design às páginas mais usadas (reparações, clientes).

## Notas honestas
- O switcher de "loja" no topo (Mender Lisboa ▼) implica multi-location, que NÃO existe (ver
  [[project_competitor_roadmap]]). v1: mostrar só o nome da loja sem dropdown funcional.
- Não é 1 sprint. Fazer por partes, cada uma completa — não meio-dashboard.
