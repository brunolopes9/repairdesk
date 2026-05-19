import { useMemo, useState, type ComponentType, type ReactNode } from 'react';
import { Link } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import {
  AlertTriangle,
  Activity,
  BarChart3,
  TrendingUp,
  Trophy,
  Star,
  Users,
  Clock,
  Lightbulb,
  ChevronRight,
  FileText,
  PackageSearch,
  ShoppingBag,
  X,
} from 'lucide-react';
import { stockApi } from '../lib/stock/api';
import {
  dashboardApi,
  periodRange,
  type AlertasResponse,
  type CategoriaFinanceira,
  type DespesaOrfa,
  type ItemPorCobrar,
  type MesFinanceiro,
  type Period,
  type ReparacaoTop,
} from '../lib/dashboard/api';
import { reparacoesApi } from '../lib/reparacoes/api';
import { trabalhosApi } from '../lib/trabalhos/api';
import {
  STATES_EM_CURSO,
  STATUS_COLOR,
  STATUS_LABEL,
  type RepairStatus,
} from '../lib/reparacoes/types';
import { tenantSettingsApi } from '../lib/tenantSettings/api';
import { formatCents, formatDateOnly } from '../lib/money';
import { EmptyState, PageHeader, Skeleton, SkeletonCard, SkeletonRow } from '../components/ui';

const PERIODS: Array<{ value: Period; label: string }> = [
  { value: 'this-month', label: 'Este mês' },
  { value: 'last-month', label: 'Mês anterior' },
  { value: 'last-90', label: '90 dias' },
  { value: 'this-year', label: 'Este ano' },
];

