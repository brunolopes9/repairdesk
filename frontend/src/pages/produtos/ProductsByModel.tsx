import { useMemo, useState, type ReactNode } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { ChevronDown, ChevronRight, Pencil, Layers, Save, X, BatteryCharging } from 'lucide-react';
import { PRODUCT_SUPPLY_TYPE, type Product } from '../../lib/products/api';
import { productModelsApi, type ProductModelDto } from '../../lib/productModels/api';
import { formatCents, parseEuros } from '../../lib/money';
import { toast } from '../../lib/toast';

interface Props {
  items: Product[];
  onEditVariant: (id: string) => void;
}

interface Grupo {
  key: string;
  brand: string;
  model: string;
  variants: Product[];
  totalStock: number;
  physicalUnits: number;
  virtualVariants: number;
  published: number;
  hidden: number;
  minPriceCents: number | null;
}

/**
 * Sprint 361: vista agrupada da lista de produtos POR MODELO (produto-pai → variantes).
 * Resolve a dor do Bruno: 50× iPhone 15 = 50 linhas a pedir as mesmas fotos/descrição.
 * Agora cada modelo é um grupo colapsável; o conteúdo partilhado (descrição, preço bateria)
 * edita-se no cabeçalho do grupo (o ProductModel), e cada variante só difere em
 * cor/capacidade/grade/fornecedor/stock/preço.
 */
