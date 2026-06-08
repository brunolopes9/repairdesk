import { useMemo, useState, type ReactNode } from 'react';
import { useQuery } from '@tanstack/react-query';
import {
  Boxes, Cloud, Store, AlertTriangle, FileWarning, Search, ChevronRight, Upload, Plus, Package, ArrowRight, Wand2,
} from 'lucide-react';
import { KpiCard, ViewTabs } from '../../components/ui';
import { liveListOptions } from '../../lib/queryOptions';
import { formatCents } from '../../lib/money';
import { catalogApi, type CatalogKpis, type CatalogParent, type CatalogTab, type CatalogVariant } from '../../lib/catalog/api';
import CatalogDetailPanel from './CatalogDetailPanel';
import { CatalogStockNav } from './CatalogStockNav';

const TABS: Array<{ key: CatalogTab; label: string }> = [
  { key: 'todos', label: 'Todos' },
  { key: 'fisico', label: 'Stock físico' },
  { key: 'virtual', label: 'Stock virtual' },
  { key: 'loja', label: 'Loja online' },
  { key: 'sem-conteudo', label: 'Sem conteúdo' },
  { key: 'critico', label: 'Stock crítico' },
];

const LOJA_BADGE: Record<string, string> = {
  Publicado: 'bg-emerald-100 text-emerald-700 dark:bg-emerald-900/40 dark:text-emerald-300',
  Parcial: 'bg-amber-100 text-amber-700 dark:bg-amber-900/40 dark:text-amber-300',
  Oculto: 'bg-zinc-100 text-zinc-600 dark:bg-zinc-800 dark:text-zinc-300',
  '—': 'bg-zinc-100 text-zinc-500 dark:bg-zinc-800 dark:text-zinc-400',
};

/**
 * Sprint 386 (Doc 87): "Catálogo & Stock" — vista unificada produtos retail + peças técnicas numa
 * árvore pai→variante. Liga ao read model GET /api/catalog (Fase 1). Linhas-pai expansíveis mostram
 * as variantes. O painel direito rico + ações vêm nas fases 3/4.
 */
