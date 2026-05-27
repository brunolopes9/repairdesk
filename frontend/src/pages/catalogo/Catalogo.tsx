import { useMemo, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import {
  Boxes, Cloud, Store, AlertTriangle, FileWarning, Search, ChevronRight, Upload, Plus, Package, PanelRight,
} from 'lucide-react';
import { KpiCard } from '../../components/ui';
import { liveListOptions } from '../../lib/queryOptions';
import { formatCents } from '../../lib/money';
import { catalogApi, type CatalogParent, type CatalogTab, type CatalogVariant } from '../../lib/catalog/api';
import CatalogDetailPanel from './CatalogDetailPanel';

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
  const [detail, setDetail] = useState<CatalogParent | null>(null);

  const catalog = useQuery({
    queryKey: ['catalog', { tab, q, categoria, marca }],
    queryFn: () => catalogApi.list({ tab, q: q.trim() || undefined, categoria: categoria || undefined, marca: marca || undefined }),
    ...liveListOptions,
  });

  const parents = catalog.data?.parents ?? [];
  const kpis = catalog.data?.kpis;

  // Opções de filtro derivadas do que está carregado (suficiente para v1).
  const categorias = useMemo(() => [...new Set(parents.map((p) => p.categoria).filter(Boolean))].sort(), [parents]);
  const marcas = useMemo(() => [...new Set(parents.map((p) => p.marca).filter((m): m is string => !!m))].sort(), [parents]);

  const pctPublicado = kpis && kpis.totalPublicavel > 0
    ? Math.round((kpis.publicadosLoja / kpis.totalPublicavel) * 100)
    : 0;

  function toggle(key: string) {
    setExpanded((cur) => {
      const next = new Set(cur);
      next.has(key) ? next.delete(key) : next.add(key);
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

      {/* KPIs */}
      <div className="grid grid-cols-2 gap-3 lg:grid-cols-5">
        <KpiCard icon={Boxes} tone="brand" label="Stock físico" value={`${kpis?.stockFisicoUnidades ?? 0} un`} sub={kpis ? formatCents(kpis.stockFisicoCustoCents) : undefined} />
        <KpiCard icon={Cloud} tone="zinc" label="Stock virtual" value={`${kpis?.stockVirtualUnidades ?? 0} un`} sub="dropship" />
        <KpiCard icon={Store} tone="emerald" label="Publicados na loja" value={String(kpis?.publicadosLoja ?? 0)} sub={`${pctPublicado}% do catálogo`} />
        <KpiCard icon={AlertTriangle} tone={kpis && kpis.stockCritico > 0 ? 'red' : 'zinc'} label="Stock crítico" value={String(kpis?.stockCritico ?? 0)} sub="unidades" />
        <KpiCard icon={FileWarning} tone={kpis && kpis.semConteudo > 0 ? 'amber' : 'zinc'} label="Sem conteúdo" value={String(kpis?.semConteudo ?? 0)} sub="a completar" />
      </div>

      {/* Tabs */}
      <div className="flex gap-1 overflow-x-auto border-b border-zinc-200 dark:border-zinc-800">
        {TABS.map(({ key, label }) => (
          <button
            key={key}
            type="button"
            onClick={() => setTab(key)}
            className={`-mb-px whitespace-nowrap border-b-2 px-3.5 py-2.5 text-sm font-medium transition ${
              tab === key
                ? 'border-brand-600 text-brand-700 dark:border-brand-400 dark:text-brand-300'
                : 'border-transparent text-zinc-500 hover:text-zinc-800 dark:hover:text-zinc-200'
            }`}
          >
            {label}
          </button>
        ))}
      </div>

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
      </div>

      {/* Tabela de linhas-pai */}
      <div className="overflow-hidden rounded-xl border border-zinc-200 bg-white dark:border-zinc-800 dark:bg-zinc-900">
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
              ) : parents.length === 0 ? (
                <tr><td colSpan={7} className="p-10 text-center text-sm text-zinc-500">Sem itens para este filtro.</td></tr>
              ) : (
                parents.map((p) => (
                  <ParentRow key={p.key} parent={p} open={expanded.has(p.key)} onToggle={() => toggle(p.key)} onOpenDetail={() => setDetail(p)} />
                ))
              )}
            </tbody>
          </table>
        </div>
        {!catalog.isLoading && parents.length > 0 && (
          <div className="border-t border-zinc-100 px-4 py-2.5 text-xs text-zinc-500 dark:border-zinc-800">
            {parents.length} {parents.length === 1 ? 'linha' : 'linhas'} · clica para expandir as variantes, ou no ícone do painel para ver o detalhe
          </div>
        )}
      </div>

      {detail && <CatalogDetailPanel parent={detail} onClose={() => setDetail(null)} />}
    </div>
  );
}

function ParentRow({ parent, open, onToggle, onOpenDetail }: { parent: CatalogParent; open: boolean; onToggle: () => void; onOpenDetail: () => void }) {
  return (
    <>
      <tr onClick={onToggle} className="cursor-pointer border-b border-zinc-100 hover:bg-zinc-50 dark:border-zinc-800/60 dark:hover:bg-zinc-800/40">
        <td className="px-4 py-2.5">
          <div className="flex items-center gap-2.5">
            <ChevronRight size={15} className={`flex-none text-zinc-400 transition-transform ${open ? 'rotate-90' : ''}`} />
            {parent.imageUrl ? (
              <img src={parent.imageUrl} alt="" className="h-9 w-9 flex-none rounded-md object-cover" />
            ) : (
              <span className="grid h-9 w-9 flex-none place-items-center rounded-md bg-zinc-100 text-zinc-400 dark:bg-zinc-800"><Package size={16} /></span>
            )}
            <div className="min-w-0">
              <div className="truncate font-medium">{parent.nome}</div>
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
          <div className="flex items-center justify-end gap-2">
            <span>{parent.margemMediaPct != null ? `${parent.margemMediaPct}%` : '—'}</span>
            <button
              type="button"
              onClick={(e) => { e.stopPropagation(); onOpenDetail(); }}
              className="grid h-7 w-7 place-items-center rounded-md text-zinc-400 hover:bg-zinc-100 hover:text-brand-600 dark:hover:bg-zinc-800"
              title="Abrir detalhe"
            >
              <PanelRight size={15} />
            </button>
          </div>
        </td>
      </tr>
      {open && parent.variants.map((v) => <VariantRow key={`${v.kind}-${v.id}`} v={v} />)}
      {open && parent.variants.length === 0 && (
        <tr className="bg-zinc-50/60 dark:bg-zinc-950/40"><td colSpan={7} className="px-4 py-2 pl-12 text-xs italic text-zinc-400">Sem variantes.</td></tr>
      )}
    </>
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
