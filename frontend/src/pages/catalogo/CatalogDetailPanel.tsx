import { useMemo, useState } from 'react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { X, Package, Plus, RefreshCw, Upload, Wand2, Store, Pencil, Check } from 'lucide-react';
import { formatCents, parseEuros } from '../../lib/money';
import { toast } from '../../lib/toast';
import { productModelsApi, type ProductModelDto } from '../../lib/productModels/api';
import { catalogApi, type CatalogParent, type CatalogVariant } from '../../lib/catalog/api';

type DetailTab = 'visao' | 'variantes';

/**
 * Sprint 387 (Doc 87): painel direito (drawer) do "Catálogo & Stock". Mostra o conteúdo herdado
 * pelo produto pai (descrição/specs/imagens do ProductModel) + lista de variantes + ações rápidas.
 * As ações navegam para as páginas existentes (toggle/sync reais = Fase 4).
 */
export default function CatalogDetailPanel({ parent, onClose }: { parent: CatalogParent; onClose: () => void }) {
  const [tab, setTab] = useState<DetailTab>('visao');
  const qc = useQueryClient();
  // Overrides otimistas do toggle de loja (key = `${kind}-${id}`), até a lista recarregar.
  const [lojaOverride, setLojaOverride] = useState<Record<string, boolean>>({});
  const [pending, setPending] = useState<Set<string>>(new Set());

  async function toggleLoja(v: CatalogVariant) {
    const key = `${v.kind}-${v.id}`;
    const next = !(lojaOverride[key] ?? v.lojaOnline);
    setLojaOverride((o) => ({ ...o, [key]: next }));
    setPending((s) => new Set(s).add(key));
    try {
      await catalogApi.setLojaOnline(v.kind, v.id, next);
      qc.invalidateQueries({ queryKey: ['catalog'] });
      toast.success(next ? 'Publicado na loja' : 'Removido da loja');
    } catch (err) {
      setLojaOverride((o) => ({ ...o, [key]: !next })); // reverte
      toast.fromError(err, 'Não foi possível mudar a visibilidade na loja.');
    } finally {
      setPending((s) => { const n = new Set(s); n.delete(key); return n; });
    }
  }

  async function saveFields(v: CatalogVariant, priceCents: number | undefined, stockQuantity: number | undefined) {
    try {
      await catalogApi.updateProductFields(v.id, { priceCents, stockQuantity });
      qc.invalidateQueries({ queryKey: ['catalog'] });
      toast.success('Variante atualizada');
    } catch (err) {
      toast.fromError(err, 'Não foi possível guardar.');
      throw err;
    }
  }

  const modelDetail = useQuery({
    queryKey: ['product-model', parent.modelId],
    queryFn: () => productModelsApi.get(parent.modelId!),
    enabled: parent.kind === 'model' && !!parent.modelId,
    staleTime: 60_000,
  });

  return (
    <div className="fixed inset-0 z-50 flex justify-end" role="dialog" aria-modal="true">
      <div className="absolute inset-0 bg-black/30 backdrop-blur-[1px]" onClick={onClose} />
      <aside className="relative flex h-full w-full max-w-md flex-col overflow-y-auto border-l border-zinc-200 bg-white shadow-2xl dark:border-zinc-800 dark:bg-zinc-900">
        {/* Header */}
        <div className="flex items-start gap-3 border-b border-zinc-200 p-4 dark:border-zinc-800">
          {parent.imageUrl ? (
            <img src={parent.imageUrl} alt="" className="h-12 w-12 flex-none rounded-lg object-cover" />
          ) : (
            <span className="grid h-12 w-12 flex-none place-items-center rounded-lg bg-zinc-100 text-zinc-400 dark:bg-zinc-800"><Package size={20} /></span>
          )}
          <div className="min-w-0 flex-1">
            <h2 className="truncate text-base font-semibold">{parent.nome}</h2>
            <div className="mt-0.5 flex flex-wrap items-center gap-1.5 text-xs text-zinc-500">
              <span>{parent.subtitle ?? parent.categoria}</span>
              <span className={`rounded-full px-1.5 py-0.5 text-[10px] font-medium ${parent.lojaOnline === 'Publicado' ? 'bg-emerald-100 text-emerald-700 dark:bg-emerald-900/40 dark:text-emerald-300' : parent.lojaOnline === 'Parcial' ? 'bg-amber-100 text-amber-700 dark:bg-amber-900/40 dark:text-amber-300' : 'bg-zinc-100 text-zinc-600 dark:bg-zinc-800 dark:text-zinc-300'}`}>{parent.lojaOnline}</span>
            </div>
          </div>
          <button type="button" onClick={onClose} className="grid h-9 w-9 flex-none place-items-center rounded-md text-zinc-400 hover:bg-zinc-100 hover:text-zinc-700 dark:hover:bg-zinc-800"><X size={18} /></button>
        </div>

        {/* Tabs */}
        <div className="flex gap-1 border-b border-zinc-200 px-4 dark:border-zinc-800">
          {([['visao', 'Visão geral'], ['variantes', `Variantes (${parent.variantCount})`]] as const).map(([k, label]) => (
            <button
              key={k}
              type="button"
              onClick={() => setTab(k)}
              className={`-mb-px border-b-2 px-2 py-2.5 text-sm font-medium transition ${tab === k ? 'border-brand-600 text-brand-700 dark:border-brand-400 dark:text-brand-300' : 'border-transparent text-zinc-500 hover:text-zinc-800 dark:hover:text-zinc-200'}`}
            >
              {label}
            </button>
          ))}
        </div>

        <div className="flex-1 space-y-4 p-4">
          {tab === 'visao' ? (
            <VisaoGeral parent={parent} model={modelDetail.data} loadingModel={modelDetail.isLoading} />
          ) : (
            <Variantes variants={parent.variants} lojaOverride={lojaOverride} pending={pending} onToggle={toggleLoja} onSaveFields={saveFields} />
          )}
        </div>

        {/* Ações rápidas */}
        <div className="border-t border-zinc-200 p-4 dark:border-zinc-800">
          <p className="mb-2 text-xs font-medium text-zinc-500">Ações rápidas</p>
          <div className="grid grid-cols-2 gap-2">
            <a href="/produtos?new=1" className="flex items-center justify-center gap-1.5 rounded-lg border border-zinc-200 px-2 py-2 text-xs font-medium hover:bg-zinc-50 dark:border-zinc-800 dark:hover:bg-zinc-800"><Plus size={14} /> Nova variante</a>
            <a href="/produtos" className="flex items-center justify-center gap-1.5 rounded-lg border border-zinc-200 px-2 py-2 text-xs font-medium hover:bg-zinc-50 dark:border-zinc-800 dark:hover:bg-zinc-800"><Upload size={14} /> Importar CSV</a>
            <a href="/produtos" className="flex items-center justify-center gap-1.5 rounded-lg border border-zinc-200 px-2 py-2 text-xs font-medium hover:bg-zinc-50 dark:border-zinc-800 dark:hover:bg-zinc-800"><RefreshCw size={14} /> Sincronizar</a>
            <a href="/produtos" className="flex items-center justify-center gap-1.5 rounded-lg border border-zinc-200 px-2 py-2 text-xs font-medium hover:bg-zinc-50 dark:border-zinc-800 dark:hover:bg-zinc-800"><Wand2 size={14} /> Corrigir conteúdo</a>
          </div>
          <p className="mt-2 text-[11px] text-zinc-400">Toggles de loja e sincronização real chegam na próxima fase.</p>
        </div>
      </aside>
    </div>
  );
}