export default function Catalogo() {
  const [tab, setTab] = useState<CatalogTab>('todos');
  const [q, setQ] = useState('');
  const [categoria, setCategoria] = useState('');
  const [marca, setMarca] = useState('');
  const [expanded, setExpanded] = useState<Set<string>>(new Set());
  const [selectedKey, setSelectedKey] = useState<string | null>(null);

  const catalog = useQuery({
    queryKey: ['catalog', { tab, q, categoria, marca }],
    queryFn: () => catalogApi.list({ tab, q: q.trim() || undefined, categoria: categoria || undefined, marca: marca || undefined }),
    ...liveListOptions,
  });

  const parents = catalog.data?.parents ?? [];
  const kpis = catalog.data?.kpis;
  const selected = parents.find((p) => p.key === selectedKey) ?? parents[0] ?? null;

  // Opções de filtro derivadas do que está carregado (suficiente para v1).
  const categorias = useMemo(() => [...new Set(parents.map((p) => p.categoria).filter(Boolean))].sort(), [parents]);
  const marcas = useMemo(() => [...new Set(parents.map((p) => p.marca).filter((m): m is string => !!m))].sort(), [parents]);

  const pctPublicado = kpis && kpis.totalPublicavel > 0
    ? Math.round((kpis.publicadosLoja / kpis.totalPublicavel) * 100)
    : 0;

  // Catálogo totalmente vazio (sem produtos/peças) vs. só vazio para o filtro atual.
  const semFiltros = !q.trim() && !categoria && !marca && tab === 'todos';
  const catalogoVazio = !catalog.isLoading && parents.length === 0 && semFiltros && (kpis?.totalPublicavel ?? 0) === 0;
  const filtersActive = !semFiltros;
  const tabsWithMeta = TABS.map(({ key, label }) => ({
    key,
    label,
    meta: key === 'todos'
      ? parents.length
      : key === 'fisico'
        ? kpis?.stockFisicoUnidades ?? 0
        : key === 'virtual'
          ? kpis?.stockVirtualUnidades ?? 0
          : key === 'loja'
            ? kpis?.publicadosLoja ?? 0
            : key === 'sem-conteudo'
              ? kpis?.semConteudo ?? 0
              : kpis?.stockCritico ?? 0,
  }));

  function toggle(key: string) {
    setExpanded((cur) => {
      const next = new Set(cur);
      if (next.has(key)) next.delete(key); else next.add(key);
      return next;
    });
  }

  return (
    <div className="space-y-5">
      <div className="flex flex-col gap-3 sm:flex-row sm:items-end sm:justify-between">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">Catálogo &amp; Stock</h1>
          <p className="text-sm text-zinc-500">Produtos, variantes, stock físico, stock virtual e loja online num só catálogo.</p>
        </div>
        <div className="flex gap-2">
          <a href="/produtos" className="flex h-9 items-center gap-1.5 rounded-lg border border-zinc-200 px-3 text-sm font-medium transition hover:bg-zinc-100 dark:border-zinc-800 dark:hover:bg-zinc-800">
            <Upload size={15} /> Importar CSV
          </a>
          <a href="/produtos?new=1" className="flex h-9 items-center gap-1.5 rounded-lg bg-brand-600 px-3 text-sm font-medium text-white shadow-sm transition hover:bg-brand-700">
            <Plus size={16} strokeWidth={2.5} /> Novo produto
          </a>
        </div>
      </div>

      <CatalogStockNav showGuide />

      {/* KPIs */}
      <div className="grid grid-cols-2 gap-3 lg:grid-cols-5">
        <KpiCard icon={Boxes} tone="brand" label="Stock físico" value={`${kpis?.stockFisicoUnidades ?? 0} un`} sub={kpis ? formatCents(kpis.stockFisicoCustoCents) : undefined} />
        <KpiCard icon={Cloud} tone="zinc" label="Stock virtual" value={`${kpis?.stockVirtualUnidades ?? 0} un`} sub="dropship" />
        <KpiCard icon={Store} tone="emerald" label="Publicados na loja" value={String(kpis?.publicadosLoja ?? 0)} sub={`${pctPublicado}% do catálogo`} />
        <KpiCard icon={AlertTriangle} tone={kpis && kpis.stockCritico > 0 ? 'red' : 'zinc'} label="Stock crítico" value={String(kpis?.stockCritico ?? 0)} sub="unidades" />
        <KpiCard icon={FileWarning} tone={kpis && kpis.semConteudo > 0 ? 'amber' : 'zinc'} label="Sem conteúdo" value={String(kpis?.semConteudo ?? 0)} sub="a completar" />
      </div>

      <CatalogCommandBoard kpis={kpis} pctPublicado={pctPublicado} onTab={setTab} />

      <ViewTabs tabs={tabsWithMeta} value={tab} onChange={(value) => setTab(value as CatalogTab)} />

      {/* Filtros */}
      <div className="flex flex-wrap items-center gap-2">
        <select value={categoria} onChange={(e) => setCategoria(e.target.value)} className="h-9 rounded-lg border border-zinc-200 bg-white px-2 text-sm dark:border-zinc-800 dark:bg-zinc-950">
          <option value="">Categoria: Todas</option>
          {categorias.map((c) => <option key={c} value={c}>{c}</option>)}
        </select>
        <select value={marca} onChange={(e) => setMarca(e.target.value)} className="h-9 rounded-lg border border-zinc-200 bg-white px-2 text-sm dark:border-zinc-800 dark:bg-zinc-950">
          <option value="">Marca: Todas</option>
          {marcas.map((m) => <option key={m} value={m}>{m}</option>)}
        </select>
        <label className="relative ml-auto block min-w-[220px] flex-1 sm:flex-none">
          <Search className="pointer-events-none absolute left-2.5 top-2.5 text-zinc-400" size={16} />
          <input
            value={q}
            onChange={(e) => setQ(e.target.value)}
            placeholder="iPhone 15, ecrã, película, SKU, IMEI…"
            className="h-9 w-full rounded-lg border border-zinc-200 bg-white pl-8 pr-3 text-sm outline-none focus:ring-2 focus:ring-brand-400 dark:border-zinc-800 dark:bg-zinc-950"
          />
        </label>
        {filtersActive && (
          <button
            type="button"
            onClick={() => { setQ(''); setCategoria(''); setMarca(''); setTab('todos'); }}
            className="h-9 rounded-lg px-3 text-xs font-medium text-zinc-500 hover:bg-zinc-100 hover:text-zinc-900 dark:hover:bg-zinc-800 dark:hover:text-zinc-100"
          >
            Limpar filtros
          </button>
        )}
      </div>

      {/* Conteúdo: tabela (esq) + painel de detalhe inline (dir) */}
      <div className="grid gap-4 xl:grid-cols-[1fr_400px]">
      <div className="overflow-hidden rounded-xl border border-zinc-200 bg-white dark:border-zinc-800 dark:bg-zinc-900">
        <div className="flex flex-col gap-2 border-b border-zinc-100 px-4 py-3 dark:border-zinc-800 sm:flex-row sm:items-center sm:justify-between">
          <div>
            <p className="text-sm font-semibold text-zinc-950 dark:text-zinc-50">Mapa operacional do catalogo</p>
            <p className="text-xs text-zinc-500">Agrupa produtos pai e variantes para veres stock, publicacao e conteudo num so sitio.</p>
          </div>
          <span className="text-xs font-medium text-zinc-500">{parents.length} grupos nesta vista</span>
        </div>
        <div className="overflow-x-auto">
          <table className="min-w-[860px] w-full text-sm">
            <thead className="border-b border-zinc-200 text-xs text-zinc-500 dark:border-zinc-800">
              <tr>
                <th className="px-4 py-2.5 text-left font-medium">Produto pai</th>
                <th className="px-3 py-2.5 text-center font-medium">Variantes</th>
                <th className="px-3 py-2.5 text-right font-medium">Stock físico</th>
                <th className="px-3 py-2.5 text-right font-medium">Stock virtual</th>
                <th className="px-3 py-2.5 text-center font-medium">Loja online</th>
                <th className="px-3 py-2.5 text-center font-medium">Conteúdo</th>
                <th className="px-4 py-2.5 text-right font-medium">Margem</th>
              </tr>
            </thead>
            <tbody>
              {catalog.isLoading ? (
                <tr><td colSpan={7} className="p-8 text-center text-sm text-zinc-500">A carregar catálogo…</td></tr>
              ) : catalogoVazio ? (
                <tr><td colSpan={7} className="p-12">
                  <div className="mx-auto flex max-w-sm flex-col items-center gap-3 text-center">
                    <span className="grid h-12 w-12 place-items-center rounded-2xl bg-brand-100 text-brand-700 dark:bg-brand-900/40 dark:text-brand-300"><Boxes size={24} /></span>
                    <div>
                      <p className="font-medium">Ainda não tens nada no catálogo</p>
                      <p className="mt-1 text-sm text-zinc-500">Importa um CSV de fornecedor ou cria o primeiro produto. Peças de stock também aparecem aqui.</p>
                    </div>
                    <div className="flex gap-2">
                      <a href="/produtos" className="flex h-9 items-center gap-1.5 rounded-lg border border-zinc-200 px-3 text-sm font-medium transition hover:bg-zinc-100 dark:border-zinc-800 dark:hover:bg-zinc-800"><Upload size={15} /> Importar CSV</a>
                      <a href="/produtos?new=1" className="flex h-9 items-center gap-1.5 rounded-lg bg-brand-600 px-3 text-sm font-medium text-white shadow-sm transition hover:bg-brand-700"><Plus size={16} strokeWidth={2.5} /> Novo produto</a>
                    </div>
                  </div>
                </td></tr>
              ) : parents.length === 0 ? (
                <tr><td colSpan={7} className="p-10 text-center text-sm text-zinc-500">Sem itens para este filtro. <button type="button" onClick={() => { setQ(''); setCategoria(''); setMarca(''); setTab('todos'); }} className="text-brand-600 hover:underline dark:text-brand-400">Limpar filtros</button></td></tr>
              ) : (
                parents.map((p) => (
                  <ParentRow key={p.key} parent={p} open={expanded.has(p.key)} selected={selected?.key === p.key} onToggle={() => toggle(p.key)} onSelect={() => setSelectedKey(p.key)} />
                ))
              )}
            </tbody>
          </table>
        </div>
        {!catalog.isLoading && parents.length > 0 && (
          <div className="border-t border-zinc-100 px-4 py-2.5 text-xs text-zinc-500 dark:border-zinc-800">
            {parents.length} {parents.length === 1 ? 'linha' : 'linhas'} · clica numa linha para ver o detalhe, ou no chevron para expandir as variantes
          </div>
        )}
        </div>

        {/* Painel de detalhe inline (persistente) */}
        {selected ? (
          <CatalogDetailPanel key={selected.key} parent={selected} inline />
        ) : (
          <aside className="hidden items-center justify-center rounded-xl border border-dashed border-zinc-300 bg-white p-8 text-center text-sm text-zinc-400 dark:border-zinc-700 dark:bg-zinc-900 xl:flex">
            Seleciona um produto para ver o detalhe herdado, variantes e ações.
          </aside>
        )}
      </div>
    </div>
  );
}

