import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Plus, Trash2, Pencil, X, Save } from 'lucide-react';
import { partKitsApi, type KitDto, type KitItemInput } from '../../lib/partKits/api';
import { stockApi } from '../../lib/stock/api';
import { toast } from '../../lib/toast';
import { formatCents } from '../../lib/money';
import { useConfirm } from '../../components/ConfirmDialog';
import { BackButton } from '../../components/ui';

/**
 * Sprint 353: gestão de kits de peças. Admin define um conjunto pré-definido
 * de peças (ex: "Kit ecrã iPhone 13") para depois aplicar 1-click numa reparação.
 */
export default function PartKitsPage() {
  const qc = useQueryClient();
  const confirm = useConfirm();
  const [editing, setEditing] = useState<KitDto | null>(null);
  const [creating, setCreating] = useState(false);

  const kitsQuery = useQuery({ queryKey: ['part-kits'], queryFn: () => partKitsApi.list() });

  const deleteMut = useMutation({
    mutationFn: (id: string) => partKitsApi.delete(id),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['part-kits'] });
      toast.success('Kit eliminado.');
    },
    onError: (err) => toast.fromError(err, 'Erro a eliminar kit.'),
  });

  async function askDelete(kit: KitDto) {
    const ok = await confirm({
      title: 'Eliminar kit',
      description: `Eliminar "${kit.nome}"? As peças no stock não são afectadas — apenas o agrupamento.`,
      confirmLabel: 'Eliminar',
      destructive: true,
    });
    if (ok) deleteMut.mutate(kit.id);
  }

  return (
    <div className="space-y-4">
      <BackButton to="/definicoes" label="Voltar a Definições" />
      <header className="flex items-center justify-between">
        <div>
          <h1 className="text-xl font-semibold">Kits de peças</h1>
          <p className="text-sm text-zinc-500">
            Conjuntos pré-definidos para aplicar 1-click numa reparação (ex: ecrã + adesivo + parafusos).
          </p>
        </div>
        <button
          type="button"
          onClick={() => { setCreating(true); setEditing(null); }}
          className="inline-flex items-center gap-1 rounded-lg bg-brand-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-brand-700"
        >
          <Plus size={14} /> Novo kit
        </button>
      </header>

      {(creating || editing) && (
        <KitForm
          initial={editing}
          onClose={() => { setCreating(false); setEditing(null); }}
          onSaved={() => {
            qc.invalidateQueries({ queryKey: ['part-kits'] });
            setCreating(false); setEditing(null);
          }}
        />
      )}

      <div className="grid gap-2">
        {kitsQuery.isLoading && <p className="text-sm text-zinc-500">A carregar…</p>}
        {kitsQuery.data?.length === 0 && (
          <p className="text-sm text-zinc-500">Ainda não há kits. Cria o primeiro para acelerar o fluxo do balcão.</p>
        )}
        {(kitsQuery.data ?? []).map((kit) => (
          <div key={kit.id} className="rounded-lg border border-zinc-200 bg-white p-3 dark:border-zinc-700 dark:bg-zinc-900">
            <div className="flex items-center justify-between gap-3">
              <div>
                <div className="font-medium">{kit.nome}</div>
                {kit.descricao && <div className="text-xs text-zinc-500">{kit.descricao}</div>}
                <div className="mt-1 text-[11px] text-zinc-500">
                  {kit.items.length} peças · custo {formatCents(kit.custoTotalCents)}
                  {kit.maoDeObraCents > 0 && <> · mão-de-obra {formatCents(kit.maoDeObraCents)}</>}
                  {' · '}
                  <span className="font-medium text-emerald-700 dark:text-emerald-400">
                    cliente {formatCents(kit.precoEfectivoCents)}
                    {kit.precoFinalCents != null && <> (fixo)</>}
                  </span>
                </div>
              </div>
              <div className="flex gap-1">
                <button type="button" onClick={() => { setEditing(kit); setCreating(false); }} className="rounded p-1.5 hover:bg-zinc-100 dark:hover:bg-zinc-800" title="Editar">
                  <Pencil size={14} />
                </button>
                <button type="button" onClick={() => askDelete(kit)} className="rounded p-1.5 text-rose-600 hover:bg-rose-50 dark:hover:bg-rose-950/30" title="Eliminar">
                  <Trash2 size={14} />
                </button>
              </div>
            </div>
            <ul className="mt-2 divide-y divide-zinc-100 text-xs dark:divide-zinc-800">
              {kit.items.map((i) => (
                <li key={i.partId} className="flex items-center justify-between py-1">
                  <span>{i.partNome}{i.partSku ? ` · ${i.partSku}` : ''}</span>
                  <span className="tabular-nums text-zinc-500">×{i.quantidade}</span>
                </li>
              ))}
            </ul>
          </div>
        ))}
      </div>

    </div>
  );
}

