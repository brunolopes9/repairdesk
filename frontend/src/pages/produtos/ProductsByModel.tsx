import { useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { ChevronDown, ChevronRight, Pencil, Layers, Save, X, BatteryCharging } from 'lucide-react';
import type { Product } from '../../lib/products/api';
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
        g = { key, brand: p.brand, model: p.model, variants: [], totalStock: 0 };
        map.set(key, g);
      }
      g.variants.push(p);
      g.totalStock += p.stockQuantity;
    }
    return [...map.values()].sort((a, b) => `${a.brand} ${a.model}`.localeCompare(`${b.brand} ${b.model}`));
  }, [items]);

  function toggle(key: string) {
    setOpenKeys((prev) => {
      const next = new Set(prev);
      next.has(key) ? next.delete(key) : next.add(key);
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
          <div key={g.key} className="rounded-lg border border-zinc-200 bg-white dark:border-zinc-700 dark:bg-zinc-900">
            <div className="flex items-center gap-2 px-3 py-2">
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
              <button
                type="button"
                onClick={() => setEditModel({ brand: g.brand, model: g.model })}
                className="inline-flex items-center gap-1 rounded border border-zinc-300 px-2 py-1 text-[11px] hover:bg-zinc-50 dark:border-zinc-700 dark:hover:bg-zinc-800"
                title="Editar conteúdo partilhado (descrição, preço da bateria) — aplica a todas as variantes"
              >
                <Pencil size={11} /> Conteúdo do modelo
              </button>
            </div>

            {aberto && (
              <ul className="border-t border-zinc-100 text-sm dark:border-zinc-800">
                {g.variants.map((v) => (
                  <li key={v.id}>
                    <button
                      type="button"
                      onClick={() => onEditVariant(v.id)}
                      className="flex w-full items-center gap-3 px-3 py-1.5 pl-9 text-left hover:bg-zinc-50 dark:hover:bg-zinc-800/50"
                    >
                      <span className="flex-1 truncate">
                        {[v.storage, v.color, v.supplierGrade ?? gradeLabel(v.grade)].filter(Boolean).join(' · ')}
                        {v.fornecedorNome && <span className="text-zinc-400"> · {v.fornecedorNome}</span>}
                        {!v.active && <span className="ml-1 text-[10px] text-zinc-400">(inactivo)</span>}
                        {!v.mostrarLojaOnline && <span className="ml-1 text-[10px] text-zinc-400">· oculto na loja</span>}
                      </span>
                      <span className="shrink-0 tabular-nums text-[11px] text-zinc-500">{v.stockQuantity} un</span>
                      <span className="shrink-0 tabular-nums font-medium">{formatCents(v.priceCents)}</span>
                    </button>
                  </li>
                ))}
              </ul>
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