function CatalogCommandBoard({
  kpis,
  pctPublicado,
  onTab,
}: {
  kpis?: CatalogKpis;
  pctPublicado: number;
  onTab: (tab: CatalogTab) => void;
}) {
  const stockCritico = kpis?.stockCritico ?? 0;
  const semConteudo = kpis?.semConteudo ?? 0;

  return (
    <section className="grid gap-3 lg:grid-cols-3">
      <CommandCard
        icon={Boxes}
        title="Loja fisica"
        eyebrow="Stock real"
        primary={`${kpis?.stockFisicoUnidades ?? 0} un`}
        secondary={kpis ? formatCents(kpis.stockFisicoCustoCents) : '0,00 EUR'}
        tone="brand"
        text="Pecas, acessorios e produtos que existem na oficina. Entram em contagens fisicas e alertas de reposicao."
        actions={(
          <>
            <a href="/stock" className={commandLinkCls}>Abrir stock <ArrowRight size={14} /></a>
            <button type="button" onClick={() => onTab('critico')} className={commandGhostCls}>
              {stockCritico} criticos
            </button>
          </>
        )}
      />
      <CommandCard
        icon={Store}
        title="Loja online"
        eyebrow="Montra e dropship"
        primary={`${kpis?.publicadosLoja ?? 0} publicados`}
        secondary={`${pctPublicado}% completo`}
        tone="emerald"
        text="Produtos retail e variantes que podem aparecer no site. Stock virtual fica aqui, mas nao entra na prateleira."
        actions={(
          <>
            <a href="/produtos" className={commandLinkCls}>Gerir retail <ArrowRight size={14} /></a>
            <button type="button" onClick={() => onTab('virtual')} className={commandGhostCls}>
              {kpis?.stockVirtualUnidades ?? 0} virtuais
            </button>
          </>
        )}
      />
      <CommandCard
        icon={Wand2}
        title="Qualidade do catalogo"
        eyebrow="Conteudo e dados"
        primary={`${semConteudo} em falta`}
        secondary={`${kpis?.totalPublicavel ?? 0} publicaveis`}
        tone={semConteudo > 0 ? 'amber' : 'zinc'}
        text="Foca primeiro fotos, descricao, SEO, preco e visibilidade. O objetivo e uma montra pronta para vender."
        actions={(
          <>
            <button type="button" onClick={() => onTab('sem-conteudo')} className={commandLinkCls}>
              Corrigir conteudo <ArrowRight size={14} />
            </button>
            <a href="/inventario" className={commandGhostCls}>Contagens</a>
          </>
        )}
      />
    </section>
  );
}