export default function Dashboard() {
  const [period, setPeriod] = useState<Period>('this-month');
  const range = periodRange(period);

  const { data, isLoading, isError } = useQuery({
    queryKey: ['dashboard', period],
    queryFn: () =>
      period === 'this-month'
        ? dashboardApi.current()
        : dashboardApi.range(range.from.toISOString(), range.to.toISOString()),
    staleTime: 60_000,
  });

  const financeiro = useQuery({
    queryKey: ['dashboard-financeiro', period],
    queryFn: () =>
      period === 'this-month'
        ? dashboardApi.financeiroCurrent()
        : dashboardApi.financeiroRange(range.from.toISOString(), range.to.toISOString()),
    staleTime: 60_000,
  });

  const alertas = useQuery({
    queryKey: ['dashboard-alertas'],
    queryFn: () => dashboardApi.alertas(),
    staleTime: 30_000,
  });

  // Para Δ% precisamos do período imediatamente anterior (mesma duração)
  const previousRange = useMemo(() => {
    const durationMs = range.to.getTime() - range.from.getTime();
    return {
      from: new Date(range.from.getTime() - durationMs),
      to: new Date(range.to.getTime() - durationMs),
    };
  }, [range.from, range.to]);

  const financeiroPrev = useQuery({
    queryKey: ['dashboard-financeiro-prev', period],
    queryFn: () => dashboardApi.financeiroRange(previousRange.from.toISOString(), previousRange.to.toISOString()),
    staleTime: 60_000,
  });

  const tendencia = useQuery({
    queryKey: ['dashboard-tendencia'],
    queryFn: () => dashboardApi.tendencia(6),
    staleTime: 5 * 60_000,
  });

  const topReparacoes = useQuery({
    queryKey: ['dashboard-top-reparacoes', period],
    queryFn: () =>
      period === 'this-month'
        ? dashboardApi.topReparacoesCurrent(5)
        : dashboardApi.topReparacoesRange(range.from.toISOString(), range.to.toISOString(), 5),
    staleTime: 60_000,
  });

  // Em curso: agrega Recebido + Diagnóstico + Aguarda Peça + Em Reparação + Reparado (5 queries paralelas).
  const emCurso = useQuery({
    queryKey: ['reparacoes-em-curso'],
    queryFn: async () => {
      const pages = await Promise.all(
        STATES_EM_CURSO.map((st) => reparacoesApi.list({ estado: st, pageSize: 100 })),
      );
      const items = pages.flatMap((p) => p.items);
      items.sort((a, b) => {
        // Prioridade visual: Recebido (precisa diag) > Reparado (precisa entregar) > Em Reparação > Aguarda Peça > Diagnóstico
        const order: Record<RepairStatus, number> = { 0: 0, 4: 1, 3: 2, 2: 3, 1: 4, 5: 99, 6: 99, 7: 99 };
        const oa = order[a.estado] ?? 99;
        const ob = order[b.estado] ?? 99;
        if (oa !== ob) return oa - ob;
        return new Date(a.estadoSince).getTime() - new Date(b.estadoSince).getTime();
      });
      return items;
    },
    staleTime: 30_000,
  });
  const emCursoItems = emCurso.data ?? [];

  const onboarding = useQuery({
    queryKey: ['onboarding-status'],
    queryFn: () => tenantSettingsApi.onboardingStatus(),
    staleTime: 30_000,
  });
  const onboardingIncomplete = onboarding.data ? !onboarding.data.onboardingCompletado : false;

  const lowStock = useQuery({
    queryKey: ['parts-low-stock'],
    queryFn: () => stockApi.lowStock(),
    staleTime: 60_000,
  });

  const reparacoesPendentesFatura = useQuery({
    queryKey: ['reparacoes-pagas-sem-fatura'],
    queryFn: () => reparacoesApi.listPagasSemFatura(100),
    staleTime: 60_000,
  });

  const trabalhosPendentesFatura = useQuery({
    queryKey: ['trabalhos-pagas-sem-fatura'],
    queryFn: () => trabalhosApi.listPagasSemFatura(100),
    staleTime: 60_000,
  });

  const totalEmCurso = emCursoItems.length;
  const lowStockCount = lowStock.data?.length ?? 0;
  const repsPendentesCount = reparacoesPendentesFatura.data?.length ?? 0;
  const trabsPendentesCount = trabalhosPendentesFatura.data?.length ?? 0;
  const totalFaturasPendentesCount = repsPendentesCount + trabsPendentesCount;
  const totalFaturasPendentesCents =
    (reparacoesPendentesFatura.data ?? []).reduce(
      (sum, r) => sum + (r.precoFinalCents ?? r.orcamentoCents ?? 0),
      0,
    ) +
    (trabalhosPendentesFatura.data ?? []).reduce(
      (sum, t) => sum + (t.precoFinalCents ?? t.orcamentoCents ?? 0),
      0,
    );

  const totalAlertas =
    (alertas.data
      ? alertas.data.reparacoesNaoPagas.length +
        alertas.data.trabalhosNaoPagos.length +
        alertas.data.despesasOrfas.length
      : 0) + lowStockCount + totalFaturasPendentesCount;

  return (
    <div className="space-y-8">
      <PageHeader
        title="Dashboard"
        description={`Vista geral da tua oficina - hoje, ${new Date().toLocaleDateString('pt-PT', { weekday: 'long', day: 'numeric', month: 'long' })}`}
      />

      {isError && (
        <div className="rounded-lg border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700 dark:border-red-900 dark:bg-red-950/40 dark:text-red-300">
          Não foi possível carregar o dashboard.
        </div>
      )}

      {/* ZONE 1 — Precisa de atenção */}
      {totalAlertas > 0 && (
        <Zone
          icon={AlertTriangle}
          title="Precisa de atenção"
          subtitle="Itens que não devem ficar esquecidos"
          tone="amber"
          count={totalAlertas}
        >
          <div className="space-y-3">
            {lowStockCount > 0 && (
              <Link
                to="/stock?lowStock=1"
                className="flex items-start gap-3 rounded-xl border border-rose-300 bg-rose-50 p-4 text-left transition hover:bg-rose-100 focus:outline-none focus-visible:ring-2 focus-visible:ring-brand-400 dark:border-rose-800/60 dark:bg-rose-950/30 dark:hover:bg-rose-950/50"
              >
                <PackageSearch size={20} strokeWidth={2} className="flex-none text-rose-700 dark:text-rose-300" aria-hidden />
                <div className="flex-1">
                  <div className="text-sm font-semibold">
                    {lowStockCount} {lowStockCount === 1 ? 'peça' : 'peças'} com stock baixo
                  </div>
                  <div className="text-xs text-zinc-600 dark:text-zinc-400">
                    Encomendar antes que pare uma reparação — clica para ver a lista
                  </div>
                </div>
                <ChevronRight size={16} strokeWidth={2} className="flex-none text-zinc-400" aria-hidden />
              </Link>
            )}
            {totalFaturasPendentesCount > 0 && (
              <div className="rounded-xl border border-amber-300 bg-amber-50 p-4 dark:border-amber-800/60 dark:bg-amber-950/30">
                <div className="flex items-start gap-3">
                  <FileText size={20} strokeWidth={2} className="flex-none text-amber-700 dark:text-amber-300" aria-hidden />
                  <div className="flex-1">
                    <div className="text-sm font-semibold">
                      {totalFaturasPendentesCount} {totalFaturasPendentesCount === 1 ? 'fatura pendente' : 'faturas pendentes'} — {formatCents(totalFaturasPendentesCents)}
                    </div>
                    <div className="text-xs text-zinc-600 dark:text-zinc-400">
                      Pagas mas ainda não comunicadas à AT. Emite em batch para fechar o dia.
                    </div>
                  </div>
                </div>
                <div className="mt-3 flex flex-wrap gap-2">
                  {repsPendentesCount > 0 && (
                    <Link
                      to="/reparacoes"
                      className="inline-flex items-center gap-1 rounded-md border border-amber-400 bg-white px-3 py-1.5 text-xs font-medium text-amber-800 hover:bg-amber-100 dark:border-amber-700 dark:bg-zinc-900 dark:text-amber-200"
                    >
                      {repsPendentesCount} reparação{repsPendentesCount === 1 ? '' : 'ões'}
                      <ChevronRight size={13} />
                    </Link>
                  )}
                  {trabsPendentesCount > 0 && (
                    <Link
                      to="/trabalhos"
                      className="inline-flex items-center gap-1 rounded-md border border-amber-400 bg-white px-3 py-1.5 text-xs font-medium text-amber-800 hover:bg-amber-100 dark:border-amber-700 dark:bg-zinc-900 dark:text-amber-200"
                    >
                      {trabsPendentesCount} trabalho{trabsPendentesCount === 1 ? '' : 's'}
                      <ChevronRight size={13} />
                    </Link>
                  )}
                </div>
              </div>
            )}
            <AlertasSection data={alertas.data} loading={alertas.isLoading} />
          </div>
        </Zone>
      )}

      {/* ZONE 2 — Hoje na oficina */}
      <Zone
        icon={Activity}
        title="Hoje na oficina"
        subtitle="Trabalho em curso, agrupado por etapa"
        tone="blue"
        count={totalEmCurso > 0 ? totalEmCurso : undefined}
      >
        <EmCursoSection items={emCursoItems} loading={emCurso.isLoading} onboardingIncomplete={onboardingIncomplete} />
      </Zone>

      {/* ZONE 3 — Saúde do negócio (com filtro de período) */}
      <Zone
        icon={BarChart3}
        title="Saúde do negócio"
        subtitle={range.label}
        tone="emerald"
        actions={
          <div className="flex flex-wrap gap-1.5">
            {PERIODS.map((p) => (
              <button
                key={p.value}
                type="button"
                onClick={() => setPeriod(p.value)}
                className={`rounded-full px-2.5 py-1 text-[11px] font-medium transition focus:outline-none focus-visible:ring-2 focus-visible:ring-brand-400 ${
                  period === p.value
                    ? 'bg-brand-600 text-white'
                    : 'bg-zinc-100 text-zinc-600 hover:bg-zinc-200 dark:bg-zinc-800 dark:text-zinc-300'
                }`}
              >
                {p.label}
              </button>
            ))}
          </div>
        }
      >
        <div className="space-y-6">
          <section className="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-3">
            <Kpi
              label="Lucro realizado"
              value={formatCents(financeiro.data?.lucroRealizadoCents)}
              sublabel={financeiro.data ? `Receita ${formatCents(financeiro.data.receitaRealizadaCents)} − Custo ${formatCents(financeiro.data.custoImputadoCents)}` : undefined}
              tone={financeiro.data && financeiro.data.lucroRealizadoCents >= 0 ? 'emerald' : 'red'}
              loading={financeiro.isLoading}
              delta={deltaPct(financeiro.data?.lucroRealizadoCents, financeiroPrev.data?.lucroRealizadoCents)}
              hint="Só conta receita já paga menos despesas imputadas a esses trabalhos."
            />
            <Kpi
              label="Receita pendente"
              value={formatCents(financeiro.data?.receitaPendenteCents)}
              sublabel="Concluído mas não pago"
              tone="amber"
              loading={financeiro.isLoading}
              delta={deltaPct(financeiro.data?.receitaPendenteCents, financeiroPrev.data?.receitaPendenteCents, { invertColor: true })}
              hint="Trabalhos/reparações terminados a aguardar pagamento do cliente."
            />
            <Kpi
              label="Investimento stock"
              value={formatCents(financeiro.data?.investimentoStockCents)}
              sublabel="Despesas sem trabalho pago"
              tone="zinc"
              loading={financeiro.isLoading}
              hint="Peças compradas ou despesas gerais ainda não associadas a um trabalho pago."
            />
            <Kpi
              label="Em curso"
              value={data ? `${data.kpis.reparacoesAbertas + data.kpis.trabalhosAbertos}` : '—'}
              sublabel={data ? `${data.kpis.reparacoesAbertas} reparações · ${data.kpis.trabalhosAbertos} trabalhos` : undefined}
              loading={isLoading}
            />
            <Kpi
              label="Vendas hoje"
              value={formatCents(data?.kpis.vendasHojeCents)}
              sublabel={data ? `Mes: ${formatCents(data.kpis.vendasMesCents)}` : undefined}
              tone="emerald"
              loading={isLoading}
            />
            <Kpi
              label="Vendas mes"
              value={formatCents(data?.kpis.vendasMesCents)}
              sublabel="POS e venda direta"
              tone="zinc"
              loading={isLoading}
            />
          </section>

          <TendenciaSection meses={tendencia.data?.meses ?? []} loading={tendencia.isLoading} />

          <div className="grid grid-cols-1 gap-3 lg:grid-cols-2">
            <TopReparacoesSection items={topReparacoes.data?.items ?? []} loading={topReparacoes.isLoading} />

            <section className="rounded-xl border border-zinc-200 bg-white p-4 dark:border-zinc-800 dark:bg-zinc-900">
              <div className="flex items-center justify-between">
                <h3 className="flex items-center gap-2 text-sm font-semibold">
                  <Users size={15} strokeWidth={2} className="text-zinc-500" />
                  Top clientes (90 dias)
                </h3>
                <span className="text-xs text-zinc-500">por receita</span>
              </div>
              {isLoading ? (
                <div className="mt-3 space-y-3">
                  {Array.from({ length: 3 }).map((_, index) => (
                    <SkeletonRow key={index} widths={['w-5', 'w-1/3', 'w-24', 'w-20']} />
                  ))}
                </div>
              ) : !data || data.topClientes.length === 0 ? (
                <div className="mt-3">
                  <EmptyState
                    compact
                    icon={Users}
                    title="Sem clientes pagos"
                    description="Quando houver trabalhos pagos nos ultimos 90 dias, os melhores clientes aparecem aqui."
                  />
                </div>
              ) : (
                <ol className="mt-2 divide-y divide-zinc-100 dark:divide-zinc-800">
                  {data.topClientes.map((c, i) => (
                    <li key={c.id} className="flex items-center justify-between gap-3 py-2 text-sm">
                      <div className="flex items-center gap-3">
                        <span className="w-5 text-right font-mono text-xs text-zinc-400">{i + 1}.</span>
                        <span className="font-medium">{c.nome}</span>
                        <span className="text-xs text-zinc-500">{c.trabalhos} {c.trabalhos === 1 ? 'trabalho' : 'trabalhos'}</span>
                      </div>
                      <span className="font-semibold">{formatCents(c.totalCents)}</span>
                    </li>
                  ))}
                </ol>
              )}
            </section>
          </div>

          <div className="grid grid-cols-1 gap-3 lg:grid-cols-2">
            <FinanceiroPorCategoria
              rows={financeiro.data?.porCategoria}
              loading={financeiro.isLoading}
            />
            <Breakdown
              title="Despesas por categoria"
              rows={data?.despesaPorCategoria}
              empty="Nenhuma despesa no período."
              accent="red"
              loading={isLoading}
            />
          </div>

          {/* Top produtos vendidos (Sprint 45 — Vendas/POS) */}
          <section className="rounded-xl border border-zinc-200 bg-white p-4 dark:border-zinc-800 dark:bg-zinc-900">
            <div className="flex items-center justify-between">
              <h3 className="flex items-center gap-2 text-sm font-semibold">
                <ShoppingBag size={15} strokeWidth={2} className="text-zinc-500" />
                Top produtos vendidos (90 dias)
              </h3>
              <span className="text-xs text-zinc-500">por receita</span>
            </div>
            {isLoading ? (
              <div className="mt-3 space-y-3">
                {Array.from({ length: 3 }).map((_, index) => (
                  <SkeletonRow key={index} widths={['w-5', 'w-1/3', 'w-20', 'w-24']} />
                ))}
              </div>
            ) : !data || data.topProdutosVendidos.length === 0 ? (
              <div className="mt-3">
                <EmptyState
                  compact
                  icon={ShoppingBag}
                  title="Sem vendas registadas"
                  description="Quando houver vendas POS nos últimos 90 dias, os produtos mais vendidos aparecem aqui."
                />
              </div>
            ) : (
              <ol className="mt-2 divide-y divide-zinc-100 dark:divide-zinc-800">
                {data.topProdutosVendidos.map((p, i) => (
                  <li key={`${p.partId ?? p.descricao}-${i}`} className="flex items-center justify-between gap-3 py-2 text-sm">
                    <div className="flex items-center gap-3 min-w-0">
                      <span className="w-5 text-right font-mono text-xs text-zinc-400">{i + 1}.</span>
                      <span className="truncate font-medium">{p.descricao}</span>
                      <span className="shrink-0 text-xs text-zinc-500">×{p.quantidade}</span>
                    </div>
                    <span className="font-semibold">{formatCents(p.totalCents)}</span>
                  </li>
                ))}
              </ol>
            )}
          </section>

          <AvaliacoesSection />
        </div>
      </Zone>
    </div>
  );
}

