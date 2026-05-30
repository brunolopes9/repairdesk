import { useMemo, type ReactNode } from 'react';
import { Link } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import {
  AlertTriangle,
  ArrowRight,
  Boxes,
  CalendarClock,
  CheckCircle2,
  ClipboardList,
  Clock3,
  Euro,
  Inbox,
  ListTodo,
  MessageCircle,
  PackageSearch,
  ShieldCheck,
  ShoppingBag,
  Trophy,
  TrendingUp,
  Wrench,
  type LucideIcon,
} from 'lucide-react';
import {
  Area,
  AreaChart,
  Bar,
  BarChart,
  Cell,
  Pie,
  PieChart,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts';
import { stockApi } from '../lib/stock/api';
import { dashboardApi } from '../lib/dashboard/api';
import { useDashboardKpisHoje } from '../lib/dashboard/hooks';
import { reparacoesApi } from '../lib/reparacoes/api';
import { vendasApi } from '../lib/vendas/api';
import { REPAIR_STATUS, STATUS_LABEL, type RepairStatus } from '../lib/reparacoes/types';
import { internalTasksApi } from '../lib/internalTasks/api';
import { InternalTaskStatus, type InternalTask } from '../lib/internalTasks/types';
import { liveListOptions } from '../lib/queryOptions';
import { formatCents, formatDateOnly } from '../lib/money';
import { EmptyState, PageHeader, Skeleton, KpiCard, SectionCard } from '../components/ui';

type Tone = 'blue' | 'emerald' | 'amber' | 'rose' | 'zinc';

const toneClass: Record<Tone, { border: string; icon: string; soft: string; text: string; chart: string }> = {
  blue: {
    border: 'border-blue-200 hover:border-blue-300 dark:border-blue-900/70 dark:hover:border-blue-800',
    icon: 'bg-blue-100 text-blue-700 dark:bg-blue-950 dark:text-blue-300',
    soft: 'bg-blue-50 text-blue-700 dark:bg-blue-950/40 dark:text-blue-300',
    text: 'text-blue-700 dark:text-blue-300',
    chart: '#2563eb',
  },
  emerald: {
    border: 'border-emerald-200 hover:border-emerald-300 dark:border-emerald-900/70 dark:hover:border-emerald-800',
    icon: 'bg-emerald-100 text-emerald-700 dark:bg-emerald-950 dark:text-emerald-300',
    soft: 'bg-emerald-50 text-emerald-700 dark:bg-emerald-950/40 dark:text-emerald-300',
    text: 'text-emerald-700 dark:text-emerald-300',
    chart: '#059669',
  },
  amber: {
    border: 'border-amber-200 hover:border-amber-300 dark:border-amber-900/70 dark:hover:border-amber-800',
    icon: 'bg-amber-100 text-amber-700 dark:bg-amber-950 dark:text-amber-300',
    soft: 'bg-amber-50 text-amber-700 dark:bg-amber-950/40 dark:text-amber-300',
    text: 'text-amber-700 dark:text-amber-300',
    chart: '#d97706',
  },
  rose: {
    border: 'border-rose-200 hover:border-rose-300 dark:border-rose-900/70 dark:hover:border-rose-800',
    icon: 'bg-rose-100 text-rose-700 dark:bg-rose-950 dark:text-rose-300',
    soft: 'bg-rose-50 text-rose-700 dark:bg-rose-950/40 dark:text-rose-300',
    text: 'text-rose-700 dark:text-rose-300',
    chart: '#e11d48',
  },
  zinc: {
    border: 'border-zinc-200 hover:border-zinc-300 dark:border-zinc-800 dark:hover:border-zinc-700',
    icon: 'bg-zinc-100 text-zinc-700 dark:bg-zinc-800 dark:text-zinc-300',
    soft: 'bg-zinc-100 text-zinc-700 dark:bg-zinc-800 dark:text-zinc-300',
    text: 'text-zinc-700 dark:text-zinc-300',
    chart: '#52525b',
  },
};

// Cores (hex) por estado de reparação — para o donut do pipeline. Roxo do diagnóstico = token design.
const ESTADO_HEX: Record<RepairStatus, string> = {
  0: '#2563eb', // Recebido
  1: '#6941c6', // Diagnóstico
  2: '#d97706', // Aguarda peça
  3: '#0891b2', // Em reparação
  4: '#059669', // Reparado
  5: '#52525b', // Entregue
  6: '#9f1239', // Cancelado
  7: '#7c3aed', // Orçamento
};

export default function Dashboard() {
  const hojeIso = useMemo(() => new Date().toISOString().slice(0, 10), []);
  const kpis = useDashboardKpisHoje(hojeIso);

  const garantias = useQuery({
    queryKey: ['dashboard-garantias-resumo-v2'],
    queryFn: () => dashboardApi.garantiasResumo(30, 5),
    staleTime: 5 * 60_000,
  });

  const reabastecer = useQuery({
    queryKey: ['parts-reabastecer-30d-v2'],
    queryFn: () => stockApi.reabastecerSugestoes(30),
    staleTime: 5 * 60_000,
  });

  // Sprint 377: fila operacional — reparações ativas (não Entregue/Cancelado), ao vivo.
  const fila = useQuery({
    queryKey: ['dashboard-fila-operacional'],
    queryFn: () => reparacoesApi.list({ pageSize: 50 }),
    ...liveListOptions,
  });

  const recentVendas = useQuery({
    queryKey: ['dashboard-recent-vendas'],
    queryFn: () => vendasApi.list({ pageSize: 10 }),
    staleTime: 60_000,
  });

  // Sprint 410: faturas pendentes (reparações pagas sem fatura Moloni) — alimenta "Alertas importantes".
  const pagasSemFatura = useQuery({
    queryKey: ['dashboard-pagas-sem-fatura'],
    queryFn: () => reparacoesApi.listPagasSemFatura(100),
    staleTime: 60_000,
  });

  // Sprint 412 (IDEIAS 1): receita por categoria — donut com dados reais do backend (mês corrente).
  const dashboardCurrent = useQuery({
    queryKey: ['dashboard-current'],
    queryFn: () => dashboardApi.current(),
    staleTime: 5 * 60_000,
  });

  // Sprint 423 (Doc 90): tarefas pendentes para widget.
  const tarefasPendentes = useQuery({
    queryKey: ['dashboard-tarefas-pendentes'],
    queryFn: () => internalTasksApi.list({ status: InternalTaskStatus.Pendente }),
    staleTime: 60_000,
  });

  // Sprint 429 (Doc 88 IDEIAS 1): cash flow diário 30d.
  const cashflow = useQuery({
    queryKey: ['dashboard-cashflow-30d'],
    queryFn: () => dashboardApi.cashflow(30),
    staleTime: 5 * 60_000,
  });

  // Sprint 431 (Doc 90): contagens dos crons S392 + S428 + S430 para "Alertas importantes".
  const alertasQuery = useQuery({
    queryKey: ['dashboard-alertas-v2'],
    queryFn: () => dashboardApi.alertas(),
    staleTime: 60_000,
  });
  // Sprint 460 (Doc 91 follow-up): reparações em estado comunicável sem outbound (cron S458).
  const avisosPendentesQuery = useQuery({
    queryKey: ['dashboard-avisos-pendentes'],
    queryFn: () => dashboardApi.avisosPendentes(8, 20),
    staleTime: 60_000,
  });
  const avisosPendentesCount = avisosPendentesQuery.data?.totalCount ?? 0;
  // Sprint 467: Devices com garantia fabricante a expirar (cross-sell oportunidade).
  const garantiaFabricanteQuery = useQuery({
    queryKey: ['dashboard-devices-garantia-expirar'],
    queryFn: () => dashboardApi.devicesGarantiaAExpirar(30, 30),
    staleTime: 5 * 60_000,
  });
  const garantiaFabricanteCount = garantiaFabricanteQuery.data?.totalCount ?? 0;
  const STALLED_DAYS = 5; // alinhado com S392 default StalledRepairs:Days
  const reparacoesParadasCount = useMemo(() => {
    const cutoff = Date.now() - STALLED_DAYS * 86_400_000;
    return (fila.data?.items ?? []).filter(
      (r) =>
        r.estado !== REPAIR_STATUS.Entregue &&
        r.estado !== REPAIR_STATUS.Cancelado &&
        new Date(r.estadoSince).getTime() < cutoff,
    ).length;
  }, [fila.data]);
  const tarefasAtrasadasCount = useMemo(() => {
    const now = Date.now();
    return (tarefasPendentes.data ?? []).filter((t) => t.dueAt && new Date(t.dueAt).getTime() < now).length;
  }, [tarefasPendentes.data]);
  const cobrancasEmAtrasoCount =
    (alertasQuery.data?.reparacoesNaoPagas?.length ?? 0) + (alertasQuery.data?.trabalhosNaoPagos?.length ?? 0);
  // Sprint 441: reparações em Pronto há +READY_DAYS (cliente não veio buscar).
  // Alinhado com cron ReadyForPickup:Days (default 5). Conta apenas a partir da fila atual.
  const READY_DAYS = 5;
  const porLevantarCount = useMemo(() => {
    const cutoff = Date.now() - READY_DAYS * 86_400_000;
    return (fila.data?.items ?? []).filter(
      (r) => r.estado === REPAIR_STATUS.Pronto && new Date(r.estadoSince).getTime() < cutoff,
    ).length;
  }, [fila.data]);
  const cashflowData = useMemo(() => {
    return (cashflow.data?.days ?? []).map((d) => ({
      label: new Date(d.date).toLocaleDateString('pt-PT', { day: '2-digit', month: '2-digit' }),
      receita: d.receitaCents / 100,
      despesa: d.despesaCents / 100,
      net: d.netCents / 100,
    }));
  }, [cashflow.data]);
  const cashflowTotal = useMemo(() => {
    const days = cashflow.data?.days ?? [];
    return {
      receita: days.reduce((s, d) => s + d.receitaCents, 0),
      despesa: days.reduce((s, d) => s + d.despesaCents, 0),
      net: days.reduce((s, d) => s + d.netCents, 0),
    };
  }, [cashflow.data]);

  // Sprint 423 (Doc 90): reparações com ETA esta semana — filtra client-side da fila.
  const reparacoesEtaSemana = useMemo(() => {
    const items = fila.data?.items ?? [];
    const now = new Date();
    const limite = new Date(now);
    limite.setDate(limite.getDate() + 7);
    return items
      .filter((r) => r.previstoEntregueEm && r.estado !== REPAIR_STATUS.Entregue && r.estado !== REPAIR_STATUS.Cancelado)
      .filter((r) => {
        const eta = new Date(r.previstoEntregueEm!);
        return eta <= limite; // inclui atrasadas (eta < now) e até +7 dias
      })
      .sort((a, b) => new Date(a.previstoEntregueEm!).getTime() - new Date(b.previstoEntregueEm!).getTime());
  }, [fila.data]);
  const RECEITA_HEX = ['#2563eb', '#059669', '#d97706', '#7c3aed', '#0891b2', '#c73535', '#52525b'];
  const receitaData = (dashboardCurrent.data?.receitaPorCategoria ?? []).map((c, i) => ({
    name: c.label, value: c.totalCents, color: RECEITA_HEX[i % RECEITA_HEX.length],
  }));
  const receitaTotal = receitaData.reduce((s, d) => s + d.value, 0);
  const filaItems = useMemo(
    () =>
      (fila.data?.items ?? [])
        .filter((r) => r.estado !== REPAIR_STATUS.Entregue && r.estado !== REPAIR_STATUS.Cancelado)
        .sort((a, b) => new Date(a.estadoSince).getTime() - new Date(b.estadoSince).getTime())
        .slice(0, 8),
    [fila.data],
  );

  // Donut "reparações por estado" — pipeline atual (ativas), derivado da mesma fila (sem queries extra).
  const estadoBreakdown = useMemo(() => {
    const ativos = (fila.data?.items ?? []).filter(
      (r) => r.estado !== REPAIR_STATUS.Entregue && r.estado !== REPAIR_STATUS.Cancelado,
    );
    const counts = new Map<RepairStatus, number>();
    for (const r of ativos) counts.set(r.estado, (counts.get(r.estado) ?? 0) + 1);
    const ordem: RepairStatus[] = [
      REPAIR_STATUS.Orcamento, REPAIR_STATUS.Recebido, REPAIR_STATUS.Diagnostico,
      REPAIR_STATUS.AguardaPeca, REPAIR_STATUS.EmReparacao, REPAIR_STATUS.Pronto,
    ];
    return ordem
      .filter((e) => (counts.get(e) ?? 0) > 0)
      .map((e) => ({ estado: e, name: STATUS_LABEL[e], value: counts.get(e) ?? 0, color: ESTADO_HEX[e] }));
  }, [fila.data]);
  const totalAtivos = estadoBreakdown.reduce((s, d) => s + d.value, 0);

  // Atividade recente — últimas entradas de reparação + últimas vendas, num só feed.
  const atividade = useMemo(() => {
    type Item = { key: string; date: string; kind: 'repair' | 'sale'; title: string; sub: string; value: number; href: string };
    const reps: Item[] = (fila.data?.items ?? []).map((r) => ({
      key: `r-${r.id}`, date: r.recebidoEm, kind: 'repair', title: `Reparação #${r.numero}`,
      sub: [r.equipamento, r.cliente?.nome].filter(Boolean).join(' · '),
      value: r.precoFinalCents ?? r.orcamentoCents ?? 0, href: `/reparacoes/${r.id}`,
    }));
    const vds: Item[] = (recentVendas.data?.items ?? []).map((v) => ({
      key: `v-${v.id}`, date: v.data, kind: 'sale', title: `Venda #${v.numero}`,
      sub: v.cliente?.nome ?? 'Cliente final', value: v.totalCents, href: `/vendas/${v.id}`,
    }));
    return [...reps, ...vds].sort((a, b) => (a.date < b.date ? 1 : -1)).slice(0, 7);
  }, [fila.data, recentVendas.data]);

  const sparklineData = useMemo(() => {
    const values = kpis.data?.receita7d ?? Array.from({ length: 7 }, () => 0);
    const start = new Date(`${hojeIso}T00:00:00.000Z`);
    start.setUTCDate(start.getUTCDate() - 6);
    return values.map((value, index) => {
      const day = new Date(start);
      day.setUTCDate(start.getUTCDate() + index);
      return {
        dia: day.toLocaleDateString('pt-PT', { weekday: 'short' }),
        valor: value,
      };
    });
  }, [hojeIso, kpis.data?.receita7d]);

  const receita7dTotal = (kpis.data?.receita7d ?? []).reduce((sum, value) => sum + value, 0);
  const hasOperationalAlert =
    (kpis.data?.valorAReceberCents ?? 0) > 0 ||
    (kpis.data?.stockCriticoCount ?? 0) > 0 ||
    (garantias.data?.expiramEm30Dias ?? 0) > 0 ||
    (reabastecer.data?.length ?? 0) > 0;

  return (
    <div className="space-y-8">
      <PageHeader
        title="Dashboard"
        description={`Operacao diaria da oficina - ${new Date().toLocaleDateString('pt-PT', {
          weekday: 'long',
          day: 'numeric',
          month: 'long',
        })}`}
      />

      {kpis.isError && (
        <div className="rounded-lg border border-rose-200 bg-rose-50 px-3 py-2 text-sm text-rose-700 dark:border-rose-900 dark:bg-rose-950/40 dark:text-rose-300">
          Nao foi possivel carregar os KPIs operacionais.
        </div>
      )}

      <section className="space-y-3">
        <ZoneHeader
          eyebrow="Hoje"
          title="O que precisa de movimento agora"
          subtitle="Entrada, cobranca e stock critico. Fiscal fica nos relatorios."
        />
        <div className="grid grid-cols-2 gap-3 md:grid-cols-3 xl:grid-cols-6">
          <Link to="/reparacoes" className="block transition hover:-translate-y-0.5">
            <KpiCard icon={Wrench} tone="brand" label="Reparações em curso"
              value={String(kpis.data?.reparacoesEmCurso ?? 0)} sub="abertas" />
          </Link>
          <Link to="/reparacoes?estado=Entregue&pagamento=NaoPago" className="block transition hover:-translate-y-0.5">
            <KpiCard icon={Euro} tone="emerald" label="Valor a receber"
              value={formatCents(kpis.data?.valorAReceberCents)} />
          </Link>
          <Link to="/stock?lowStock=1" className="block transition hover:-translate-y-0.5">
            <KpiCard icon={AlertTriangle} tone={(kpis.data?.stockCriticoCount ?? 0) > 0 ? 'red' : 'zinc'}
              label="Stock crítico" value={String(kpis.data?.stockCriticoCount ?? 0)} sub="peças" />
          </Link>
          <Link to="/reparacoes?estado=Entregue" className="block transition hover:-translate-y-0.5">
            <KpiCard icon={CheckCircle2} tone="brand" label="Entregues (7d)"
              value={String(kpis.data?.reparacoesEntregues7d ?? 0)} />
          </Link>
          <Link to="/relatorios/negocio" className="block transition hover:-translate-y-0.5">
            <KpiCard icon={Trophy} tone={(kpis.data?.lucroEstimado7dCents ?? 0) >= 0 ? 'amber' : 'red'}
              label="Lucro estimado (7d)" value={formatCents(kpis.data?.lucroEstimado7dCents)} />
          </Link>
          <Link to="/relatorios/produtividade" className="block transition hover:-translate-y-0.5">
            <KpiCard icon={Clock3} tone="zinc" label="Tempo médio"
              value={formatHours(kpis.data?.tempoMedioReparacaoHoras)} />
          </Link>
        </div>
      </section>

      <section className="space-y-3">
        <ZoneHeader
          eyebrow="Operação"
          title="Fila operacional"
          subtitle="Reparações ativas por ordem de espera — o que precisa de ação a seguir."
        />
        <SectionCard
          action={<Link to="/reparacoes" className="text-xs font-medium text-brand-600 hover:underline dark:text-brand-400">Ver todas →</Link>}
          bodyClassName="p-0"
        >
          {fila.isLoading ? (
            <div className="space-y-2 p-4">{Array.from({ length: 5 }).map((_, i) => <Skeleton key={i} className="h-12 rounded-lg" />)}</div>
          ) : filaItems.length === 0 ? (
            <div className="p-8"><EmptyState title="Sem reparações ativas" description="Quando houver reparações em curso aparecem aqui por ordem de prioridade." /></div>
          ) : (
            <ul className="divide-y divide-zinc-100 dark:divide-zinc-800">
              {filaItems.map((r) => {
                const prio = prioridade(r.estadoSince);
                return (
                  <li key={r.id}>
                    <Link to={`/reparacoes/${r.id}`} className="flex items-center gap-3 px-4 py-2.5 transition hover:bg-zinc-50 dark:hover:bg-zinc-800/50">
                      <span className={`hidden w-16 flex-none rounded-md px-2 py-0.5 text-center text-[11px] font-semibold sm:inline ${prio.cls}`}>{prio.label}</span>
                      <span className="w-28 flex-none truncate text-xs text-zinc-400">#{r.numero} · {estadoLabel(r.estado)}</span>
                      <span className="min-w-0 flex-1">
                        <span className="block truncate text-sm font-medium">{r.cliente?.nome ?? '—'}</span>
                        <span className="block truncate text-xs text-zinc-500">{r.equipamento}</span>
                      </span>
                      <span className="hidden w-40 flex-none truncate text-xs text-zinc-600 dark:text-zinc-300 md:inline">{proximaAccao(r.estado)}</span>
                      <span className="w-16 flex-none text-right text-[11px] text-zinc-400">{tempoDesde(r.estadoSince)}</span>
                    </Link>
                  </li>
                );
              })}
            </ul>
          )}
        </SectionCard>
      </section>

      <section className="space-y-3">
        <ZoneHeader
          eyebrow="Esta semana"
          title="Ritmo dos ultimos 7 dias"
          subtitle="So operacao: receita realizada, entregas, lucro estimado e tempo medio."
        />
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 md:grid-cols-3 xl:grid-cols-4">
          <WeeklyCard
            to="/relatorios/negocio"
            icon={TrendingUp}
            tone="emerald"
            label="Receita 7d"
            value={formatCents(receita7dTotal)}
            loading={kpis.isLoading}
          >
            <div className="mt-3 h-20">
              <ResponsiveContainer width="100%" height="100%">
                <AreaChart data={sparklineData} margin={{ left: 0, right: 0, top: 6, bottom: 0 }}>
                  <defs>
                    <linearGradient id="receita7d" x1="0" x2="0" y1="0" y2="1">
                      <stop offset="0%" stopColor={toneClass.emerald.chart} stopOpacity={0.35} />
                      <stop offset="100%" stopColor={toneClass.emerald.chart} stopOpacity={0.02} />
                    </linearGradient>
                  </defs>
                  <Tooltip formatter={(value) => formatCents(Number(value))} labelFormatter={(label) => `${label}`} />
                  <Area
                    type="monotone"
                    dataKey="valor"
                    stroke={toneClass.emerald.chart}
                    strokeWidth={2}
                    fill="url(#receita7d)"
                    dot={{ r: 2 }}
                    activeDot={{ r: 4 }}
                  />
                </AreaChart>
              </ResponsiveContainer>
            </div>
          </WeeklyCard>

          <WeeklyCard
            to="/reparacoes?estado=Entregue"
            icon={CheckCircle2}
            tone="blue"
            label="Reparacoes entregues 7d"
            value={kpis.data?.reparacoesEntregues7d}
            suffix="entregues"
            loading={kpis.isLoading}
          />

          <WeeklyCard
            to="/relatorios/negocio"
            icon={Trophy}
            tone={(kpis.data?.lucroEstimado7dCents ?? 0) >= 0 ? 'amber' : 'rose'}
            label="Lucro estimado 7d"
            value={formatCents(kpis.data?.lucroEstimado7dCents)}
            loading={kpis.isLoading}
            helper="Receita menos pecas consumidas e OpEx puro."
          />

          <WeeklyCard
            to="/reparacoes"
            icon={Clock3}
            tone="zinc"
            label="Tempo medio reparacao"
            value={formatHours(kpis.data?.tempoMedioReparacaoHoras)}
            loading={kpis.isLoading}
            helper="Da ficha criada ate Entregue, nos ultimos 7 dias."
          />
        </div>
      </section>

      <section className="space-y-3">
        <ZoneHeader
          eyebrow="Pipeline"
          title="Reparações por estado"
          subtitle="Distribuição das reparações em curso e últimos eventos da loja."
        />
        <div className="grid gap-3 lg:grid-cols-2 xl:grid-cols-4">
        <SectionCard title="Receita por categoria">
          {dashboardCurrent.isLoading ? (
            <div className="h-[220px] animate-pulse rounded-lg bg-zinc-100 dark:bg-zinc-800" />
          ) : receitaTotal === 0 ? (
            <EmptyState icon={Euro} title="Sem receita este mês" description="Quando entrar a primeira reparação paga ou venda, aparece aqui a distribuição." />
          ) : (
            <div className="space-y-3">
              <div className="relative h-[180px]">
                <ResponsiveContainer width="100%" height="100%">
                  <PieChart>
                    <Pie data={receitaData} dataKey="value" nameKey="name" innerRadius={50} outerRadius={75} paddingAngle={2} stroke="none">
                      {receitaData.map((d) => <Cell key={d.name} fill={d.color} />)}
                    </Pie>
                    <Tooltip formatter={(v, n) => [formatCents(Number(v)), String(n)]} />
                  </PieChart>
                </ResponsiveContainer>
                <div className="pointer-events-none absolute inset-0 flex flex-col items-center justify-center">
                  <span className="text-lg font-bold tabular-nums">{formatCents(receitaTotal)}</span>
                  <span className="text-[10px] text-zinc-500">mês corrente</span>
                </div>
              </div>
              <ul className="space-y-1">
                {receitaData.map((d) => (
                  <li key={d.name} className="flex items-center gap-2 text-xs">
                    <span className="h-2 w-2 flex-none rounded-full" style={{ backgroundColor: d.color }} />
                    <span className="flex-1 truncate">{d.name}</span>
                    <span className="tabular-nums font-medium">{formatCents(d.value)}</span>
                    <span className="w-10 text-right text-zinc-400">{Math.round((d.value / receitaTotal) * 100)}%</span>
                  </li>
                ))}
              </ul>
            </div>
          )}
        </SectionCard>

        <SectionCard title="Reparações por estado">
          {fila.isLoading ? (
            <div className="h-[220px] animate-pulse rounded-lg bg-zinc-100 dark:bg-zinc-800" />
          ) : totalAtivos === 0 ? (
            <EmptyState icon={Wrench} title="Sem reparações em curso" description="Quando entrarem equipamentos, vês aqui a distribuição por estado." />
          ) : (
            <div className="grid gap-4 sm:grid-cols-[220px_1fr] sm:items-center">
              <div className="relative h-[220px]">
                <ResponsiveContainer width="100%" height="100%">
                  <PieChart>
                    <Pie data={estadoBreakdown} dataKey="value" nameKey="name" innerRadius={62} outerRadius={92} paddingAngle={2} stroke="none">
                      {estadoBreakdown.map((d) => <Cell key={d.estado} fill={d.color} />)}
                    </Pie>
                    <Tooltip formatter={(v, n) => [`${v} reparações`, String(n)]} />
                  </PieChart>
                </ResponsiveContainer>
                <div className="pointer-events-none absolute inset-0 flex flex-col items-center justify-center">
                  <span className="text-2xl font-bold tabular-nums">{totalAtivos}</span>
                  <span className="text-[11px] text-zinc-500">em curso</span>
                </div>
              </div>
              <ul className="space-y-1.5">
                {estadoBreakdown.map((d) => (
                  <li key={d.estado}>
                    <Link to="/reparacoes" className="flex items-center gap-2.5 rounded-md px-1 py-1 text-sm transition hover:bg-zinc-50 dark:hover:bg-zinc-800/50">
                      <span className="h-2.5 w-2.5 flex-none rounded-full" style={{ backgroundColor: d.color }} />
                      <span className="flex-1">{d.name}</span>
                      <span className="tabular-nums font-medium">{d.value}</span>
                      <span className="w-12 text-right text-xs text-zinc-400">{Math.round((d.value / totalAtivos) * 100)}%</span>
                    </Link>
                  </li>
                ))}
              </ul>
            </div>
          )}
        </SectionCard>

        <SectionCard title="Atividade recente">
          {fila.isLoading || recentVendas.isLoading ? (
            <div className="space-y-2">{Array.from({ length: 5 }).map((_, i) => <div key={i} className="h-12 animate-pulse rounded-lg bg-zinc-100 dark:bg-zinc-800" />)}</div>
          ) : atividade.length === 0 ? (
            <p className="py-6 text-center text-sm text-zinc-500">Sem atividade recente.</p>
          ) : (
            <ul className="space-y-0.5">
              {atividade.map((a) => (
                <li key={a.key}>
                  <Link to={a.href} className="flex items-center gap-2.5 rounded-lg px-1.5 py-2 transition hover:bg-zinc-50 dark:hover:bg-zinc-800/50">
                    <span className={`grid h-8 w-8 flex-none place-items-center rounded-full ${a.kind === 'repair' ? 'bg-sky-100 text-sky-700 dark:bg-sky-950/50 dark:text-sky-300' : 'bg-emerald-100 text-emerald-700 dark:bg-emerald-950/50 dark:text-emerald-300'}`}>
                      {a.kind === 'repair' ? <Wrench size={14} /> : <ShoppingBag size={14} />}
                    </span>
                    <span className="min-w-0 flex-1">
                      <span className="block truncate text-sm font-medium">{a.title}</span>
                      <span className="block truncate text-[11px] text-zinc-400">{a.sub} · {formatDateOnly(a.date)}</span>
                    </span>
                    <span className="flex-none text-xs font-semibold tabular-nums">{formatCents(a.value)}</span>
                  </Link>
                </li>
              ))}
            </ul>
          )}
        </SectionCard>

        <SectionCard title="Alertas importantes">
          {garantias.isLoading || reabastecer.isLoading || pagasSemFatura.isLoading ? (
            <p className="text-sm text-zinc-400">A carregar…</p>
          ) : (() => {
            const expiramGar = garantias.data?.expiramEm30Dias ?? 0;
            const stockCritico = kpis.data?.stockCriticoCount ?? 0;
            const faturasPend = pagasSemFatura.data?.length ?? 0;
            // Sprint 431: novos sinais (alinhados com os crons S392/S428/S430).
            const totalAlertas = expiramGar + stockCritico + faturasPend
              + reparacoesParadasCount + tarefasAtrasadasCount + cobrancasEmAtrasoCount + porLevantarCount
              + avisosPendentesCount + garantiaFabricanteCount;
            if (totalAlertas === 0) return <p className="py-6 text-center text-sm text-zinc-500">Sem alertas — tudo em dia ✓</p>;
            return (
              <ul className="space-y-2">
                {expiramGar > 0 && (
                  <li>
                    <Link to="/reparacoes" className="flex items-center gap-2.5 rounded-lg bg-amber-50 px-2.5 py-2 text-sm transition hover:bg-amber-100 dark:bg-amber-950/40 dark:hover:bg-amber-950/60">
                      <span className="grid h-8 w-8 flex-none place-items-center rounded-full bg-amber-100 text-amber-700 dark:bg-amber-900/60 dark:text-amber-300"><ShieldCheck size={14} /></span>
                      <span className="min-w-0 flex-1">
                        <span className="block font-medium text-amber-900 dark:text-amber-100">{expiramGar} garantia{expiramGar === 1 ? '' : 's'} a expirar</span>
                        <span className="block text-[11px] text-amber-700/80 dark:text-amber-300/80">Próximos 30 dias</span>
                      </span>
                    </Link>
                  </li>
                )}
                {stockCritico > 0 && (
                  <li>
                    <Link to="/stock?lowStockOnly=1" className="flex items-center gap-2.5 rounded-lg bg-rose-50 px-2.5 py-2 text-sm transition hover:bg-rose-100 dark:bg-rose-950/40 dark:hover:bg-rose-950/60">
                      <span className="grid h-8 w-8 flex-none place-items-center rounded-full bg-rose-100 text-rose-700 dark:bg-rose-900/60 dark:text-rose-300"><AlertTriangle size={14} /></span>
                      <span className="min-w-0 flex-1">
                        <span className="block font-medium text-rose-900 dark:text-rose-100">{stockCritico} peça{stockCritico === 1 ? '' : 's'} em stock crítico</span>
                        <span className="block text-[11px] text-rose-700/80 dark:text-rose-300/80">Repor antes que falte na bancada</span>
                      </span>
                    </Link>
                  </li>
                )}
                {faturasPend > 0 && (
                  <li>
                    <Link to="/reparacoes?openPagasSemFatura=1" className="flex items-center gap-2.5 rounded-lg bg-amber-50 px-2.5 py-2 text-sm transition hover:bg-amber-100 dark:bg-amber-950/40 dark:hover:bg-amber-950/60">
                      <span className="grid h-8 w-8 flex-none place-items-center rounded-full bg-amber-100 text-amber-700 dark:bg-amber-900/60 dark:text-amber-300"><Euro size={14} /></span>
                      <span className="min-w-0 flex-1">
                        <span className="block font-medium text-amber-900 dark:text-amber-100">{faturasPend} reparação{faturasPend === 1 ? '' : 'ões'} paga{faturasPend === 1 ? '' : 's'} sem fatura</span>
                        <span className="block text-[11px] text-amber-700/80 dark:text-amber-300/80">Emitir no Moloni para fechar</span>
                      </span>
                    </Link>
                  </li>
                )}
                {/* Sprint 431 (Doc 90): sinais do cron S392 — reparações paradas. */}
                {reparacoesParadasCount > 0 && (
                  <li>
                    <Link to="/reparacoes" className="flex items-center gap-2.5 rounded-lg bg-rose-50 px-2.5 py-2 text-sm transition hover:bg-rose-100 dark:bg-rose-950/40 dark:hover:bg-rose-950/60">
                      <span className="grid h-8 w-8 flex-none place-items-center rounded-full bg-rose-100 text-rose-700 dark:bg-rose-900/60 dark:text-rose-300"><Clock3 size={14} /></span>
                      <span className="min-w-0 flex-1">
                        <span className="block font-medium text-rose-900 dark:text-rose-100">{reparacoesParadasCount} reparaç{reparacoesParadasCount === 1 ? 'ão parada' : 'ões paradas'} há +{STALLED_DAYS}d</span>
                        <span className="block text-[11px] text-rose-700/80 dark:text-rose-300/80">Sem mudar de estado — investigar bloqueios</span>
                      </span>
                    </Link>
                  </li>
                )}
                {/* Sprint 431 (Doc 90): sinais do cron S428 — tarefas atrasadas. */}
                {tarefasAtrasadasCount > 0 && (
                  <li>
                    <Link to="/tarefas" className="flex items-center gap-2.5 rounded-lg bg-amber-50 px-2.5 py-2 text-sm transition hover:bg-amber-100 dark:bg-amber-950/40 dark:hover:bg-amber-950/60">
                      <span className="grid h-8 w-8 flex-none place-items-center rounded-full bg-amber-100 text-amber-700 dark:bg-amber-900/60 dark:text-amber-300"><ListTodo size={14} /></span>
                      <span className="min-w-0 flex-1">
                        <span className="block font-medium text-amber-900 dark:text-amber-100">{tarefasAtrasadasCount} tarefa{tarefasAtrasadasCount === 1 ? '' : 's'} atrasada{tarefasAtrasadasCount === 1 ? '' : 's'}</span>
                        <span className="block text-[11px] text-amber-700/80 dark:text-amber-300/80">Prazo passou — concluir ou re-agendar</span>
                      </span>
                    </Link>
                  </li>
                )}
                {/* Sprint 431 (Doc 90): sinais do cron S430 — cobranças em atraso. */}
                {cobrancasEmAtrasoCount > 0 && (
                  <li>
                    <Link to="/reparacoes" className="flex items-center gap-2.5 rounded-lg bg-amber-50 px-2.5 py-2 text-sm transition hover:bg-amber-100 dark:bg-amber-950/40 dark:hover:bg-amber-950/60">
                      <span className="grid h-8 w-8 flex-none place-items-center rounded-full bg-amber-100 text-amber-700 dark:bg-amber-900/60 dark:text-amber-300"><Euro size={14} /></span>
                      <span className="min-w-0 flex-1">
                        <span className="block font-medium text-amber-900 dark:text-amber-100">{cobrancasEmAtrasoCount} cobrança{cobrancasEmAtrasoCount === 1 ? '' : 's'} em atraso</span>
                        <span className="block text-[11px] text-amber-700/80 dark:text-amber-300/80">Entregue / Concluído mas por pagar</span>
                      </span>
                    </Link>
                  </li>
                )}
                {/* Sprint 467: Devices com garantia fabricante a expirar — oportunidade cross-sell. */}
                {garantiaFabricanteCount > 0 && (
                  <li>
                    <Link to="/clientes" className="flex items-center gap-2.5 rounded-lg bg-purple-50 px-2.5 py-2 text-sm transition hover:bg-purple-100 dark:bg-purple-950/40 dark:hover:bg-purple-950/60">
                      <span className="grid h-8 w-8 flex-none place-items-center rounded-full bg-purple-100 text-purple-700 dark:bg-purple-900/60 dark:text-purple-300"><ShieldCheck size={14} /></span>
                      <span className="min-w-0 flex-1">
                        <span className="block font-medium text-purple-900 dark:text-purple-100">{garantiaFabricanteCount} garantia{garantiaFabricanteCount === 1 ? '' : 's'} fabricante a expirar (30d)</span>
                        <span className="block text-[11px] text-purple-700/80 dark:text-purple-300/80">Oferecer garantia loja antes do fabricante acabar</span>
                      </span>
                    </Link>
                  </li>
                )}
                {/* Sprint 460 (Doc 91): clientes em D/AP/P sem outbound — chamada à ação para CTAs S456/S457. */}
                {avisosPendentesCount > 0 && (
                  <li>
                    <Link to="/reparacoes" className="flex items-center gap-2.5 rounded-lg bg-sky-50 px-2.5 py-2 text-sm transition hover:bg-sky-100 dark:bg-sky-950/40 dark:hover:bg-sky-950/60">
                      <span className="grid h-8 w-8 flex-none place-items-center rounded-full bg-sky-100 text-sky-700 dark:bg-sky-900/60 dark:text-sky-300"><MessageCircle size={14} /></span>
                      <span className="min-w-0 flex-1">
                        <span className="block font-medium text-sky-900 dark:text-sky-100">{avisosPendentesCount} cliente{avisosPendentesCount === 1 ? '' : 's'} a avisar</span>
                        <span className="block text-[11px] text-sky-700/80 dark:text-sky-300/80">Diagnóstico / Aguarda peça / Pronto sem comunicação enviada</span>
                      </span>
                    </Link>
                  </li>
                )}
                {/* Sprint 441: sinais do cron ReadyForPickup — prontas há +N dias por levantar. */}
                {porLevantarCount > 0 && (
                  <li>
                    <Link to="/reparacoes?estado=4" className="flex items-center gap-2.5 rounded-lg bg-amber-50 px-2.5 py-2 text-sm transition hover:bg-amber-100 dark:bg-amber-950/40 dark:hover:bg-amber-950/60">
                      <span className="grid h-8 w-8 flex-none place-items-center rounded-full bg-amber-100 text-amber-700 dark:bg-amber-900/60 dark:text-amber-300"><Inbox size={14} /></span>
                      <span className="min-w-0 flex-1">
                        <span className="block font-medium text-amber-900 dark:text-amber-100">{porLevantarCount} pronta{porLevantarCount === 1 ? '' : 's'} há +{READY_DAYS}d por levantar</span>
                        <span className="block text-[11px] text-amber-700/80 dark:text-amber-300/80">Cliente ainda não veio buscar — voltar a contactar</span>
                      </span>
                    </Link>
                  </li>
                )}
              </ul>
            );
          })()}
        </SectionCard>
        </div>
      </section>

      {/* Sprint 429 (Doc 88 IDEIAS 1 + Doc 90 §3): cash flow 30 dias — pulso financeiro real. */}
      <section className="space-y-3">
        <ZoneHeader
          eyebrow="Fluxo de caixa"
          title="Receita vs despesa dos últimos 30 dias"
          subtitle={`Total: receita ${formatCents(cashflowTotal.receita)} · despesa ${formatCents(cashflowTotal.despesa)} · net ${formatCents(cashflowTotal.net)}.`}
        />
        <div className="rounded-xl border border-zinc-200 bg-white p-4 dark:border-zinc-800 dark:bg-zinc-900">
          <div className="h-56">
            <ResponsiveContainer width="100%" height="100%">
              <AreaChart data={cashflowData} margin={{ left: 0, right: 8, top: 6, bottom: 0 }}>
                <defs>
                  <linearGradient id="cf-receita" x1="0" x2="0" y1="0" y2="1">
                    <stop offset="0%" stopColor="#059669" stopOpacity={0.35} />
                    <stop offset="100%" stopColor="#059669" stopOpacity={0.02} />
                  </linearGradient>
                  <linearGradient id="cf-despesa" x1="0" x2="0" y1="0" y2="1">
                    <stop offset="0%" stopColor="#dc2626" stopOpacity={0.30} />
                    <stop offset="100%" stopColor="#dc2626" stopOpacity={0.02} />
                  </linearGradient>
                </defs>
                <XAxis dataKey="label" tick={{ fontSize: 10 }} interval="preserveStartEnd" />
                <YAxis tick={{ fontSize: 10 }} tickFormatter={(v) => `${v}€`} width={50} />
                <Tooltip
                  formatter={(value, name) => [`${Number(value ?? 0).toFixed(2)}€`, String(name)]}
                  labelStyle={{ fontWeight: 600 }}
                />
                <Area type="monotone" dataKey="receita" name="Receita" stroke="#059669" strokeWidth={2} fill="url(#cf-receita)" dot={false} />
                <Area type="monotone" dataKey="despesa" name="Despesa" stroke="#dc2626" strokeWidth={2} fill="url(#cf-despesa)" dot={false} />
              </AreaChart>
            </ResponsiveContainer>
          </div>
        </div>
      </section>

      {/* Sprint 423 (Doc 90): widgets pessoais — o que está marcado para os próximos dias. */}
      <section className="space-y-3">
        <ZoneHeader
          eyebrow="Para hoje"
          title="O teu dia, num só sítio"
          subtitle="Reparações com ETA esta semana e tarefas internas pendentes."
        />
        <div className="grid grid-cols-1 gap-3 lg:grid-cols-2">
          <ReparacoesEtaWidget loading={fila.isLoading} items={reparacoesEtaSemana} />
          <TarefasPendentesWidget loading={tarefasPendentes.isLoading} items={tarefasPendentes.data ?? []} />
        </div>
      </section>

      <section className="space-y-3">
        <ZoneHeader
          eyebrow="Alertas + Top"
          title="O que merece accao ou repeticao"
          subtitle={hasOperationalAlert ? 'Primeiro o risco, depois o que esta a dar dinheiro.' : 'Sem incendios operacionais neste momento.'}
        />
        <div className="grid grid-cols-1 gap-3 lg:grid-cols-2 xl:grid-cols-4">
          <GarantiasWidget loading={garantias.isLoading} activas={garantias.data?.activas ?? 0} expiram={garantias.data?.expiramEm30Dias ?? 0} items={garantias.data?.proximasAExpirar ?? []} />
          <ReabastecerWidget loading={reabastecer.isLoading} items={reabastecer.data ?? []} />
          <TopReparacoesWidget loading={kpis.isLoading} items={kpis.data?.topReparacoesLucrativas30d ?? []} />
          <TopPecasWidget loading={kpis.isLoading} items={kpis.data?.topPecasUsadas30d ?? []} />
        </div>
      </section>
    </div>
  );
}

function ZoneHeader({ eyebrow, title, subtitle }: { eyebrow: string; title: string; subtitle: string }) {
  return (
    <div>
      <div className="text-xs font-semibold uppercase tracking-wide text-zinc-500">{eyebrow}</div>
      <div className="mt-1 flex flex-col gap-1 sm:flex-row sm:items-end sm:justify-between">
        <h2 className="text-lg font-semibold tracking-tight text-zinc-950 dark:text-zinc-50">{title}</h2>
        <p className="max-w-2xl text-sm text-zinc-500">{subtitle}</p>
      </div>
    </div>
  );
}

// Sprint 377: helpers da fila operacional (derivados do estado/tempo — sem campos novos).
function estadoLabel(e: RepairStatus): string {
  return STATUS_LABEL[e] ?? '—';
}

function proximaAccao(e: RepairStatus): string {
  return ({
    [REPAIR_STATUS.Orcamento]: 'Aguardar aprovação',
    [REPAIR_STATUS.Recebido]: 'Iniciar diagnóstico',
    [REPAIR_STATUS.Diagnostico]: 'Concluir diagnóstico',
    [REPAIR_STATUS.AguardaPeca]: 'Peça em falta',
    [REPAIR_STATUS.EmReparacao]: 'Continuar reparação',
    [REPAIR_STATUS.Pronto]: 'Contactar cliente',
    [REPAIR_STATUS.Entregue]: '—',
    [REPAIR_STATUS.Cancelado]: '—',
  } as Record<RepairStatus, string>)[e] ?? '—';
}

function diasDesde(iso: string): number {
  return Math.max(0, Math.floor((Date.now() - new Date(iso).getTime()) / 86_400_000));
}

function prioridade(iso: string): { label: string; cls: string } {
  const d = diasDesde(iso);
  if (d >= 5) return { label: 'Alta', cls: 'bg-red-100 text-red-700 dark:bg-red-900/40 dark:text-red-300' };
  if (d >= 2) return { label: 'Média', cls: 'bg-amber-100 text-amber-700 dark:bg-amber-900/40 dark:text-amber-300' };
  return { label: 'Baixa', cls: 'bg-zinc-100 text-zinc-600 dark:bg-zinc-800 dark:text-zinc-300' };
}

function tempoDesde(iso: string): string {
  const h = Math.floor((Date.now() - new Date(iso).getTime()) / 3_600_000);
  if (h < 1) return 'agora';
  if (h < 24) return `há ${h}h`;
  return `há ${Math.floor(h / 24)}d`;
}

function WeeklyCard({
  to,
  icon: Icon,
  tone,
  label,
  value,
  suffix,
  helper,
  loading,
  children,
}: {
  to: string;
  icon: LucideIcon;
  tone: Tone;
  label: string;
  value: ReactNode;
  suffix?: string;
  helper?: string;
  loading: boolean;
  children?: ReactNode;
}) {
  const cls = toneClass[tone];
  return (
    <Link
      to={to}
      className="group flex min-h-36 flex-col rounded-xl border border-zinc-200/80 bg-white p-4 shadow-sm shadow-black/[0.02] transition hover:-translate-y-0.5 hover:shadow-md dark:border-zinc-800 dark:bg-zinc-900"
    >
      <div className="flex items-start justify-between gap-3">
        <span className={`grid h-9 w-9 flex-none place-items-center rounded-lg ${cls.icon}`}>
          <Icon size={18} strokeWidth={2} />
        </span>
        <ArrowRight size={15} className="text-zinc-300 transition group-hover:translate-x-0.5 group-hover:text-zinc-600 dark:text-zinc-600 dark:group-hover:text-zinc-300" />
      </div>
      <p className="mt-3 text-xs font-medium text-zinc-500">{label}</p>
      {loading ? (
        <Skeleton className="mt-1 h-7 w-24" />
      ) : (
        <div className="mt-0.5 flex items-baseline gap-2">
          <span className="text-2xl font-semibold tabular-nums tracking-tight text-zinc-950 dark:text-zinc-50">{value ?? 0}</span>
          {suffix && <span className="text-xs text-zinc-500">{suffix}</span>}
        </div>
      )}
      {/* Sprint 239 fix: esconder children (sparkline ResponsiveContainer) quando loading
          para evitar Recharts warning "width(-1) and height(-1) of chart should be greater
          than 0" — o container ainda não tem dimensões antes dos KPIs carregarem. */}
      {!loading && children}
      {helper && <p className="mt-auto pt-3 text-xs leading-5 text-zinc-500">{helper}</p>}
    </Link>
  );
}

function GarantiasWidget({
  loading,
  activas,
  expiram,
  items,
}: {
  loading: boolean;
  activas: number;
  expiram: number;
  items: Array<{
    id: string;
    slug: string;
    dataFim: string;
    diasRestantes: number;
    origem: 'Reparacao' | 'Venda';
    documentoReferencia: string | null;
    equipamentoOuArtigo: string | null;
    clienteNome: string | null;
  }>;
}) {
  return (
    <Panel
      to="/reparacoes"
      icon={ShieldCheck}
      tone={expiram > 0 ? 'amber' : 'emerald'}
      title="Garantias a expirar"
      value={loading ? null : `${expiram}`}
      meta={`${activas} activas`}
    >
      {loading ? (
        <PanelSkeleton />
      ) : items.length === 0 ? (
        <EmptyState compact icon={ShieldCheck} title="Nada a expirar" description="As garantias dos proximos 30 dias estao limpas." />
      ) : (
        <ul className="mt-3 divide-y divide-zinc-100 text-sm dark:divide-zinc-800">
          {items.slice(0, 4).map((g) => (
            <li key={g.id} className="py-2">
              <a href={`/g/${g.slug}`} target="_blank" rel="noopener noreferrer" className="block rounded-md px-1 py-1 hover:bg-zinc-50 dark:hover:bg-zinc-800">
                <div className="flex items-center justify-between gap-2">
                  <span className="truncate font-medium">{g.equipamentoOuArtigo ?? g.documentoReferencia ?? 'Garantia'}</span>
                  <span className={g.diasRestantes <= 7 ? 'text-rose-600 dark:text-rose-400' : 'text-amber-700 dark:text-amber-300'}>
                    {g.diasRestantes}d
                  </span>
                </div>
                <div className="truncate text-xs text-zinc-500">
                  {g.clienteNome ?? 'Consumidor final'} - {formatDateOnly(g.dataFim)}
                </div>
              </a>
            </li>
          ))}
        </ul>
      )}
    </Panel>
  );
}

function ReabastecerWidget({
  loading,
  items,
}: {
  loading: boolean;
  items: Array<{
    partId: string;
    sku: string;
    nome: string;
    qtdStockActual: number;
    consumoDias: number;
    diasRestantesEstimados: number;
  }>;
}) {
  return (
    <Panel
      to="/stock"
      icon={PackageSearch}
      tone={items.length > 0 ? 'rose' : 'zinc'}
      title="Reabastecer < 30d"
      value={loading ? null : `${items.length}`}
      meta="previsao por consumo"
    >
      {loading ? (
        <PanelSkeleton />
      ) : items.length === 0 ? (
        <EmptyState compact icon={Boxes} title="Stock estavel" description="Nenhuma peca esta a caminho de ruptura nos proximos 30 dias." />
      ) : (
        <ul className="mt-3 divide-y divide-zinc-100 text-sm dark:divide-zinc-800">
          {items.slice(0, 4).map((p) => (
            <li key={p.partId} className="py-2">
              <div className="flex items-center justify-between gap-2">
                <span className="truncate font-medium">{p.nome}</span>
                <span className="text-rose-600 dark:text-rose-400">{p.diasRestantesEstimados}d</span>
              </div>
              <div className="truncate text-xs text-zinc-500">
                {p.sku} - stock {p.qtdStockActual} - usaste {p.consumoDias}/30d
              </div>
            </li>
          ))}
        </ul>
      )}
    </Panel>
  );
}

function TopReparacoesWidget({
  loading,
  items,
}: {
  loading: boolean;
  items: Array<{
    id: string;
    numero: number;
    equipamento: string;
    clienteNome: string | null;
    receitaCents: number;
    custoPecasCents: number;
    lucroCents: number;
  }>;
}) {
  return (
    <Panel
      to="/relatorios/negocio"
      icon={Trophy}
      tone="amber"
      title="Top reparacoes 30d"
      value={loading ? null : `${items.length}`}
      meta="por lucro"
    >
      {loading ? (
        <PanelSkeleton />
      ) : items.length === 0 ? (
        <EmptyState compact icon={Trophy} title="Ainda sem top" description="Quando entregares reparacoes pagas, aparecem aqui as mais lucrativas." />
      ) : (
        <ul className="mt-3 divide-y divide-zinc-100 text-sm dark:divide-zinc-800">
          {items.map((r) => (
            <li key={r.id}>
              <Link to={`/reparacoes/${r.id}`} className="block rounded-md px-1 py-2 hover:bg-zinc-50 dark:hover:bg-zinc-800">
                <div className="flex items-center justify-between gap-2">
                  <span className="truncate font-medium">#{r.numero} - {r.equipamento}</span>
                  <span className={r.lucroCents >= 0 ? 'text-emerald-700 dark:text-emerald-300' : 'text-rose-600 dark:text-rose-400'}>
                    {formatCents(r.lucroCents)}
                  </span>
                </div>
                <div className="truncate text-xs text-zinc-500">
                  {r.clienteNome ?? 'Cliente'} - receita {formatCents(r.receitaCents)} - pecas {formatCents(r.custoPecasCents)}
                </div>
              </Link>
            </li>
          ))}
        </ul>
      )}
    </Panel>
  );
}

function TopPecasWidget({
  loading,
  items,
}: {
  loading: boolean;
  items: Array<{
    partId: string;
    nome: string;
    sku: string | null;
    quantidade: number;
  }>;
}) {
  const data = items.map((p) => ({
    nome: p.sku ?? p.nome,
    quantidade: p.quantidade,
  }));

  return (
    <Panel
      to="/stock"
      icon={Boxes}
      tone="blue"
      title="Top pecas usadas 30d"
      value={loading ? null : `${items.length}`}
      meta="uso em reparacao"
    >
      {loading ? (
        <PanelSkeleton />
      ) : items.length === 0 ? (
        <EmptyState compact icon={Boxes} title="Sem consumo" description="As pecas usadas em reparacoes aparecem aqui para comprares melhor." />
      ) : (
        <>
          <div className="mt-3 h-28">
            <ResponsiveContainer width="100%" height="100%">
              <BarChart data={data} layout="vertical" margin={{ left: 0, right: 8, top: 4, bottom: 4 }}>
                <XAxis type="number" hide />
                <YAxis type="category" dataKey="nome" hide />
                <Tooltip formatter={(value) => `${value} un.`} />
                <Bar dataKey="quantidade" radius={[0, 4, 4, 0]} fill={toneClass.blue.chart} />
              </BarChart>
            </ResponsiveContainer>
          </div>
          <ul className="mt-2 space-y-1 text-xs text-zinc-500">
            {items.slice(0, 3).map((p) => (
              <li key={p.partId} className="flex justify-between gap-2">
                <span className="truncate">{p.sku ? `${p.sku} - ` : ''}{p.nome}</span>
                <span className="font-medium text-zinc-700 dark:text-zinc-300">{p.quantidade}</span>
              </li>
            ))}
          </ul>
        </>
      )}
    </Panel>
  );
}

// Sprint 423: widget de reparações com ETA esta semana (colhe S419).
function ReparacoesEtaWidget({
  loading,
  items,
}: {
  loading: boolean;
  items: Array<{ id: string; numero: number; equipamento: string; cliente: { nome: string }; previstoEntregueEm: string | null; estado: RepairStatus }>;
}) {
  const hojeStart = new Date(); hojeStart.setHours(0, 0, 0, 0);
  const hojeEnd = new Date(hojeStart); hojeEnd.setDate(hojeEnd.getDate() + 1);
  const hoje = items.filter((r) => {
    const t = new Date(r.previstoEntregueEm!).getTime();
    return t >= hojeStart.getTime() && t < hojeEnd.getTime();
  }).length;
  const atrasadas = items.filter((r) => new Date(r.previstoEntregueEm!) < hojeStart).length;
  const tone: Tone = atrasadas > 0 ? 'rose' : hoje > 0 ? 'amber' : 'zinc';
  return (
    <Panel
      to="/agendamentos"
      icon={CalendarClock}
      tone={tone}
      title="Reparações com ETA"
      value={loading ? null : `${items.length}`}
      meta={atrasadas > 0 ? `${atrasadas} atrasada${atrasadas === 1 ? '' : 's'}` : hoje > 0 ? `${hoje} hoje` : 'próximos 7 dias'}
    >
      {loading ? (
        <PanelSkeleton />
      ) : items.length === 0 ? (
        <EmptyState compact icon={CalendarClock} title="Sem ETA marcado" description="Define data prevista na reparação para ver aqui." />
      ) : (
        <ul className="mt-3 divide-y divide-zinc-100 text-sm dark:divide-zinc-800">
          {items.slice(0, 4).map((r) => {
            const eta = new Date(r.previstoEntregueEm!);
            const atrasada = eta < hojeStart;
            const ehHoje = eta >= hojeStart && eta < hojeEnd;
            return (
              <li key={r.id} className="py-2">
                <Link to={`/reparacoes/${r.id}`} className="block rounded-md px-1 py-1 hover:bg-zinc-50 dark:hover:bg-zinc-800">
                  <div className="flex items-center justify-between gap-2">
                    <span className="truncate font-medium">#{r.numero} {r.equipamento}</span>
                    <span className={atrasada ? 'text-rose-600 dark:text-rose-400' : ehHoje ? 'text-amber-700 dark:text-amber-300' : 'text-zinc-500'}>
                      {eta.toLocaleString('pt-PT', { weekday: 'short', hour: '2-digit', minute: '2-digit' })}
                    </span>
                  </div>
                  <div className="truncate text-xs text-zinc-500">
                    {r.cliente.nome} · {STATUS_LABEL[r.estado]}
                  </div>
                </Link>
              </li>
            );
          })}
        </ul>
      )}
    </Panel>
  );
}

// Sprint 423: widget de tarefas pendentes (colhe S422).
function TarefasPendentesWidget({
  loading,
  items,
}: {
  loading: boolean;
  items: InternalTask[];
}) {
  const agora = Date.now();
  const atrasadas = items.filter((t) => t.dueAt && new Date(t.dueAt).getTime() < agora).length;
  const tone: Tone = atrasadas > 0 ? 'rose' : items.length > 0 ? 'amber' : 'zinc';
  return (
    <Panel
      to="/tarefas"
      icon={ListTodo}
      tone={tone}
      title="Tarefas pendentes"
      value={loading ? null : `${items.length}`}
      meta={atrasadas > 0 ? `${atrasadas} atrasada${atrasadas === 1 ? '' : 's'}` : 'todas a tempo'}
    >
      {loading ? (
        <PanelSkeleton />
      ) : items.length === 0 ? (
        <EmptyState compact icon={ClipboardList} title="Lista limpa" description="Sem tarefas internas pendentes." />
      ) : (
        <ul className="mt-3 divide-y divide-zinc-100 text-sm dark:divide-zinc-800">
          {items.slice(0, 4).map((t) => {
            const atrasada = !!t.dueAt && new Date(t.dueAt).getTime() < agora;
            return (
              <li key={t.id} className="py-2">
                <div className="flex items-center justify-between gap-2">
                  <span className="truncate font-medium">{t.title}</span>
                  {t.dueAt && (
                    <span className={atrasada ? 'text-rose-600 dark:text-rose-400' : 'text-zinc-500'}>
                      {new Date(t.dueAt).toLocaleDateString('pt-PT', { day: '2-digit', month: 'short' })}
                    </span>
                  )}
                </div>
                {(t.assignedToDisplayName || t.reparacaoNumero) && (
                  <div className="truncate text-xs text-zinc-500">
                    {t.assignedToDisplayName && <>@{t.assignedToDisplayName}</>}
                    {t.assignedToDisplayName && t.reparacaoNumero && ' · '}
                    {t.reparacaoNumero && <>Reparação #{t.reparacaoNumero}</>}
                  </div>
                )}
              </li>
            );
          })}
        </ul>
      )}
    </Panel>
  );
}

function Panel({
  to,
  icon: Icon,
  tone,
  title,
  value,
  meta,
  children,
}: {
  to: string;
  icon: LucideIcon;
  tone: Tone;
  title: string;
  value: string | null;
  meta: string;
  children: ReactNode;
}) {
  const cls = toneClass[tone];
  return (
    <div className={`rounded-lg border bg-white p-4 shadow-sm dark:bg-zinc-900 ${cls.border}`}>
      <Link to={to} className="group flex items-start justify-between gap-3">
        <div className="min-w-0">
          <div className={`inline-flex items-center gap-2 rounded-md px-2.5 py-1 text-xs font-medium ${cls.soft}`}>
            <Icon size={14} strokeWidth={2} />
            {title}
          </div>
          <div className="mt-3 flex items-end gap-2">
            {value == null ? <Skeleton className="h-7 w-12" /> : <span className="text-2xl font-semibold text-zinc-950 dark:text-zinc-50">{value}</span>}
            <span className="pb-1 text-xs text-zinc-500">{meta}</span>
          </div>
        </div>
        <ArrowRight size={15} className="mt-1 text-zinc-400 transition group-hover:translate-x-0.5 group-hover:text-zinc-700 dark:group-hover:text-zinc-200" />
      </Link>
      {children}
    </div>
  );
}

function PanelSkeleton() {
  return (
    <div className="mt-4 space-y-2">
      <Skeleton className="h-4 w-full" />
      <Skeleton className="h-4 w-5/6" />
      <Skeleton className="h-4 w-2/3" />
    </div>
  );
}

function formatHours(hours: number | null | undefined) {
  if (hours == null) return '-';
  if (hours < 24) return `${hours.toFixed(1)} h`;
  return `${(hours / 24).toFixed(1)} dias`;
}