function CommandCard({
  icon: Icon,
  title,
  eyebrow,
  primary,
  secondary,
  text,
  tone,
  actions,
}: {
  icon: typeof Boxes;
  title: string;
  eyebrow: string;
  primary: string;
  secondary: string;
  text: string;
  tone: 'brand' | 'emerald' | 'amber' | 'zinc';
  actions: ReactNode;
}) {
  const toneCls = {
    brand: 'border-brand-200 bg-brand-50 text-brand-700 dark:border-brand-900/60 dark:bg-brand-950/30 dark:text-brand-300',
    emerald: 'border-emerald-200 bg-emerald-50 text-emerald-700 dark:border-emerald-900/60 dark:bg-emerald-950/30 dark:text-emerald-300',
    amber: 'border-amber-200 bg-amber-50 text-amber-700 dark:border-amber-900/60 dark:bg-amber-950/30 dark:text-amber-300',
    zinc: 'border-zinc-200 bg-zinc-50 text-zinc-600 dark:border-zinc-800 dark:bg-zinc-950 dark:text-zinc-300',
  }[tone];

  return (
    <article className="rounded-lg border border-zinc-200 bg-white p-4 shadow-sm shadow-black/[0.02] dark:border-zinc-800 dark:bg-zinc-900">
      <div className="flex items-start justify-between gap-3">
        <div>
          <p className="text-[10px] font-semibold uppercase tracking-[0.16em] text-zinc-400">{eyebrow}</p>
          <h2 className="mt-1 text-base font-semibold text-zinc-950 dark:text-zinc-50">{title}</h2>
        </div>
        <span className={`grid h-10 w-10 place-items-center rounded-lg border ${toneCls}`}>
          <Icon size={18} />
        </span>
      </div>
      <div className="mt-4">
        <p className="text-2xl font-semibold tabular-nums text-zinc-950 dark:text-zinc-50">{primary}</p>
        <p className="text-xs text-zinc-500">{secondary}</p>
      </div>
      <p className="mt-3 min-h-[44px] text-xs leading-5 text-zinc-500 dark:text-zinc-400">{text}</p>
      <div className="mt-4 flex flex-wrap gap-2">{actions}</div>
    </article>
  );
}