type ZoneTone = 'amber' | 'blue' | 'emerald';
type IconCmp = ComponentType<{ className?: string; size?: number; strokeWidth?: number }>;

const ZONE_TONES: Record<ZoneTone, { iconBg: string; iconFg: string; border: string }> = {
  amber: {
    iconBg: 'bg-amber-100 dark:bg-amber-900/40',
    iconFg: 'text-amber-700 dark:text-amber-300',
    border: 'border-amber-200/70 dark:border-amber-800/40',
  },
  blue: {
    iconBg: 'bg-blue-100 dark:bg-blue-900/40',
    iconFg: 'text-blue-700 dark:text-blue-300',
    border: 'border-blue-200/70 dark:border-blue-800/40',
  },
  emerald: {
    iconBg: 'bg-emerald-100 dark:bg-emerald-900/40',
    iconFg: 'text-emerald-700 dark:text-emerald-300',
    border: 'border-emerald-200/70 dark:border-emerald-800/40',
  },
};

function Zone({
  icon: Icon,
  title,
  subtitle,
  tone,
  count,
  actions,
  children,
}: {
  icon: IconCmp;
  title: string;
  subtitle?: string;
  tone: ZoneTone;
  count?: number;
  actions?: ReactNode;
  children: ReactNode;
}) {
  const t = ZONE_TONES[tone];
  return (
    <section className="space-y-3">
      <div className={`flex flex-wrap items-center justify-between gap-3 border-b pb-2 ${t.border}`}>
        <div className="flex items-center gap-2.5">
          <span className={`grid h-8 w-8 flex-none place-items-center rounded-lg ${t.iconBg}`}>
            <Icon size={16} strokeWidth={2} className={t.iconFg} />
          </span>
          <div>
            <h2 className="flex items-center gap-2 text-base font-semibold tracking-tight">
              {title}
              {typeof count === 'number' && count > 0 && (
                <span className="rounded-full bg-zinc-100 px-2 py-0.5 text-[11px] font-medium text-zinc-600 dark:bg-zinc-800 dark:text-zinc-300">
                  {count}
                </span>
              )}
            </h2>
            {subtitle && <p className="text-xs text-zinc-500">{subtitle}</p>}
          </div>
        </div>
        {actions}
      </div>
      {children}
    </section>
  );
}

interface DeltaInfo {
  pct: number;
  positive: boolean;
  arrow: string;
}

function deltaPct(
  current: number | undefined,
  previous: number | undefined,
  opts: { invertColor?: boolean } = {},
): DeltaInfo | undefined {
  if (current == null || previous == null) return undefined;
  if (previous === 0 && current === 0) return undefined;
  const delta = previous === 0 ? 100 : ((current - previous) / Math.abs(previous)) * 100;
  const positive = opts.invertColor ? delta < 0 : delta > 0;
  return {
    pct: Math.round(delta),
    positive,
    arrow: delta > 0 ? '↑' : delta < 0 ? '↓' : '·',
  };
}