export default function ProductsByModel({ items, onEditVariant }: Props) {
  const [openKeys, setOpenKeys] = useState<Set<string>>(new Set());
  const [editModel, setEditModel] = useState<{ brand: string; model: string } | null>(null);

  // Modelos-template existentes, para mostrar se já têm conteúdo partilhado definido.
  const modelsQuery = useQuery({ queryKey: ['product-models'], queryFn: () => productModelsApi.list(), staleTime: 60_000 });
  const modelByKey = useMemo(() => {
    const m = new Map<string, ProductModelDto>();
    for (const md of modelsQuery.data ?? []) m.set(`${md.brand}|||${md.model}`.toLowerCase(), md);
    return m;
  }, [modelsQuery.data]);

  const grupos = useMemo<Grupo[]>(() => {
    const map = new Map<string, Grupo>();
    for (const p of items) {
      const key = `${p.brand}|||${p.model}`.toLowerCase();
      let g = map.get(key);
      if (!g) {
        g = {
          key,
          brand: p.brand,
          model: p.model,
          variants: [],
          totalStock: 0,
          physicalUnits: 0,
          virtualVariants: 0,
          published: 0,
          hidden: 0,
          minPriceCents: null,
        };
        map.set(key, g);
      }
      g.variants.push(p);
      g.totalStock += p.stockQuantity;
      if (p.supplyType === PRODUCT_SUPPLY_TYPE.Stock) g.physicalUnits += p.stockQuantity;
      if (p.supplyType === PRODUCT_SUPPLY_TYPE.Dropship) g.virtualVariants += 1;
      if (p.active && p.mostrarLojaOnline) g.published += 1;
      if (!p.mostrarLojaOnline) g.hidden += 1;
      g.minPriceCents = g.minPriceCents === null ? p.priceCents : Math.min(g.minPriceCents, p.priceCents);
    }
    return [...map.values()].sort((a, b) => `${a.brand} ${a.model}`.localeCompare(`${b.brand} ${b.model}`));
  }, [items]);

  function toggle(key: string) {
    setOpenKeys((prev) => {
      const next = new Set(prev);
      if (next.has(key)) next.delete(key); else next.add(key);
      return next;
    });
  }

  if (grupos.length === 0) {
    return <p className="py-8 text-center text-sm text-zinc-500">Sem produtos.</p>;
  }

  return (
    <div className="space-y-1.5">
      {grupos.map((g) => {
        const aberto = openKeys.has(g.key);
        const modelo = modelByKey.get(g.key);
        const temConteudo = !!(modelo?.descriptionMarkdown || (modelo?.images.length ?? 0) > 0);
        return (
          <div key={g.key} className="overflow-hidden rounded-lg border border-zinc-200 bg-white shadow-sm shadow-black/[0.02] dark:border-zinc-700 dark:bg-zinc-900">
            <div className="grid gap-3 px-3 py-3 xl:grid-cols-[minmax(0,1fr)_auto_auto]">
              <button type="button" onClick={() => toggle(g.key)} className="flex flex-1 items-center gap-2 text-left">
                {aberto ? <ChevronDown size={16} className="shrink-0 text-zinc-400" /> : <ChevronRight size={16} className="shrink-0 text-zinc-400" />}
                <Layers size={15} className="shrink-0 text-zinc-400" />
                <span className="font-medium">{g.brand} {g.model}</span>
                <span className="text-[11px] text-zinc-500">
                  {g.variants.length} variante(s) · {g.totalStock} un
                </span>
                {modelo?.batteryUpgradePriceCents != null && (
                  <span className="inline-flex items-center gap-1 rounded bg-emerald-50 px-1.5 py-0.5 text-[10px] text-emerald-700 dark:bg-emerald-950/40 dark:text-emerald-300">
                    <BatteryCharging size={10} /> {formatCents(modelo.batteryUpgradePriceCents)}
                  </span>
                )}
                {temConteudo
                  ? <span className="rounded bg-sky-50 px-1.5 py-0.5 text-[10px] text-sky-700 dark:bg-sky-950/40 dark:text-sky-300">conteúdo ✓</span>
                  : <span className="rounded bg-amber-50 px-1.5 py-0.5 text-[10px] text-amber-700 dark:bg-amber-950/40 dark:text-amber-300">sem descrição/fotos</span>}
              </button>
              <div className="flex flex-wrap items-center gap-1.5">
                <ModelStat label="Variantes" value={g.variants.length.toString()} />
                <ModelStat label="Fisico" value={`${g.physicalUnits} un`} tone={g.physicalUnits > 0 ? 'emerald' : 'zinc'} />
                <ModelStat label="Virtual" value={g.virtualVariants.toString()} tone={g.virtualVariants > 0 ? 'sky' : 'zinc'} />
                <ModelStat label="Online" value={`${g.published}/${g.variants.length}`} tone={g.hidden > 0 ? 'amber' : 'emerald'} />
                <ModelStat label="Desde" value={g.minPriceCents != null ? formatCents(g.minPriceCents) : '—'} />
              </div>
              <button
                type="button"
                onClick={() => setEditModel({ brand: g.brand, model: g.model })}
                className="inline-flex min-h-9 items-center justify-center gap-1 rounded-lg border border-zinc-300 px-3 text-xs font-medium hover:bg-zinc-50 dark:border-zinc-700 dark:hover:bg-zinc-800"
                title="Editar conteúdo partilhado (descrição, preço da bateria) — aplica a todas as variantes"
              >
                <Pencil size={12} /> Conteudo
              </button>
            </div>

            {aberto && (
              <div className="border-t border-zinc-100 bg-zinc-50/60 dark:border-zinc-800 dark:bg-zinc-950/40">
                <div className="hidden grid-cols-[minmax(0,1.6fr)_110px_110px_90px_110px] gap-3 px-3 py-2 text-[10px] font-semibold uppercase tracking-[0.16em] text-zinc-400 md:grid">
                  <span>Variante</span>
                  <span>Origem</span>
                  <span>Loja</span>
                  <span className="text-right">Stock</span>
                  <span className="text-right">Preco</span>
                </div>
              <ul className="divide-y divide-zinc-100 bg-white text-sm dark:divide-zinc-800 dark:bg-zinc-900">
                {g.variants.map((v) => (
                  <li key={v.id}>
                    <button
                      type="button"
                      onClick={() => onEditVariant(v.id)}
                      className="grid w-full gap-2 px-3 py-2 text-left hover:bg-zinc-50 dark:hover:bg-zinc-800/50 md:grid-cols-[minmax(0,1.6fr)_110px_110px_90px_110px] md:items-center md:gap-3"
                    >
                      <span className="min-w-0">
                        {[v.storage, v.color, v.supplierGrade ?? gradeLabel(v.grade)].filter(Boolean).join(' · ')}
                        {v.fornecedorNome && <span className="text-zinc-400"> · {v.fornecedorNome}</span>}
                        {!v.active && <span className="ml-1 text-[10px] text-zinc-400">(inactivo)</span>}
                        {!v.mostrarLojaOnline && <span className="ml-1 text-[10px] text-zinc-400">· oculto na loja</span>}
                      </span>
                      <span className="flex flex-wrap gap-1 md:block">
                        <TinyBadge tone={v.supplyType === PRODUCT_SUPPLY_TYPE.Stock ? 'emerald' : 'sky'}>
                          {v.supplyType === PRODUCT_SUPPLY_TYPE.Stock ? 'stock proprio' : 'dropship'}
                        </TinyBadge>
                      </span>
                      <span className="flex flex-wrap gap-1 md:block">
                        <TinyBadge tone={!v.active ? 'zinc' : v.mostrarLojaOnline ? 'emerald' : 'amber'}>
                          {!v.active ? 'inativo' : v.mostrarLojaOnline ? 'publicado' : 'oculto'}
                        </TinyBadge>
                      </span>
                      <span className="tabular-nums text-zinc-600 dark:text-zinc-300 md:text-right">{v.stockQuantity} un</span>
                      <span className="tabular-nums font-semibold text-zinc-950 dark:text-zinc-50 md:text-right">{formatCents(v.priceCents)}</span>
                    </button>
                  </li>
                ))}
              </ul>
              </div>
            )}
          </div>
        );
      })}

      {editModel && (
        <ModelContentModal
          brand={editModel.brand}
          model={editModel.model}
          existing={modelByKey.get(`${editModel.brand}|||${editModel.model}`.toLowerCase()) ?? null}
          onClose={() => setEditModel(null)}
        />
      )}
    </div>
  );
}