const commandLinkCls = 'inline-flex h-8 items-center gap-1.5 rounded-md bg-zinc-950 px-2.5 text-xs font-medium text-white transition hover:bg-zinc-800 dark:bg-zinc-50 dark:text-zinc-950 dark:hover:bg-zinc-200';
const commandGhostCls = 'inline-flex h-8 items-center gap-1.5 rounded-md border border-zinc-200 px-2.5 text-xs font-medium text-zinc-600 transition hover:bg-zinc-50 hover:text-zinc-950 dark:border-zinc-800 dark:text-zinc-300 dark:hover:bg-zinc-800 dark:hover:text-zinc-50';

function ParentRow({ parent, open, selected, onToggle, onSelect }: { parent: CatalogParent; open: boolean; selected: boolean; onToggle: () => void; onSelect: () => void }) {
  return (
    <>
      <tr onClick={onSelect} className={`cursor-pointer border-b border-zinc-100 dark:border-zinc-800/60 ${selected ? 'bg-sky-50 dark:bg-sky-950/30' : 'hover:bg-zinc-50 dark:hover:bg-zinc-800/40'}`}>
        <td className="px-4 py-2.5">
          <div className="flex items-center gap-2.5">
            <button type="button" onClick={(e) => { e.stopPropagation(); onToggle(); }} className="grid h-6 w-6 flex-none place-items-center rounded text-zinc-400 transition hover:bg-zinc-100 hover:text-zinc-700 dark:hover:bg-zinc-800" title={open ? 'Recolher variantes' : 'Expandir variantes'} aria-label="Expandir variantes">
              <ChevronRight size={15} className={`transition-transform ${open ? 'rotate-90' : ''}`} />
            </button>
            {parent.imageUrl ? (
              <img src={parent.imageUrl} alt="" className="h-9 w-9 flex-none rounded-md object-cover" />
            ) : (
              <span className="grid h-9 w-9 flex-none place-items-center rounded-md bg-zinc-100 text-zinc-400 dark:bg-zinc-800"><Package size={16} /></span>
            )}
            <div className="min-w-0">
              <div className="flex min-w-0 flex-wrap items-center gap-2">
                <span className="truncate font-medium">{parent.nome}</span>
                <ParentKindBadge kind={parent.kind} />
              </div>
              <div className="truncate text-xs text-zinc-500">
                {parent.subtitle ?? parent.categoria}{parent.skuPai ? ` · SKU ${parent.skuPai}` : ''}
              </div>
            </div>
          </div>
        </td>
        <td className="px-3 py-2.5 text-center text-zinc-600 dark:text-zinc-400">{parent.variantCount}</td>
        <td className="px-3 py-2.5 text-right tabular-nums">
          {parent.stockFisicoUnidades} un
          {parent.valorStockCents > 0 && <div className="text-xs text-zinc-400">{formatCents(parent.valorStockCents)}</div>}
        </td>
        <td className="px-3 py-2.5 text-right tabular-nums text-zinc-500">{parent.stockVirtualUnidades > 0 ? `${parent.stockVirtualUnidades} un` : '—'}</td>
        <td className="px-3 py-2.5 text-center">
          <span className={`rounded-full px-2 py-0.5 text-[11px] font-medium ${LOJA_BADGE[parent.lojaOnline] ?? LOJA_BADGE['—']}`}>{parent.lojaOnline}</span>
        </td>
        <td className="px-3 py-2.5 text-center">
          {parent.conteudo === 'Completo' ? (
            <span className="text-xs font-medium text-emerald-600 dark:text-emerald-400">Completo</span>
          ) : parent.conteudo === 'Incompleto' ? (
            <span className="text-xs font-medium text-amber-600 dark:text-amber-400">Incompleto</span>
          ) : (
            <span className="text-xs text-zinc-400">—</span>
          )}
        </td>
        <td className="px-4 py-2.5 text-right tabular-nums">
          {parent.margemMediaPct != null ? `${parent.margemMediaPct}%` : '—'}
        </td>
      </tr>
      {open && parent.variants.map((v) => <VariantRow key={`${v.kind}-${v.id}`} v={v} />)}
      {open && parent.variants.length === 0 && (
        <tr className="bg-zinc-50/60 dark:bg-zinc-950/40"><td colSpan={7} className="px-4 py-2 pl-12 text-xs italic text-zinc-400">Sem variantes.</td></tr>
      )}
    </>
  );
}