interface FormProps {
  initial: KitDto | null;
  onClose: () => void;
  onSaved: () => void;
}

function KitForm({ initial, onClose, onSaved }: FormProps) {
  const [nome, setNome] = useState(initial?.nome ?? '');
  const [descricao, setDescricao] = useState(initial?.descricao ?? '');
  const [items, setItems] = useState<KitItemInput[]>(
    initial?.items.map((i) => ({ partId: i.partId, quantidade: i.quantidade })) ?? []
  );
  const [partQuery, setPartQuery] = useState('');
  // Sprint 433 (Doc 90 §7.3 Bundles): bundle pricing fields (em euros para o input UX).
  const [maoDeObraEuro, setMaoDeObraEuro] = useState(
    initial?.maoDeObraCents ? (initial.maoDeObraCents / 100).toFixed(2) : '',
  );
  const [maoDeObraDesc, setMaoDeObraDesc] = useState(initial?.maoDeObraDescricao ?? '');
  const [precoFinalEuro, setPrecoFinalEuro] = useState(
    initial?.precoFinalCents != null ? (initial.precoFinalCents / 100).toFixed(2) : '',
  );

  const partsQuery = useQuery({
    queryKey: ['stock-search', partQuery],
    queryFn: () => stockApi.list({ q: partQuery || undefined, pageSize: 10 }),
    enabled: partQuery.length >= 2,
  });

  const saveMut = useMutation({
    mutationFn: () => {
      // Parse euros → cents (S433 bundle pricing).
      const moc = Math.round((Number(maoDeObraEuro.replace(',', '.')) || 0) * 100);
      const pfe = precoFinalEuro.trim() === '' ? null : Math.round((Number(precoFinalEuro.replace(',', '.')) || 0) * 100);
      const payload = {
        nome: nome.trim(),
        descricao: descricao.trim() || null,
        items,
        maoDeObraCents: moc,
        maoDeObraDescricao: maoDeObraDesc.trim() || null,
        precoFinalCents: pfe,
      };
      return initial ? partKitsApi.update(initial.id, payload) : partKitsApi.create(payload);
    },
    onSuccess: () => {
      toast.success(initial ? 'Kit actualizado.' : 'Kit criado.');
      onSaved();
    },
    onError: (err) => {
      const e = err as { response?: { data?: { message?: string } } };
      toast.error(e.response?.data?.message ?? 'Erro a guardar kit.');
    },
  });

  function addItem(partId: string) {
    if (items.some((i) => i.partId === partId)) return;
    setItems([...items, { partId, quantidade: 1 }]);
    setPartQuery('');
  }
  function updateQty(partId: string, qty: number) {
    setItems(items.map((i) => (i.partId === partId ? { ...i, quantidade: Math.max(1, qty) } : i)));
  }
  function removeItem(partId: string) {
    setItems(items.filter((i) => i.partId !== partId));
  }

  // Build map de nome para mostrar nas linhas seleccionadas. Quando estamos a editar,
  // usamos initial.items; quando o user adiciona, usamos o resultado da search.
  const knownNames: Record<string, string> = {};
  for (const i of initial?.items ?? []) knownNames[i.partId] = i.partNome;
  for (const p of partsQuery.data?.items ?? []) knownNames[p.id] = p.nome;

  const canSave = nome.trim().length > 0 && items.length > 0;

  return (
    <div className="rounded-lg border border-brand-300 bg-brand-50/30 p-3 dark:border-brand-700 dark:bg-brand-950/20">
      <div className="mb-2 flex items-center justify-between">
        <h2 className="text-sm font-semibold">{initial ? 'Editar kit' : 'Novo kit'}</h2>
        <button type="button" onClick={onClose} className="rounded p-1 hover:bg-zinc-100 dark:hover:bg-zinc-800">
          <X size={14} />
        </button>
      </div>
      <div className="grid gap-2">
        <input
          type="text" placeholder="Nome (ex: Kit ecrã iPhone 13)" value={nome}
          onChange={(e) => setNome(e.target.value)}
          className="rounded border border-zinc-300 px-2 py-1 text-sm dark:border-zinc-700 dark:bg-zinc-800"
        />
        <input
          type="text" placeholder="Descrição opcional" value={descricao}
          onChange={(e) => setDescricao(e.target.value)}
          className="rounded border border-zinc-300 px-2 py-1 text-sm dark:border-zinc-700 dark:bg-zinc-800"
        />

        {/* Sprint 433 (Doc 90 §7.3): Bundle pricing — mão-de-obra + preço fixo cliente. */}
        <details className="rounded border border-dashed border-zinc-300 px-2 py-1.5 dark:border-zinc-700">
          <summary className="cursor-pointer text-xs font-medium text-zinc-600 dark:text-zinc-400">
            Bundle pricing (opcional) — mão-de-obra + preço cliente
          </summary>
          <div className="mt-2 grid gap-2 sm:grid-cols-3">
            <label className="block text-xs">
              <span className="mb-0.5 block text-zinc-500">Mão-de-obra (€)</span>
              <input
                inputMode="decimal" placeholder="0,00" value={maoDeObraEuro}
                onChange={(e) => setMaoDeObraEuro(e.target.value)}
                className="w-full rounded border border-zinc-300 px-2 py-1 text-sm dark:border-zinc-700 dark:bg-zinc-800"
              />
            </label>
            <label className="block text-xs sm:col-span-2">
              <span className="mb-0.5 block text-zinc-500">Descrição da mão-de-obra</span>
              <input
                type="text" placeholder="ex: Troca ecrã + teste"
                value={maoDeObraDesc}
                onChange={(e) => setMaoDeObraDesc(e.target.value)}
                className="w-full rounded border border-zinc-300 px-2 py-1 text-sm dark:border-zinc-700 dark:bg-zinc-800"
              />
            </label>
            <label className="block text-xs">
              <span className="mb-0.5 block text-zinc-500">Preço fixo cliente (€)</span>
              <input
                inputMode="decimal" placeholder="(soma se vazio)"
                value={precoFinalEuro}
                onChange={(e) => setPrecoFinalEuro(e.target.value)}
                className="w-full rounded border border-zinc-300 px-2 py-1 text-sm dark:border-zinc-700 dark:bg-zinc-800"
              />
            </label>
            <p className="text-[11px] text-zinc-500 sm:col-span-2 sm:self-end">
              Vazio = custo das peças + mão-de-obra. Definido = preço comercial fechado (margem implícita).
            </p>
          </div>
        </details>

        <div>
          <label className="mb-1 block text-xs font-medium text-zinc-600 dark:text-zinc-400">Adicionar peças</label>
          <input
            type="text" placeholder="Procurar (mín. 2 chars)…" value={partQuery}
            onChange={(e) => setPartQuery(e.target.value)}
            className="w-full rounded border border-zinc-300 px-2 py-1 text-sm dark:border-zinc-700 dark:bg-zinc-800"
          />
          {partsQuery.data && partsQuery.data.items.length > 0 && (
            <ul className="mt-1 max-h-40 overflow-y-auto rounded border border-zinc-200 bg-white text-xs dark:border-zinc-700 dark:bg-zinc-900">
              {partsQuery.data.items.map((p) => (
                <li key={p.id}>
                  <button
                    type="button" onClick={() => addItem(p.id)}
                    disabled={items.some((i) => i.partId === p.id)}
                    className="block w-full px-2 py-1 text-left hover:bg-zinc-50 disabled:opacity-50 dark:hover:bg-zinc-800"
                  >
                    {p.nome}{p.sku ? ` · ${p.sku}` : ''} · {formatCents(p.custoUnitarioCents)}
                  </button>
                </li>
              ))}
            </ul>
          )}
        </div>

        {items.length > 0 && (
          <ul className="rounded border border-zinc-200 bg-white text-xs dark:border-zinc-700 dark:bg-zinc-900">
            {items.map((i) => (
              <li key={i.partId} className="flex items-center justify-between gap-2 border-b border-zinc-100 px-2 py-1 last:border-b-0 dark:border-zinc-800">
                <span className="flex-1">{knownNames[i.partId] ?? i.partId}</span>
                <input
                  type="number" min={1} value={i.quantidade}
                  onChange={(e) => updateQty(i.partId, parseInt(e.target.value) || 1)}
                  className="w-16 rounded border border-zinc-300 px-1 py-0.5 text-right text-xs dark:border-zinc-700 dark:bg-zinc-800"
                />
                <button type="button" onClick={() => removeItem(i.partId)} className="rounded p-1 text-rose-600 hover:bg-rose-50 dark:hover:bg-rose-950/30">
                  <X size={11} />
                </button>
              </li>
            ))}
          </ul>
        )}

        <div className="flex gap-2">
          <button
            type="button" onClick={() => saveMut.mutate()}
            disabled={!canSave || saveMut.isPending}
            className="inline-flex items-center gap-1 rounded-lg bg-brand-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-brand-700 disabled:opacity-50"
          >
            <Save size={13} /> Guardar
          </button>
          <button type="button" onClick={onClose} className="rounded-lg border border-zinc-300 px-3 py-1.5 text-sm dark:border-zinc-700">
            Cancelar
          </button>
        </div>
      </div>
    </div>
  );
}