function ModelStat({
  label,
  value,
  tone = 'zinc',
}: {
  label: string;
  value: string;
  tone?: 'zinc' | 'emerald' | 'sky' | 'amber';
}) {
  const toneCls = {
    zinc: 'border-zinc-200 bg-zinc-50 text-zinc-700 dark:border-zinc-700 dark:bg-zinc-800 dark:text-zinc-200',
    emerald: 'border-emerald-200 bg-emerald-50 text-emerald-700 dark:border-emerald-900/50 dark:bg-emerald-950/30 dark:text-emerald-300',
    sky: 'border-sky-200 bg-sky-50 text-sky-700 dark:border-sky-900/50 dark:bg-sky-950/30 dark:text-sky-300',
    amber: 'border-amber-200 bg-amber-50 text-amber-700 dark:border-amber-900/50 dark:bg-amber-950/30 dark:text-amber-300',
  }[tone];

  return (
    <span className={`inline-flex min-h-9 min-w-[74px] flex-col justify-center rounded-lg border px-2 text-right ${toneCls}`}>
      <span className="text-[9px] font-semibold uppercase tracking-[0.14em] opacity-70">{label}</span>
      <span className="text-xs font-semibold tabular-nums">{value}</span>
    </span>
  );
}

function TinyBadge({
  tone,
  children,
}: {
  tone: 'zinc' | 'emerald' | 'sky' | 'amber';
  children: ReactNode;
}) {
  const toneCls = {
    zinc: 'bg-zinc-100 text-zinc-600 dark:bg-zinc-800 dark:text-zinc-300',
    emerald: 'bg-emerald-50 text-emerald-700 dark:bg-emerald-950/40 dark:text-emerald-300',
    sky: 'bg-sky-50 text-sky-700 dark:bg-sky-950/40 dark:text-sky-300',
    amber: 'bg-amber-50 text-amber-700 dark:bg-amber-950/40 dark:text-amber-300',
  }[tone];

  return (
    <span className={`inline-flex items-center gap-1 rounded-md px-1.5 py-0.5 text-[10px] font-medium ${toneCls}`}>
      {children}
    </span>
  );
}