function ParentKindBadge({ kind }: { kind: CatalogParent['kind'] }) {
  const map = {
    model: {
      label: 'Modelo retail',
      cls: 'bg-blue-50 text-blue-700 ring-blue-200 dark:bg-blue-950/40 dark:text-blue-300 dark:ring-blue-900/60',
    },
    'product-group': {
      label: 'Produto solto',
      cls: 'bg-violet-50 text-violet-700 ring-violet-200 dark:bg-violet-950/40 dark:text-violet-300 dark:ring-violet-900/60',
    },
    'part-group': {
      label: 'Peca tecnica',
      cls: 'bg-zinc-100 text-zinc-600 ring-zinc-200 dark:bg-zinc-800 dark:text-zinc-300 dark:ring-zinc-700',
    },
  } satisfies Record<CatalogParent['kind'], { label: string; cls: string }>;
  const item = map[kind];
  return (
    <span className={`inline-flex h-5 flex-none items-center rounded-full px-2 text-[10px] font-semibold ring-1 ${item.cls}`}>
      {item.label}
    </span>
  );
}

function VariantRow({ v }: { v: CatalogVariant }) {
  const descr = [v.cor, v.armazenamento, v.grade].filter(Boolean).join(' · ') || v.sku || '—';
  return (
    <tr className="border-b border-zinc-100 bg-zinc-50/50 text-[13px] dark:border-zinc-800/60 dark:bg-zinc-950/30">
      <td className="px-4 py-2 pl-12">
        <div className="font-medium">{descr}</div>
        <div className="text-xs text-zinc-500">
          {v.sku ? `${v.sku}` : 'sem SKU'}{v.fornecedor ? ` · ${v.fornecedor}` : ''}
        </div>
      </td>
      <td className="px-3 py-2 text-center">
        <span className={`rounded px-1.5 py-0.5 text-[10px] font-medium ${v.tipoStock === 'virtual' ? 'bg-sky-100 text-sky-700 dark:bg-sky-900/40 dark:text-sky-300' : 'bg-zinc-100 text-zinc-600 dark:bg-zinc-800 dark:text-zinc-300'}`}>
          {v.tipoStock === 'virtual' ? 'Virtual' : 'Físico'}
        </span>
      </td>
      <td className={`px-3 py-2 text-right tabular-nums ${v.stockCritico ? 'font-semibold text-rose-600 dark:text-rose-400' : ''}`} colSpan={2}>
        {v.qtd} un{v.stockCritico ? ' ⚠' : ''}
      </td>
      <td className="px-3 py-2 text-center">
        <span className={`inline-block h-2.5 w-2.5 rounded-full ${v.lojaOnline ? 'bg-emerald-500' : 'bg-zinc-300 dark:bg-zinc-600'}`} title={v.lojaOnline ? 'Na loja' : 'Fora da loja'} />
      </td>
      <td className="px-3 py-2 text-center text-xs text-zinc-500">{v.estado}</td>
      <td className="px-4 py-2 text-right tabular-nums">{v.precoVendaCents != null ? formatCents(v.precoVendaCents) : '—'}</td>
    </tr>
  );
}