function Kpi({
  label,
  value,
  sublabel,
  tone,
  loading,
  delta,
  hint,
}: {
  label: string;
  value: string;
  sublabel?: string;
  tone?: 'emerald' | 'red' | 'amber' | 'zinc';
  loading?: boolean;
  delta?: DeltaInfo;
  hint?: string;
}) {
  const toneCls =
    tone === 'emerald'
      ? 'text-emerald-700 dark:text-emerald-400'
      : tone === 'red'
        ? 'text-red-700 dark:text-red-400'
        : tone === 'amber'
          ? 'text-amber-700 dark:text-amber-400'
          : tone === 'zinc'
            ? 'text-zinc-700 dark:text-zinc-300'
            : '';
  const deltaCls = delta
    ? delta.positive
      ? 'bg-emerald-100 text-emerald-700 dark:bg-emerald-900/40 dark:text-emerald-300'
      : 'bg-rose-100 text-rose-700 dark:bg-rose-900/40 dark:text-rose-300'
    : '';
  return (
    <div className="rounded-xl border border-zinc-200 bg-white p-4 dark:border-zinc-800 dark:bg-zinc-900" title={hint}>
      <div className="flex items-center justify-between gap-2">
        <div className="text-xs uppercase tracking-wide text-zinc-500">{label}</div>
        {delta && (
          <span className={`rounded-full px-2 py-0.5 text-[10px] font-semibold tabular-nums ${deltaCls}`} title="vs período anterior">
            {delta.arrow} {Math.abs(delta.pct)}%
          </span>
        )}
      </div>
      <div className={`mt-1 text-2xl font-semibold ${toneCls}`}>{loading ? <Skeleton className="h-8 w-28" /> : value}</div>
      {sublabel && <div className="text-[11px] text-zinc-500">{sublabel}</div>}
    </div>
  );
}

const MONTH_LABELS_PT = ['Jan', 'Fev', 'Mar', 'Abr', 'Mai', 'Jun', 'Jul', 'Ago', 'Set', 'Out', 'Nov', 'Dez'];

function TendenciaSection({ meses, loading }: { meses: MesFinanceiro[]; loading: boolean }) {
  if (loading) {
    return (
      <section className="rounded-xl border border-zinc-200 bg-white p-4 dark:border-zinc-800 dark:bg-zinc-900">
        <h3 className="flex items-center gap-2 text-sm font-semibold">
          <TrendingUp size={15} strokeWidth={2} className="text-zinc-500" />
          Evolução (últimos 6 meses)
        </h3>
        <div className="mt-3 space-y-3">
          {Array.from({ length: 3 }).map((_, index) => (
            <SkeletonRow key={index} widths={['w-1/3', 'w-1/2', 'w-20']} />
          ))}
        </div>
      </section>
    );
  }
  const hasData = meses.some((m) => m.receitaCents > 0 || m.custoCents > 0);
  if (!hasData) return null;

  const maxValue = Math.max(1, ...meses.flatMap((m) => [m.receitaCents, m.custoCents]));
  const totalReceita = meses.reduce((s, m) => s + m.receitaCents, 0);
  const totalLucro = meses.reduce((s, m) => s + m.lucroCents, 0);
  const margemMedia = totalReceita > 0 ? Math.round((totalLucro / totalReceita) * 100) : 0;

  // SVG dimensions
  const W = 600;
  const H = 180;
  const PAD = { top: 12, right: 12, bottom: 28, left: 44 };
  const innerW = W - PAD.left - PAD.right;
  const innerH = H - PAD.top - PAD.bottom;
  const groupW = innerW / meses.length;
  const barW = Math.max(6, (groupW - 12) / 2);

  return (
    <section className="rounded-xl border border-zinc-200 bg-white p-4 dark:border-zinc-800 dark:bg-zinc-900">
      <div className="flex flex-wrap items-end justify-between gap-2">
        <div>
          <h3 className="flex items-center gap-2 text-sm font-semibold">
            <TrendingUp size={15} strokeWidth={2} className="text-zinc-500" />
            Evolução (últimos 6 meses)
          </h3>
          <p className="text-xs text-zinc-500">Receita vs custo imputado, mês a mês</p>
        </div>
        <div className="flex gap-4 text-[11px]">
          <div className="flex items-center gap-1.5"><span className="inline-block h-2 w-3 rounded-sm bg-emerald-500/80" />Receita</div>
          <div className="flex items-center gap-1.5"><span className="inline-block h-2 w-3 rounded-sm bg-rose-500/70" />Custo</div>
          <div className="flex items-center gap-1.5"><span className="inline-block h-2 w-3 rounded-sm bg-brand-500" />Lucro</div>
          <span className="text-zinc-500">·</span>
          <span className="text-zinc-600 dark:text-zinc-400">Margem média {margemMedia}%</span>
        </div>
      </div>

      <div className="mt-3 overflow-x-auto">
        <svg viewBox={`0 0 ${W} ${H}`} className="block h-44 w-full min-w-[560px]" role="img" aria-label="Evolução mensal">
          {/* Grid lines */}
          {[0, 0.25, 0.5, 0.75, 1].map((t) => (
            <line
              key={t}
              x1={PAD.left}
              x2={W - PAD.right}
              y1={PAD.top + innerH * (1 - t)}
              y2={PAD.top + innerH * (1 - t)}
              className="stroke-zinc-200 dark:stroke-zinc-800"
              strokeDasharray={t === 0 ? '0' : '2 3'}
            />
          ))}
          {/* Y axis labels */}
          {[0, 0.5, 1].map((t) => (
            <text
              key={t}
              x={PAD.left - 6}
              y={PAD.top + innerH * (1 - t) + 4}
              className="fill-zinc-500 text-[10px]"
              textAnchor="end"
            >
              {formatCentsShort(maxValue * t)}
            </text>
          ))}

          {/* Bars + labels */}
          {meses.map((m, i) => {
            const baseX = PAD.left + i * groupW;
            const receitaH = (m.receitaCents / maxValue) * innerH;
            const custoH = (m.custoCents / maxValue) * innerH;
            const cx = baseX + groupW / 2;
            return (
              <g key={`${m.ano}-${m.mes}`}>
                <rect
                  x={cx - barW - 1}
                  y={PAD.top + innerH - receitaH}
                  width={barW}
                  height={receitaH}
                  className="fill-emerald-500/80"
                  rx="1"
                >
                  <title>{`${MONTH_LABELS_PT[m.mes - 1]} ${m.ano} · Receita ${formatCents(m.receitaCents)}`}</title>
                </rect>
                <rect
                  x={cx + 1}
                  y={PAD.top + innerH - custoH}
                  width={barW}
                  height={custoH}
                  className="fill-rose-500/70"
                  rx="1"
                >
                  <title>{`${MONTH_LABELS_PT[m.mes - 1]} ${m.ano} · Custo ${formatCents(m.custoCents)}`}</title>
                </rect>
                <text
                  x={cx}
                  y={H - PAD.bottom + 14}
                  className="fill-zinc-500 text-[10px]"
                  textAnchor="middle"
                >
                  {MONTH_LABELS_PT[m.mes - 1]}
                </text>
              </g>
            );
          })}

          {/* Lucro line */}
          <polyline
            points={meses.map((m, i) => {
              const cx = PAD.left + i * groupW + groupW / 2;
              const cy = PAD.top + innerH - (Math.max(0, m.lucroCents) / maxValue) * innerH;
              return `${cx},${cy}`;
            }).join(' ')}
            className="fill-none stroke-brand-500"
            strokeWidth="2"
          />
          {meses.map((m, i) => {
            const cx = PAD.left + i * groupW + groupW / 2;
            const cy = PAD.top + innerH - (Math.max(0, m.lucroCents) / maxValue) * innerH;
            return (
              <circle key={`dot-${i}`} cx={cx} cy={cy} r={3} className="fill-brand-500">
                <title>{`Lucro ${formatCents(m.lucroCents)}`}</title>
              </circle>
            );
          })}
        </svg>
      </div>
    </section>
  );
}