function VisaoGeral({ parent, model, loadingModel }: { parent: CatalogParent; model?: ProductModelDto; loadingModel: boolean }) {
  const specs = useMemo(() => parseSpecs(model?.specsJson), [model?.specsJson]);

  return (
    <div className="space-y-4">
      {/* Resumo de stock */}
      <div className="grid grid-cols-3 gap-2">
        <MiniStat label="Físico" value={`${parent.stockFisicoUnidades}`} sub="un" />
        <MiniStat label="Virtual" value={`${parent.stockVirtualUnidades}`} sub="un" />
        <MiniStat label="Margem" value={parent.margemMediaPct != null ? `${parent.margemMediaPct}%` : '—'} sub="média" />
      </div>

      {parent.kind === 'model' ? (
        <>
          <div>
            <div className="mb-1.5 flex items-center justify-between">
              <h3 className="text-xs font-semibold uppercase tracking-wide text-zinc-500">Conteúdo do produto pai</h3>
              <span className="text-[10px] text-zinc-400">herdado por todas as variantes</span>
            </div>
            {loadingModel ? (
              <p className="text-sm text-zinc-400">A carregar…</p>
            ) : model ? (
              <>
                {model.images.length > 0 && (
                  <div className="mb-3 flex gap-2 overflow-x-auto">
                    {model.images.slice(0, 6).map((img, i) => (
                      <img key={i} src={img.url} alt={img.alt ?? ''} className="h-16 w-16 flex-none rounded-md object-cover" />
                    ))}
                  </div>
                )}
                {model.descriptionMarkdown ? (
                  <p className="whitespace-pre-wrap text-sm text-zinc-600 dark:text-zinc-300">{model.descriptionMarkdown}</p>
                ) : (
                  <p className="text-sm italic text-amber-600 dark:text-amber-400">Sem descrição — produto incompleto para a loja.</p>
                )}
                {specs.length > 0 && (
                  <dl className="mt-3 grid grid-cols-2 gap-x-3 gap-y-1 text-xs">
                    {specs.map(([k, v]) => (
                      <div key={k} className="flex justify-between gap-2 border-b border-zinc-100 py-1 dark:border-zinc-800">
                        <dt className="text-zinc-500">{k}</dt><dd className="text-right font-medium">{v}</dd>
                      </div>
                    ))}
                  </dl>
                )}
                {model.batteryUpgradePriceCents != null && (
                  <p className="mt-3 text-xs text-zinc-500">Bateria nova: <strong>{formatCents(model.batteryUpgradePriceCents)}</strong></p>
                )}
              </>
            ) : (
              <p className="text-sm text-zinc-400">Sem detalhe de modelo.</p>
            )}
          </div>
        </>
      ) : parent.kind === 'part-group' ? (
        <div className="rounded-lg bg-zinc-50 p-3 text-sm text-zinc-500 dark:bg-zinc-950">
          <div className="mb-1 flex items-center gap-1.5 font-medium text-zinc-700 dark:text-zinc-300"><Package size={14} /> Peça técnica</div>
          Stock interno (peças/acessórios). O conteúdo rico de loja aplica-se a produtos retail (modelos).
        </div>
      ) : (
        <div className="rounded-lg bg-zinc-50 p-3 text-sm text-zinc-500 dark:bg-zinc-950">
          <div className="mb-1 flex items-center gap-1.5 font-medium text-zinc-700 dark:text-zinc-300"><Store size={14} /> Produto sem modelo</div>
          Estas variantes não estão ligadas a um modelo-template. Liga-as a um modelo para partilharem descrição e imagens.
        </div>
      )}
    </div>
  );
}

