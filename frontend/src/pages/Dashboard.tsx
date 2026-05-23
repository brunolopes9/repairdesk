import { useMemo, type ReactNode } from 'react';
import { Link } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import {
  AlertTriangle,
  ArrowRight,
  Boxes,
  CheckCircle2,
  Clock3,
  Euro,
  PackageSearch,
  ShieldCheck,
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
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts';
import { stockApi } from '../lib/stock/api';
import { dashboardApi } from '../lib/dashboard/api';
import { useDashboardKpisHoje } from '../lib/dashboard/hooks';
import { formatCents, formatDateOnly } from '../lib/money';
import { EmptyState, PageHeader, Skeleton } from '../components/ui';

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
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 md:grid-cols-3">
          <KpiLinkCard
            to="/reparacoes?estado=Em curso"
            icon={Wrench}
            tone="blue"
            label="Reparacoes em curso"
            value={kpis.data?.reparacoesEmCurso}
            suffix="abertas"
            loading={kpis.isLoading}
            helper="Abre o kanban para destravar diagnostico, pecas e entrega."
          />
          <KpiLinkCard
            to="/reparacoes?estado=Entregue&pagamento=NaoPago"
            icon={Euro}
            tone="emerald"
            label="Valor a receber hoje"
            value={formatCents(kpis.data?.valorAReceberCents)}
            loading={kpis.isLoading}
            helper="Reparacoes entregues hoje ainda marcadas como nao pagas."
          />
          <KpiLinkCard
            to="/stock?lowStock=1"
            icon={AlertTriangle}
            tone={(kpis.data?.stockCriticoCount ?? 0) > 0 ? 'rose' : 'zinc'}
            label="Stock critico"
            value={kpis.data?.stockCriticoCount}
            suffix="pecas"
            loading={kpis.isLoading}
            helper="Pecas activas com stock igual ou abaixo do minimo."
          />
        </div>
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

function KpiLinkCard({
  to,
  icon: Icon,
  tone,
  label,
  value,
  suffix,
  helper,
  loading,
}: {
  to: string;
  icon: LucideIcon;
  tone: Tone;
  label: string;
  value: ReactNode;
  suffix?: string;
  helper: string;
  loading: boolean;
}) {
  const cls = toneClass[tone];
  return (
    <Link
      to={to}
      className={`group flex min-h-44 flex-col justify-between rounded-lg border bg-white p-5 shadow-sm transition hover:-translate-y-0.5 hover:shadow-md dark:bg-zinc-900 ${cls.border}`}
    >
      <div className="flex items-start justify-between gap-3">
        <div className={`grid h-11 w-11 place-items-center rounded-lg ${cls.icon}`}>
          <Icon size={22} strokeWidth={2} />
        </div>
        <ArrowRight size={16} className="text-zinc-400 transition group-hover:translate-x-0.5 group-hover:text-zinc-700 dark:group-hover:text-zinc-200" />
      </div>
      <div>
        <div className="text-sm font-medium text-zinc-500">{label}</div>
        {loading ? (
          <Skeleton className="mt-2 h-8 w-28" />
        ) : (
          <div className="mt-1 flex items-baseline gap-2">
            <span className="text-3xl font-semibold tracking-tight text-zinc-950 dark:text-zinc-50">{value ?? 0}</span>
            {suffix && <span className="text-sm text-zinc-500">{suffix}</span>}
          </div>
        )}
        <p className="mt-2 text-xs leading-5 text-zinc-500">{helper}</p>
      </div>
    </Link>
  );
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
      className={`group flex min-h-36 flex-col rounded-lg border bg-white p-4 shadow-sm transition hover:-translate-y-0.5 hover:shadow-md dark:bg-zinc-900 ${cls.border}`}
    >
      <div className="flex items-start justify-between gap-3">
        <div className={`inline-flex items-center gap-2 rounded-md px-2.5 py-1 text-xs font-medium ${cls.soft}`}>
          <Icon size={14} strokeWidth={2} />
          {label}
        </div>
        <ArrowRight size={15} className="text-zinc-400 transition group-hover:translate-x-0.5 group-hover:text-zinc-700 dark:group-hover:text-zinc-200" />
      </div>
      {loading ? (
        <Skeleton className="mt-5 h-7 w-24" />
      ) : (
        <div className="mt-4 flex items-baseline gap-2">
          <span className="text-2xl font-semibold tracking-tight text-zinc-950 dark:text-zinc-50">{value ?? 0}</span>
          {suffix && <span className="text-xs text-zinc-500">{suffix}</span>}
        </div>
      )}
      {children}
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