function TopReparacoesSection({ items, loading }: { items: ReparacaoTop[]; loading: boolean }) {
  if (loading) {
    return (
      <section className="rounded-xl border border-zinc-200 bg-white p-4 dark:border-zinc-800 dark:bg-zinc-900">
        <h3 className="flex items-center gap-2 text-sm font-semibold">
          <Trophy size={15} strokeWidth={2} className="text-zinc-500" />
          Top reparações lucrativas
        </h3>
        <div className="mt-3 space-y-3">
          {Array.from({ length: 3 }).map((_, index) => (
            <SkeletonRow key={index} widths={['w-1/3', 'w-1/2', 'w-20']} />
          ))}
        </div>
      </section>
    );
  }
  if (items.length === 0) return null;

  return (
    <section className="rounded-xl border border-zinc-200 bg-white p-4 dark:border-zinc-800 dark:bg-zinc-900">
      <div className="flex items-center justify-between">
        <h3 className="flex items-center gap-2 text-sm font-semibold">
          <Trophy size={15} strokeWidth={2} className="text-zinc-500" />
          Top reparações lucrativas
        </h3>
        <span className="text-xs text-zinc-500">por lucro no período</span>
      </div>
      <ol className="mt-2 divide-y divide-zinc-100 dark:divide-zinc-800">
        {items.map((r, i) => {
          const margem = r.receitaCents > 0 ? Math.round((r.lucroCents / r.receitaCents) * 100) : 0;
          return (
            <li key={r.id}>
              <Link to={`/reparacoes/${r.id}`} className="flex items-center justify-between gap-3 py-2 text-sm hover:bg-zinc-50 dark:hover:bg-zinc-800">
                <div className="min-w-0 flex-1">
                  <div className="flex items-center gap-2">
                    <span className="w-5 text-right font-mono text-xs text-zinc-400">{i + 1}.</span>
                    <span className="text-xs font-mono text-zinc-500">#{r.numero}</span>
                    <span className="truncate font-medium">{r.equipamento}</span>
                  </div>
                  {r.clienteNome && <div className="ml-7 text-[11px] text-zinc-500">{r.clienteNome}</div>}
                </div>
                <div className="text-right">
                  <div className="font-semibold text-emerald-700 dark:text-emerald-400 tabular-nums">{formatCents(r.lucroCents)}</div>
                  <div className="text-[10px] text-zinc-500 tabular-nums">{formatCents(r.receitaCents)} − {formatCents(r.custoCents)} · {margem}%</div>
                </div>
              </Link>
            </li>
          );
        })}
      </ol>
    </section>
  );
}

function formatCentsShort(cents: number): string {
  const v = cents / 100;
  if (Math.abs(v) >= 1000) return `${Math.round(v / 100) / 10}k €`;
  return `${Math.round(v)} €`;
}

function AvaliacoesSection() {
  const q = useQuery({
    queryKey: ['dashboard-avaliacoes'],
    queryFn: () => dashboardApi.avaliacoes(),
    staleTime: 5 * 60_000,
  });

  if (q.isLoading) return null;
  const data = q.data;
  if (!data || data.total === 0) {
    // Empty state — incentivar a primeira avaliação
    return (
      <section className="rounded-xl border border-dashed border-zinc-300 bg-white p-4 text-center dark:border-zinc-700 dark:bg-zinc-900">
        <h3 className="flex items-center justify-center gap-2 text-sm font-semibold">
          <Star size={15} strokeWidth={2} className="text-zinc-500" />
          Avaliações
        </h3>
        <p className="mt-2 text-xs text-zinc-500">
          Ainda sem avaliações. Marca uma reparação como <em>Entregue</em> — o cliente recebe pedido de avaliação automático no portal.
        </p>
      </section>
    );
  }

  const maxDist = Math.max(1, ...Object.values(data.distribuicao));
  const npsTone = data.nps >= 50 ? 'emerald' : data.nps >= 0 ? 'amber' : 'rose';
  const npsCls =
    npsTone === 'emerald'
      ? 'text-emerald-700 dark:text-emerald-400'
      : npsTone === 'amber'
        ? 'text-amber-700 dark:text-amber-400'
        : 'text-rose-700 dark:text-rose-400';

  return (
    <section className="grid grid-cols-1 gap-3 sm:grid-cols-3">
      {/* Card média + total */}
      <div className="rounded-xl border border-zinc-200 bg-white p-4 dark:border-zinc-800 dark:bg-zinc-900">
        <div className="flex items-center gap-1.5 text-xs uppercase tracking-wide text-zinc-500">
          <Star size={12} strokeWidth={2} />
          Avaliação média
        </div>
        <div className="mt-1 flex items-baseline gap-1">
          <span className="text-4xl font-semibold tabular-nums text-amber-600 dark:text-amber-400">
            {data.mediaScore?.toFixed(1) ?? '—'}
          </span>
          <span className="text-base text-zinc-500">/5</span>
        </div>
        <div className="mt-1 text-amber-500" aria-hidden>
          {'★'.repeat(Math.round(data.mediaScore ?? 0))}
          <span className="text-zinc-300 dark:text-zinc-700">{'★'.repeat(5 - Math.round(data.mediaScore ?? 0))}</span>
        </div>
        <div className="mt-2 text-[11px] text-zinc-500">
          {data.total} {data.total === 1 ? 'avaliação' : 'avaliações'} no total
        </div>
      </div>

      {/* Distribuição */}
      <div className="rounded-xl border border-zinc-200 bg-white p-4 dark:border-zinc-800 dark:bg-zinc-900">
        <div className="text-xs uppercase tracking-wide text-zinc-500">Distribuição</div>
        <ul className="mt-2 space-y-1">
          {[5, 4, 3, 2, 1].map((star) => {
            const count = data.distribuicao[String(star)] ?? 0;
            const pct = (count / maxDist) * 100;
            return (
              <li key={star} className="flex items-center gap-2 text-xs">
                <span className="w-8 text-right tabular-nums">{star}★</span>
                <div className="flex-1 h-2 overflow-hidden rounded-full bg-zinc-100 dark:bg-zinc-800">
                  <div className={`h-full ${star >= 4 ? 'bg-emerald-500/70' : star === 3 ? 'bg-amber-500/70' : 'bg-rose-500/70'}`} style={{ width: `${pct}%` }} />
                </div>
                <span className="w-8 text-right tabular-nums text-zinc-500">{count}</span>
              </li>
            );
          })}
        </ul>
      </div>

      {/* NPS */}
      <div className="rounded-xl border border-zinc-200 bg-white p-4 dark:border-zinc-800 dark:bg-zinc-900" title="NPS = % de 5★ menos % de 1-2★. Acima de 50 é excelente.">
        <div className="text-xs uppercase tracking-wide text-zinc-500">NPS</div>
        <div className={`mt-1 text-4xl font-semibold tabular-nums ${npsCls}`}>
          {data.nps > 0 ? '+' : ''}{data.nps}
        </div>
        <div className="mt-2 grid grid-cols-2 gap-1 text-[11px]">
          <div className="text-emerald-700 dark:text-emerald-400">↑ {data.promoters} promoters</div>
          <div className="text-rose-700 dark:text-rose-400">↓ {data.detractors} detractors</div>
        </div>
      </div>

      {/* Recentes — span full */}
      <div className="col-span-full rounded-xl border border-zinc-200 bg-white p-4 dark:border-zinc-800 dark:bg-zinc-900">
        <div className="flex items-center justify-between">
          <h3 className="text-sm font-semibold">Avaliações recentes</h3>
          <span className="text-xs text-zinc-500">últimas {data.recentes.length}</span>
        </div>
        {data.recentes.length === 0 ? (
          <p className="mt-2 text-xs text-zinc-500">Sem avaliações ainda.</p>
        ) : (
          <ul className="mt-3 divide-y divide-zinc-100 dark:divide-zinc-800">
            {data.recentes.map((r) => (
              <li key={r.id}>
                <Link to={`/reparacoes/${r.reparacaoId}`} className="flex items-start gap-3 py-2 text-sm hover:bg-zinc-50 dark:hover:bg-zinc-800">
                  <div className="flex-shrink-0 text-amber-500 text-base">
                    {'★'.repeat(r.score)}
                    <span className="text-zinc-300 dark:text-zinc-700">{'★'.repeat(5 - r.score)}</span>
                  </div>
                  <div className="min-w-0 flex-1">
                    <div className="flex flex-wrap items-center gap-2 text-xs">
                      <span className="font-mono text-zinc-500">#{r.reparacaoNumero}</span>
                      <span className="font-medium">{r.clienteNome}</span>
                      <span className="text-zinc-500">· {r.equipamento}</span>
                      <span className="ml-auto text-zinc-400">{formatDateOnly(r.criadaEm)}</span>
                    </div>
                    {r.comentario && <p className="mt-1 text-xs text-zinc-700 dark:text-zinc-300">"{r.comentario}"</p>}
                  </div>
                </Link>
              </li>
            ))}
          </ul>
        )}
      </div>
    </section>
  );
}

