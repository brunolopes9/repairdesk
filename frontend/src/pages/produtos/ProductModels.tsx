import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Plus, Pencil, Trash2, X, Save, Layers, BatteryCharging } from 'lucide-react';
import { productModelsApi, type ProductModelDto, type CreateOrUpdateModelPayload } from '../../lib/productModels/api';
import { toast } from '../../lib/toast';
import { useConfirm } from '../../components/ConfirmDialog';
import { formatCents, parseEuros } from '../../lib/money';

/**
 * Sprint 359 (Doc 83): gestão de templates de modelo. O conteúdo partilhado
 * (descrição, specs, preço bateria) define-se 1× aqui; as unidades herdam.
 */
export default function ProductModelsPage() {
  const qc = useQueryClient();
  const confirm = useConfirm();
  const [editing, setEditing] = useState<ProductModelDto | null>(null);
  const [creating, setCreating] = useState(false);

  const list = useQuery({ queryKey: ['product-models'], queryFn: () => productModelsApi.list() });

  const deleteMut = useMutation({
    mutationFn: (id: string) => productModelsApi.delete(id),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['product-models'] });
      toast.success('Modelo eliminado.');
    },
    onError: (err) => {
      const e = err as { response?: { data?: { message?: string } } };
      toast.error(e.response?.data?.message ?? 'Erro a eliminar modelo.');
    },
  });

  async function askDelete(m: ProductModelDto) {
    if (m.unitsCount > 0) {
      toast.error(`${m.unitsCount} unidades ligadas a este modelo. Desliga-as antes de apagar.`);
      return;
    }
    const ok = await confirm({
      title: 'Eliminar modelo',
      description: `Eliminar o template "${m.brand} ${m.model}"? As peças no stock não são afectadas.`,
      confirmLabel: 'Eliminar',
      destructive: true,
    });
    if (ok) deleteMut.mutate(m.id);
  }

  return (
    <div className="space-y-4">
      <header className="flex items-center justify-between">
        <div>
          <h1 className="flex items-center gap-2 text-xl font-semibold"><Layers size={20} /> Modelos</h1>
          <p className="text-sm text-zinc-500">
            Conteúdo partilhado por modelo (descrição, specs, preço bateria). Define 1× e as unidades herdam ao importar lotes.
          </p>
        </div>
        <button
          type="button" onClick={() => { setCreating(true); setEditing(null); }}
          className="inline-flex items-center gap-1 rounded-lg bg-brand-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-brand-700"
        >
          <Plus size={14} /> Novo modelo
        </button>
      </header>

      {(creating || editing) && (
        <ModelForm
          initial={editing}
          onClose={() => { setCreating(false); setEditing(null); }}
          onSaved={() => { qc.invalidateQueries({ queryKey: ['product-models'] }); setCreating(false); setEditing(null); }}
        />
      )}

      <div className="grid gap-2">
        {list.isLoading && <p className="text-sm text-zinc-500">A carregar…</p>}
        {list.data?.length === 0 && (
          <p className="text-sm text-zinc-500">Ainda não há modelos. Cria o primeiro para deixar de preencher conteúdo unidade a unidade.</p>
        )}
        {(list.data ?? []).map((m) => (
          <div key={m.id} className="rounded-lg border border-zinc-200 bg-white p-3 dark:border-zinc-700 dark:bg-zinc-900">
            <div className="flex items-center justify-between gap-3">
              <div className="min-w-0">
                <div className="font-medium">{m.brand} {m.model} {!m.active && <span className="text-xs text-zinc-400">(inactivo)</span>}</div>
                <div className="mt-0.5 flex flex-wrap gap-x-3 gap-y-0.5 text-[11px] text-zinc-500">
                  <span>{m.unitsCount} unidade(s)</span>
                  {m.series && <span>série: {m.series}</span>}
                  {m.batteryUpgradePriceCents != null && (
                    <span className="inline-flex items-center gap-1"><BatteryCharging size={11} /> bateria {formatCents(m.batteryUpgradePriceCents)}</span>
                  )}
                  <span>{m.images.length} imagem(ns)</span>
                </div>
              </div>
              <div className="flex shrink-0 gap-1">
                <button type="button" onClick={() => { setEditing(m); setCreating(false); }} className="rounded p-1.5 hover:bg-zinc-100 dark:hover:bg-zinc-800" title="Editar"><Pencil size={14} /></button>
                <button type="button" onClick={() => askDelete(m)} className="rounded p-1.5 text-rose-600 hover:bg-rose-50 dark:hover:bg-rose-950/30" title="Eliminar"><Trash2 size={14} /></button>
              </div>
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}

function ModelForm({ initial, onClose, onSaved }: { initial: ProductModelDto | null; onClose: () => void; onSaved: () => void }) {
  const [brand, setBrand] = useState(initial?.brand ?? '');
  const [model, setModel] = useState(initial?.model ?? '');
  const [descricao, setDescricao] = useState(initial?.descriptionMarkdown ?? '');
  const [series, setSeries] = useState(initial?.series ?? '');
  const [bateria, setBateria] = useState(
    initial?.batteryUpgradePriceCents != null ? (initial.batteryUpgradePriceCents / 100).toFixed(2) : ''
  );

  const saveMut = useMutation({
    mutationFn: () => {
      const payload: CreateOrUpdateModelPayload = {
        brand: brand.trim(),
        model: model.trim(),
        descriptionMarkdown: descricao.trim() || null,
        series: series.trim() || null,
        batteryUpgradePriceCents: bateria.trim() ? parseEuros(bateria) : null,
      };
      return initial ? productModelsApi.update(initial.id, payload) : productModelsApi.create(payload);
    },
    onSuccess: () => { toast.success(initial ? 'Modelo actualizado.' : 'Modelo criado.'); onSaved(); },
    onError: (err) => {
      const e = err as { response?: { data?: { message?: string } } };
      toast.error(e.response?.data?.message ?? 'Erro a guardar modelo.');
    },
  });

  const canSave = brand.trim().length > 0 && model.trim().length > 0;

  return (
    <div className="rounded-lg border border-brand-300 bg-brand-50/30 p-3 dark:border-brand-700 dark:bg-brand-950/20">
      <div className="mb-2 flex items-center justify-between">
        <h2 className="text-sm font-semibold">{initial ? `Editar ${initial.brand} ${initial.model}` : 'Novo modelo'}</h2>
        <button type="button" onClick={onClose} className="rounded p-1 hover:bg-zinc-100 dark:hover:bg-zinc-800"><X size={14} /></button>
      </div>
      <div className="grid gap-2">
        <div className="grid grid-cols-2 gap-2">
          <input type="text" placeholder="Marca (Apple)" value={brand} onChange={(e) => setBrand(e.target.value)} className={inputCls} disabled={!!initial} />
          <input type="text" placeholder="Modelo (iPhone 15)" value={model} onChange={(e) => setModel(e.target.value)} className={inputCls} disabled={!!initial} />
        </div>
        <input type="text" placeholder="Série de marketing (opcional, ex: iPhone 15)" value={series} onChange={(e) => setSeries(e.target.value)} className={inputCls} />
        <label className="text-xs font-medium text-zinc-600 dark:text-zinc-400">Preço bateria nova (€) — deixa vazio se este modelo não tem upgrade</label>
        <input type="text" inputMode="decimal" placeholder="50.00" value={bateria} onChange={(e) => setBateria(e.target.value)} className={inputCls} />
        <textarea rows={4} placeholder="Descrição comercial partilhada (Markdown)" value={descricao} onChange={(e) => setDescricao(e.target.value)} className={inputCls + ' resize-none'} />
        <div className="flex gap-2">
          <button type="button" onClick={() => saveMut.mutate()} disabled={!canSave || saveMut.isPending} className="inline-flex items-center gap-1 rounded-lg bg-brand-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-brand-700 disabled:opacity-50"><Save size={13} /> Guardar</button>
          <button type="button" onClick={onClose} className="rounded-lg border border-zinc-300 px-3 py-1.5 text-sm dark:border-zinc-700">Cancelar</button>
        </div>
        {initial && <p className="text-[11px] text-zinc-400">Marca/Modelo não editáveis (chave do template). Para mudar, cria um novo.</p>}
      </div>
    </div>
  );
}

const inputCls = 'w-full rounded border border-zinc-300 px-2 py-1.5 text-sm disabled:bg-zinc-100 disabled:text-zinc-500 dark:border-zinc-700 dark:bg-zinc-800 dark:disabled:bg-zinc-900';