function gradeLabel(grade: number): string {
  // Mapa curto (ProductGrade enum) — suficiente para a linha de variante.
  return ['Sealed', 'A++', 'A+', 'A', 'B+', 'B', 'C+', 'C'][grade] ?? `Grade ${grade}`;
}

/** Modal para editar o conteúdo partilhado do modelo (find-or-create por brand+model). */
function ModelContentModal({ brand, model, existing, onClose }: { brand: string; model: string; existing: ProductModelDto | null; onClose: () => void }) {
  const qc = useQueryClient();
  const [descricao, setDescricao] = useState(existing?.descriptionMarkdown ?? '');
  const [series, setSeries] = useState(existing?.series ?? '');
  const [bateria, setBateria] = useState(
    existing?.batteryUpgradePriceCents != null ? (existing.batteryUpgradePriceCents / 100).toFixed(2) : ''
  );

  const saveMut = useMutation({
    mutationFn: () => {
      const payload = {
        brand, model,
        descriptionMarkdown: descricao.trim() || null,
        series: series.trim() || null,
        batteryUpgradePriceCents: bateria.trim() ? parseEuros(bateria) : null,
      };
      return existing ? productModelsApi.update(existing.id, payload) : productModelsApi.create(payload);
    },
    onSuccess: () => {
      toast.success('Conteúdo do modelo guardado — aplica a todas as variantes.');
      qc.invalidateQueries({ queryKey: ['product-models'] });
      onClose();
    },
    onError: (err) => {
      const e = err as { response?: { data?: { message?: string } } };
      toast.error(e.response?.data?.message ?? 'Erro a guardar.');
    },
  });

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/40 p-4" onClick={onClose}>
      <div className="w-full max-w-lg rounded-xl bg-white p-4 shadow-xl dark:bg-zinc-900" onClick={(e) => e.stopPropagation()}>
        <div className="mb-3 flex items-center justify-between">
          <h2 className="text-sm font-semibold">Conteúdo partilhado — {brand} {model}</h2>
          <button type="button" onClick={onClose} className="rounded p-1 hover:bg-zinc-100 dark:hover:bg-zinc-800"><X size={16} /></button>
        </div>
        <p className="mb-3 text-[11px] text-zinc-500">
          Define 1× aqui. Todas as variantes ({brand} {model}) herdam isto na loja online — descrição, fotos e preço da bateria. As variantes só mudam cor/capacidade/grade/fornecedor/preço/stock.
        </p>
        <div className="space-y-2">
          <label className="block text-xs font-medium text-zinc-600 dark:text-zinc-400">Série de marketing (opcional)</label>
          <input type="text" value={series} onChange={(e) => setSeries(e.target.value)} placeholder="ex: iPhone 15" className={inputCls} />
          <label className="block text-xs font-medium text-zinc-600 dark:text-zinc-400">Preço bateria nova (€) — vazio se não há upgrade</label>
          <input type="text" inputMode="decimal" value={bateria} onChange={(e) => setBateria(e.target.value)} placeholder="50.00" className={inputCls} />
          <label className="block text-xs font-medium text-zinc-600 dark:text-zinc-400">Descrição comercial partilhada (Markdown)</label>
          <textarea rows={5} value={descricao} onChange={(e) => setDescricao(e.target.value)} placeholder="Este modelo tem..." className={inputCls + ' resize-none'} />
          <div className="flex gap-2 pt-1">
            <button type="button" onClick={() => saveMut.mutate()} disabled={saveMut.isPending} className="inline-flex items-center gap-1 rounded-lg bg-brand-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-brand-700 disabled:opacity-50">
              <Save size={13} /> Guardar
            </button>
            <button type="button" onClick={onClose} className="rounded-lg border border-zinc-300 px-3 py-1.5 text-sm dark:border-zinc-700">Cancelar</button>
          </div>
        </div>
      </div>
    </div>
  );
}

const inputCls = 'w-full rounded border border-zinc-300 px-2 py-1.5 text-sm dark:border-zinc-700 dark:bg-zinc-800';