function FinanceiroPorCategoria({
  rows,
  loading,
}: {
  rows: CategoriaFinanceira[] | undefined;
  loading?: boolean;
}) {
  const max = Math.max(1, ...(rows ?? []).map((r) => Math.max(r.receitaCents, r.custoCents)));
  return (
    <section className="rounded-xl border border-zinc-200 bg-white p-4 dark:border-zinc-800 dark:bg-zinc-900">
      <div className="flex items-center justify-between">
        <h2 className="text-sm font-semibold">Lucro por categoria</h2>
        <span className="text-xs text-zinc-500">Receita · Custo · Lucro</span>
      </div>
      {loading ? (
        <div className="mt-3 space-y-3">
          {Array.from({ length: 3 }).map((_, index) => (
            <SkeletonRow key={index} widths={['w-1/3', 'w-1/2', 'w-20']} />
          ))}
        </div>
      ) : !rows || rows.length === 0 ? (
        <div className="mt-3">
          <EmptyState
            compact
            icon={BarChart3}
            title="Sem trabalhos pagos"
            description="Quando houver receita no periodo, a margem por categoria aparece aqui."
          />
        </div>
      ) : (
        <ul className="mt-3 space-y-3">
          {rows.map((r) => {
            const receitaPct = Math.round((r.receitaCents / max) * 100);
            const custoPct = Math.round((r.custoCents / max) * 100);
            const margem = r.receitaCents > 0 ? Math.round((r.lucroCents / r.receitaCents) * 100) : 0;
            return (
              <li key={r.label}>
                <div className="flex items-center justify-between text-sm">
                  <span className="font-medium">
                    {r.label} <span className="text-xs font-normal text-zinc-500">· {r.count}</span>
                  </span>
                  <span className={`text-sm font-semibold ${r.lucroCents >= 0 ? 'text-emerald-700 dark:text-emerald-400' : 'text-red-700 dark:text-red-400'}`}>
                    {formatCents(r.lucroCents)}{' '}
                    <span className="text-[11px] font-normal text-zinc-500">({margem}%)</span>
                  </span>
                </div>
                <div className="mt-1 space-y-1">
                  <div className="flex items-center gap-2 text-[11px] text-zinc-500">
                    <span className="w-12">Receita</span>
                    <div className="flex-1 h-1.5 overflow-hidden rounded-full bg-zinc-100 dark:bg-zinc-800">
                      <div className="h-full bg-emerald-500/70" style={{ width: `${receitaPct}%` }} />
                    </div>
                    <span className="w-20 text-right tabular-nums">{formatCents(r.receitaCents)}</span>
                  </div>
                  <div className="flex items-center gap-2 text-[11px] text-zinc-500">
                    <span className="w-12">Custo</span>
                    <div className="flex-1 h-1.5 overflow-hidden rounded-full bg-zinc-100 dark:bg-zinc-800">
                      <div className="h-full bg-red-500/70" style={{ width: `${custoPct}%` }} />
                    </div>
                    <span className="w-20 text-right tabular-nums">{formatCents(r.custoCents)}</span>
                  </div>
                </div>
              </li>
            );
          })}
        </ul>
      )}
    </section>
  );
}

function Breakdown({
  title,
  rows,
  empty,
  accent,
  loading,
}: {
  title: string;
  rows: { label: string; count: number; totalCents: number }[] | undefined;
  empty: string;
  accent: 'emerald' | 'red';
  loading?: boolean;
}) {
  const total = (rows ?? []).reduce((s, r) => s + r.totalCents, 0);
  const barCls = accent === 'emerald' ? 'bg-emerald-500/70' : 'bg-red-500/70';
  return (
    <section className="rounded-xl border border-zinc-200 bg-white p-4 dark:border-zinc-800 dark:bg-zinc-900">
      <div className="flex items-center justify-between">
        <h2 className="text-sm font-semibold">{title}</h2>
        <span className="text-xs text-zinc-500">{formatCents(total)}</span>
      </div>
      {loading ? (
        <div className="mt-3 space-y-3">
          {Array.from({ length: 3 }).map((_, index) => (
            <SkeletonRow key={index} widths={['w-1/3', 'w-1/2', 'w-20']} />
          ))}
        </div>
      ) : !rows || rows.length === 0 ? (
        <div className="mt-3">
          <EmptyState compact icon={BarChart3} title="Sem movimento no periodo" description={empty} />
        </div>
      ) : (
        <ul className="mt-2 space-y-2">
          {rows.map((r) => {
            const pct = total > 0 ? Math.round((r.totalCents / total) * 100) : 0;
            return (
              <li key={r.label}>
                <div className="flex items-center justify-between text-sm">
                  <span>{r.label} <span className="text-xs text-zinc-500">· {r.count}</span></span>
                  <span className="font-medium">{formatCents(r.totalCents)}</span>
                </div>
                <div className="mt-1 h-1.5 overflow-hidden rounded-full bg-zinc-100 dark:bg-zinc-800">
                  <div className={`h-full ${barCls}`} style={{ width: `${pct}%` }} />
                </div>
              </li>
            );
          })}
        </ul>
      )}
    </section>
  );
}