function Variantes({
  variants, lojaOverride, pending, onToggle, onSaveFields,
}: {
  variants: CatalogVariant[];
  lojaOverride: Record<string, boolean>;
  pending: Set<string>;
  onToggle: (v: CatalogVariant) => void;
  onSaveFields: (v: CatalogVariant, priceCents: number | undefined, stockQuantity: number | undefined) => Promise<void>;
}) {
  if (variants.length === 0) return <p className="text-sm italic text-zinc-400">Sem variantes.</p>;
  return (
    <ul className="space-y-2">
      {variants.map((v) => {
        const key = `${v.kind}-${v.id}`;
        return (
          <VarianteItem
            key={key}
            v={v}
            loja={lojaOverride[key] ?? v.lojaOnline}
            isPending={pending.has(key)}
            onToggle={() => onToggle(v)}
            onSaveFields={onSaveFields}
          />
        );
      })}
    </ul>
  );
}

function VarianteItem({
  v, loja, isPending, onToggle, onSaveFields,
}: {
  v: CatalogVariant;
  loja: boolean;
  isPending: boolean;
  onToggle: () => void;
  onSaveFields: (v: CatalogVariant, priceCents: number | undefined, stockQuantity: number | undefined) => Promise<void>;
}) {
  const descr = [v.cor, v.armazenamento, v.grade].filter(Boolean).join(' · ') || v.sku || '—';
  const editavel = v.kind === 'product'; // peças geram-se pelo ledger de stock, não aqui
  const [editing, setEditing] = useState(false);
  const [preco, setPreco] = useState('');
  const [qtd, setQtd] = useState('');
  const [saving, setSaving] = useState(false);

  function abrir() {
    setPreco(v.precoVendaCents != null ? (v.precoVendaCents / 100).toFixed(2).replace('.', ',') : '');
    setQtd(String(v.qtd));
    setEditing(true);
  }

  async function guardar() {
    setSaving(true);
    try {
      const priceCents = preco.trim() ? parseEuros(preco) ?? undefined : undefined;
      const stockQuantity = qtd.trim() ? Math.max(0, Math.trunc(Number(qtd.replace(',', '.')))) : undefined;
      await onSaveFields(v, priceCents, Number.isFinite(stockQuantity!) ? stockQuantity : undefined);
      setEditing(false);
    } catch { /* toast tratado no handler */ }
    finally { setSaving(false); }
  }

  return (
    <li className="rounded-lg border border-zinc-200 p-3 dark:border-zinc-800">
      <div className="flex items-start justify-between gap-2">
        <div className="min-w-0">
          <div className="truncate text-sm font-medium">{descr}</div>
          <div className="truncate text-xs text-zinc-500">{v.sku ?? 'sem SKU'}{v.fornecedor ? ` · ${v.fornecedor}` : ''}</div>
        </div>
        <button
          type="button"
          onClick={onToggle}
          disabled={isPending}
          role="switch"
          aria-checked={loja}
          title={loja ? 'Na loja — clica para esconder' : 'Fora da loja — clica para publicar'}
          className={`relative inline-flex h-5 w-9 flex-none items-center rounded-full transition disabled:opacity-50 ${loja ? 'bg-emerald-500' : 'bg-zinc-300 dark:bg-zinc-600'}`}
        >
          <span className={`inline-block h-4 w-4 transform rounded-full bg-white shadow transition ${loja ? 'translate-x-4' : 'translate-x-0.5'}`} />
        </button>
      </div>

      {editing ? (
        <div className="mt-2 flex items-end gap-2">
          <label className="grid gap-0.5 text-[11px] text-zinc-500">
            Preço (€)
            <input value={preco} onChange={(e) => setPreco(e.target.value)} inputMode="decimal" className="h-8 w-24 rounded border border-zinc-300 px-2 text-sm dark:border-zinc-700 dark:bg-zinc-950" />
          </label>
          <label className="grid gap-0.5 text-[11px] text-zinc-500">
            Stock
            <input value={qtd} onChange={(e) => setQtd(e.target.value)} inputMode="numeric" className="h-8 w-20 rounded border border-zinc-300 px-2 text-sm dark:border-zinc-700 dark:bg-zinc-950" />
          </label>
          <button type="button" onClick={guardar} disabled={saving} className="flex h-8 items-center gap-1 rounded-lg bg-brand-600 px-2.5 text-xs font-medium text-white hover:bg-brand-700 disabled:opacity-50"><Check size={13} /> {saving ? '…' : 'Guardar'}</button>
          <button type="button" onClick={() => setEditing(false)} className="grid h-8 w-8 place-items-center rounded-lg text-zinc-400 hover:bg-zinc-100 dark:hover:bg-zinc-800"><X size={14} /></button>
        </div>
      ) : (
        <div className="mt-2 flex items-center justify-between text-xs">
          <div className="flex items-center gap-2">
            <span className={`rounded px-1.5 py-0.5 font-medium ${v.tipoStock === 'virtual' ? 'bg-sky-100 text-sky-700 dark:bg-sky-900/40 dark:text-sky-300' : 'bg-zinc-100 text-zinc-600 dark:bg-zinc-800 dark:text-zinc-300'}`}>{v.tipoStock === 'virtual' ? 'Virtual' : 'Físico'}</span>
            <span className={v.stockCritico ? 'font-semibold text-rose-600 dark:text-rose-400' : 'text-zinc-500'}>{v.qtd} un{v.stockCritico ? ' ⚠' : ''}</span>
          </div>
          <div className="flex items-center gap-2">
            <span className="font-medium tabular-nums">{v.precoVendaCents != null ? formatCents(v.precoVendaCents) : '—'}</span>
            {editavel ? (
              <button type="button" onClick={abrir} title="Editar preço/stock" className="grid h-6 w-6 place-items-center rounded text-zinc-400 hover:bg-zinc-100 hover:text-brand-600 dark:hover:bg-zinc-800"><Pencil size={12} /></button>
            ) : (
              <span className="text-[10px] text-zinc-400" title="O stock de peças gere-se na página Stock (com movimentos)">via Stock</span>
            )}
          </div>
        </div>
      )}
    </li>
  );
}

function MiniStat({ label, value, sub }: { label: string; value: string; sub: string }) {
  return (
    <div className="rounded-lg border border-zinc-200 p-2.5 text-center dark:border-zinc-800">
      <div className="text-[11px] text-zinc-500">{label}</div>
      <div className="text-lg font-semibold tabular-nums leading-tight">{value}</div>
      <div className="text-[10px] text-zinc-400">{sub}</div>
    </div>
  );
}

function parseSpecs(json: string | null | undefined): Array<[string, string]> {
  if (!json) return [];
  try {
    const obj = JSON.parse(json);
    if (obj && typeof obj === 'object') {
      return Object.entries(obj).map(([k, v]) => [k, String(v)] as [string, string]).slice(0, 12);
    }
  } catch {
    /* ignora JSON inválido */
  }
  return [];
}