interface EmCursoItem {
  id: string;
  numero: number;
  equipamento: string;
  cliente: { nome: string; telefone: string };
  estado: RepairStatus;
  estadoSince: string;
  recebidoEm: string;
}

function EmCursoSection({
  items,
  loading,
  onboardingIncomplete,
}: {
  items: EmCursoItem[];
  loading: boolean;
  onboardingIncomplete: boolean;
}) {
  if (loading) {
    return (
      <div className="rounded-xl border border-zinc-200 bg-white p-4 dark:border-zinc-800 dark:bg-zinc-900">
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 xl:grid-cols-3">
        {Array.from({ length: 3 }).map((_, index) => (
          <SkeletonCard key={index} />
        ))}
      </div>
      </div>
    );
  }
  if (items.length === 0) {
    return (
      <EmptyState
        icon={Activity}
        title="Sem reparacoes em curso"
        description={
          onboardingIncomplete
            ? 'Termina o arranque para criares a primeira ficha em poucos minutos.'
            : 'Quando entrar uma reparacao, aparece aqui agrupada por etapa.'
        }
        action={onboardingIncomplete ? (
          <Link
            to="/bemvindo"
            className="inline-flex h-9 items-center justify-center rounded-lg bg-brand-600 px-3 text-sm font-medium text-white shadow-sm transition hover:bg-brand-700 focus:outline-none focus-visible:ring-2 focus-visible:ring-brand-400"
          >
            Continuar wizard
          </Link>
        ) : undefined}
      />
    );
  }

  // separar por urgência: Recebido (precisa diag) primeiro, Reparado (precisa entregar) depois, Diag (a aguardar peça) por último
  const recebidos = items.filter((i) => i.estado === 0);
  const reparados = items.filter((i) => i.estado === 4);
  const emReparacao = items.filter((i) => i.estado === 3);
  const aguardaPeca = items.filter((i) => i.estado === 2);
  const diagnostico = items.filter((i) => i.estado === 1);

  return (
    <div className="space-y-4">
      {recebidos.length > 0 && (
        <Group
          title="Por diagnosticar"
          subtitle="Acabados de entrar na loja — vê estes primeiro"
          items={recebidos}
          accent="amber"
        />
      )}
      {reparados.length > 0 && (
        <Group
          title="Prontos para entregar"
          subtitle="Acabados, à espera do cliente — avisa que pode passar"
          items={reparados}
          accent="emerald"
        />
      )}
      {emReparacao.length > 0 && (
        <Group
          title="Em reparação"
          subtitle="A trabalhar nelas — termina e marca como Reparado"
          items={emReparacao}
          accent="blue"
        />
      )}
      {aguardaPeca.length > 0 && (
        <Group
          title="A aguardar peça"
          subtitle="Peça encomendada — quando chegar avança para Em reparação"
          items={aguardaPeca}
          accent="sky"
        />
      )}
      {diagnostico.length > 0 && (
        <Group
          title="Em análise"
          subtitle="A diagnosticar — decide se encomenda peça ou avança para reparar"
          items={diagnostico}
          accent="violet"
        />
      )}
    </div>
  );
}

function Group({
  title,
  subtitle,
  items,
  accent,
}: {
  title: string;
  subtitle: string;
  items: EmCursoItem[];
  accent: 'amber' | 'emerald' | 'blue' | 'sky' | 'violet';
}) {
  const borderCls =
    accent === 'amber'
      ? 'border-amber-300 dark:border-amber-700'
      : accent === 'emerald'
        ? 'border-emerald-300 dark:border-emerald-700'
        : accent === 'sky'
          ? 'border-sky-300 dark:border-sky-700'
          : accent === 'violet'
            ? 'border-violet-300 dark:border-violet-700'
            : 'border-blue-300 dark:border-blue-700';
  return (
    <div className={`rounded-xl border-l-4 ${borderCls} bg-white dark:bg-zinc-900`}>
      <div className="px-4 pt-3">
        <div className="text-sm font-semibold">{title} <span className="text-zinc-500">· {items.length}</span></div>
        <div className="text-xs text-zinc-500">{subtitle}</div>
      </div>
      <ul className="divide-y divide-zinc-100 px-2 pb-2 pt-2 dark:divide-zinc-800">
        {items.map((r) => {
          const diasParado = Math.floor((Date.now() - new Date(r.estadoSince).getTime()) / (1000 * 60 * 60 * 24));
          const stale = diasParado >= 7;
          return (
            <li key={r.id}>
              <Link
                to={`/reparacoes/${r.id}`}
                className="flex items-center justify-between gap-3 rounded-lg px-2 py-2 text-sm hover:bg-zinc-50 dark:hover:bg-zinc-800"
              >
                <div className="min-w-0 flex-1">
                  <div className="flex flex-wrap items-center gap-2">
                    <span className="text-xs font-mono text-zinc-500">#{r.numero}</span>
                    <span className={`rounded-full px-2 py-0.5 text-[10px] font-medium ${STATUS_COLOR[r.estado]}`}>
                      {STATUS_LABEL[r.estado]}
                    </span>
                    {stale && (
                      <span className="inline-flex items-center gap-1 rounded-full bg-red-100 px-2 py-0.5 text-[10px] font-semibold text-red-700 dark:bg-red-950/50 dark:text-red-300">
                        <Clock size={11} strokeWidth={2.25} />
                        {diasParado} dias parado
                      </span>
                    )}
                  </div>
                  <div className="mt-0.5 truncate font-medium">{r.equipamento}</div>
                  <div className="text-[11px] text-zinc-500">
                    {r.cliente.nome}{r.cliente.telefone && ` · ${r.cliente.telefone}`}
                  </div>
                </div>
                <span className="text-[11px] text-zinc-400 whitespace-nowrap">
                  desde {formatDateOnly(r.estadoSince)}
                </span>
              </Link>
            </li>
          );
        })}
      </ul>
    </div>
  );
}

function AlertasSection({ data, loading }: { data: AlertasResponse | undefined; loading: boolean }) {
  const [open, setOpen] = useState<'porCobrar' | 'orfas' | null>(null);

  if (loading || !data) return null;

  const porCobrarItems: ItemPorCobrar[] = [...data.reparacoesNaoPagas, ...data.trabalhosNaoPagos]
    .sort((a, b) => {
      const ad = a.concluidoEm ? new Date(a.concluidoEm).getTime() : 0;
      const bd = b.concluidoEm ? new Date(b.concluidoEm).getTime() : 0;
      return bd - ad;
    });

  const nadaParaAvisar = porCobrarItems.length === 0 && data.despesasOrfas.length === 0;
  if (nadaParaAvisar) return null;

  return (
    <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
      {porCobrarItems.length > 0 && (
        <AlertCard
          tone="amber"
          icon={AlertTriangle}
          title={`${porCobrarItems.length} ${porCobrarItems.length === 1 ? 'item' : 'itens'} concluídos por cobrar`}
          subtitle={`${formatCents(data.totalPorCobrarCents)} pendente — clica para ver`}
          onClick={() => setOpen('porCobrar')}
        />
      )}
      {data.despesasOrfas.length > 0 && (
        <AlertCard
          tone="blue"
          icon={Lightbulb}
          title={`${data.despesasOrfas.length} ${data.despesasOrfas.length === 1 ? 'despesa' : 'despesas'} sem trabalho associado`}
          subtitle={`${formatCents(data.totalDespesasOrfasCents)} em stock/overhead — confirma se está certo`}
          onClick={() => setOpen('orfas')}
        />
      )}

      {open === 'porCobrar' && (
        <PorCobrarPanel
          trabalhos={data.trabalhosNaoPagos}
          reparacoes={data.reparacoesNaoPagas}
          onClose={() => setOpen(null)}
        />
      )}
      {open === 'orfas' && (
        <DespesasOrfasPanel
          items={data.despesasOrfas}
          onClose={() => setOpen(null)}
        />
      )}
    </div>
  );
}

function AlertCard({
  tone,
  icon: Icon,
  title,
  subtitle,
  onClick,
}: {
  tone: 'amber' | 'blue';
  icon: IconCmp;
  title: string;
  subtitle: string;
  onClick: () => void;
}) {
  const toneCls =
    tone === 'amber'
      ? 'border-amber-300 bg-amber-50 hover:bg-amber-100 dark:border-amber-800/60 dark:bg-amber-950/30 dark:hover:bg-amber-950/50'
      : 'border-blue-300 bg-blue-50 hover:bg-blue-100 dark:border-blue-800/60 dark:bg-blue-950/30 dark:hover:bg-blue-950/50';
  const iconCls = tone === 'amber' ? 'text-amber-700 dark:text-amber-300' : 'text-blue-700 dark:text-blue-300';
  return (
    <button
      type="button"
      onClick={onClick}
      className={`flex items-start gap-3 rounded-xl border p-4 text-left transition focus:outline-none focus-visible:ring-2 focus-visible:ring-brand-400 ${toneCls}`}
    >
      <Icon size={20} strokeWidth={2} className={`flex-none ${iconCls}`} aria-hidden />
      <div className="flex-1">
        <div className="text-sm font-semibold">{title}</div>
        <div className="text-xs text-zinc-600 dark:text-zinc-400">{subtitle}</div>
      </div>
      <ChevronRight size={16} strokeWidth={2} className="flex-none text-zinc-400" aria-hidden />
    </button>
  );
}

function PorCobrarPanel({
  trabalhos,
  reparacoes,
  onClose,
}: {
  trabalhos: ItemPorCobrar[];
  reparacoes: ItemPorCobrar[];
  onClose: () => void;
}) {
  return (
    <div className="col-span-full rounded-xl border border-amber-300 bg-white p-4 dark:border-amber-800/60 dark:bg-zinc-900">
      <div className="mb-3 flex items-center justify-between">
        <h3 className="text-sm font-semibold">Itens concluídos por cobrar</h3>
        <button
          type="button"
          onClick={onClose}
          aria-label="Fechar painel"
          className="rounded-md p-1 text-zinc-500 transition hover:bg-zinc-100 hover:text-zinc-700 focus:outline-none focus-visible:ring-2 focus-visible:ring-brand-400 dark:hover:bg-zinc-800 dark:hover:text-zinc-300"
        >
          <X size={14} strokeWidth={2} />
        </button>
      </div>
      {reparacoes.length > 0 && (
        <>
          <div className="mb-1 text-[11px] uppercase tracking-wide text-zinc-500">Reparações</div>
          <ul className="mb-3 divide-y divide-zinc-100 dark:divide-zinc-800">
            {reparacoes.map((r) => (
              <li key={r.id}>
                <Link to={`/reparacoes/${r.id}`} className="flex items-center justify-between gap-2 rounded-md py-1.5 text-sm hover:bg-zinc-50 dark:hover:bg-zinc-800">
                  <div className="min-w-0 flex-1">
                    <span className="text-xs font-mono text-zinc-500">#{r.numero}</span>{' '}
                    <span className="truncate font-medium">{r.titulo}</span>
                    {r.clienteNome && <span className="ml-2 text-[11px] text-zinc-500">· {r.clienteNome}</span>}
                  </div>
                  <span className="font-semibold tabular-nums">{formatCents(r.valorCents)}</span>
                </Link>
              </li>
            ))}
          </ul>
        </>
      )}
      {trabalhos.length > 0 && (
        <>
          <div className="mb-1 text-[11px] uppercase tracking-wide text-zinc-500">Trabalhos</div>
          <ul className="divide-y divide-zinc-100 dark:divide-zinc-800">
            {trabalhos.map((t) => (
              <li key={t.id}>
                <Link to={`/trabalhos/${t.id}`} className="flex items-center justify-between gap-2 rounded-md py-1.5 text-sm hover:bg-zinc-50 dark:hover:bg-zinc-800">
                  <div className="min-w-0 flex-1">
                    <span className="text-xs font-mono text-zinc-500">#{t.numero}</span>{' '}
                    <span className="truncate font-medium">{t.titulo}</span>
                    {t.clienteNome && <span className="ml-2 text-[11px] text-zinc-500">· {t.clienteNome}</span>}
                  </div>
                  <span className="font-semibold tabular-nums">{formatCents(t.valorCents)}</span>
                </Link>
              </li>
            ))}
          </ul>
        </>
      )}
    </div>
  );
}

function DespesasOrfasPanel({ items, onClose }: { items: DespesaOrfa[]; onClose: () => void }) {
  return (
    <div className="col-span-full rounded-xl border border-blue-300 bg-white p-4 dark:border-blue-800/60 dark:bg-zinc-900">
      <div className="mb-3 flex items-center justify-between">
        <h3 className="text-sm font-semibold">Despesas sem trabalho associado</h3>
        <button
          type="button"
          onClick={onClose}
          aria-label="Fechar painel"
          className="rounded-md p-1 text-zinc-500 transition hover:bg-zinc-100 hover:text-zinc-700 focus:outline-none focus-visible:ring-2 focus-visible:ring-brand-400 dark:hover:bg-zinc-800 dark:hover:text-zinc-300"
        >
          <X size={14} strokeWidth={2} />
        </button>
      </div>
      <p className="mb-3 text-xs text-zinc-500">
        Estas despesas não estão ligadas a nenhuma reparação ou trabalho. Clica numa despesa para a editar (e associar a trabalho/reparação) ou apagar. Se for compra de stock ou overhead (renda, internet…), deixa como está.
      </p>
      <ul className="divide-y divide-zinc-100 dark:divide-zinc-800">
        {items.map((d) => (
          <li key={d.id}>
            <Link to={`/despesas?edit=${d.id}`} className="flex items-center justify-between gap-2 rounded-md px-2 py-1.5 text-sm hover:bg-zinc-50 dark:hover:bg-zinc-800">
              <div className="min-w-0 flex-1">
                <div className="truncate font-medium">{d.descricao}</div>
                <div className="text-[11px] text-zinc-500">
                  {formatDateOnly(d.data)}{d.fornecedor && ` · ${d.fornecedor}`}
                </div>
              </div>
              <span className="font-semibold tabular-nums">{formatCents(d.valorCents)}</span>
            </Link>
          </li>
        ))}
      </ul>
    </div>
  );
}
