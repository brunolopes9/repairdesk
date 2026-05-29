import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Pencil, Plus, Trash2, Wrench, X } from 'lucide-react';
import { Button } from '../../components/ui/Button';
import { BackButton } from '../../components/ui';
import { useConfirm } from '../../components/ConfirmDialog';
import { toast } from '../../lib/toast';
import { apiErrorMessage } from '../../lib/errors';
import { formatCents } from '../../lib/money';
import { servicesApi, type ServiceItem } from '../../lib/services/api';

/**
 * Sprint 435 (Doc 90 screenshot Services RoApp): catálogo de mão-de-obra/serviços.
 * Bruno define uma vez "Bateria iPhone 13 — €40, garantia 2 anos" e reutiliza em
 * orçamentos/vendas. Hoje obriga a re-escrever cada vez.
 */
export default function Servicos() {
  const qc = useQueryClient();
  const confirm = useConfirm();
  const [showInactive, setShowInactive] = useState(false);
  const [editing, setEditing] = useState<ServiceItem | null>(null);
  const [creating, setCreating] = useState(false);

  const list = useQuery({
    queryKey: ['services', showInactive],
    queryFn: () => servicesApi.list(showInactive),
  });

  const removeMut = useMutation({
    mutationFn: (id: string) => servicesApi.remove(id),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['services'] });
      toast.success('Serviço eliminado.');
    },
    onError: (err) => toast.error(apiErrorMessage(err) || 'Erro ao eliminar.'),
  });

  async function askDelete(s: ServiceItem) {
    if (await confirm({
      title: 'Eliminar serviço?',
      description: `Eliminar "${s.nome}"? Pode quebrar referências em orçamentos antigos — alternativa é marcar inactivo.`,
      destructive: true,
      confirmLabel: 'Eliminar',
    })) removeMut.mutate(s.id);
  }

  return (
    <div className="space-y-5">
      <div>
        <BackButton to="/definicoes" />
        <h1 className="mt-1 flex items-center gap-2 text-2xl font-semibold tracking-tight">
          <Wrench size={24} /> Catálogo de Serviços
        </h1>
        <p className="text-sm text-zinc-500">
          Mão-de-obra pré-definida (preço + garantia) para reutilizar em orçamentos.
        </p>
      </div>

      <div className="flex items-center justify-between gap-2">
        <label className="inline-flex items-center gap-1.5 text-xs">
          <input type="checkbox" checked={showInactive} onChange={(e) => setShowInactive(e.target.checked)} />
          Incluir inactivos
        </label>
        <Button leftIcon={<Plus size={15} />} onClick={() => { setCreating(true); setEditing(null); }}>
          Novo serviço
        </Button>
      </div>

      {(creating || editing) && (
        <ServiceForm
          initial={editing}
          onClose={() => { setCreating(false); setEditing(null); }}
          onSaved={() => {
            qc.invalidateQueries({ queryKey: ['services'] });
            setCreating(false);
            setEditing(null);
          }}
        />
      )}

      {list.isLoading && <p className="text-sm text-zinc-500">A carregar…</p>}
      {!list.isLoading && (list.data?.length ?? 0) === 0 && (
        <div className="rounded-xl border border-dashed border-zinc-300 p-10 text-center text-sm text-zinc-500 dark:border-zinc-700">
          <Wrench className="mx-auto mb-2 text-zinc-400" size={28} />
          Sem serviços. Cria o primeiro para acelerar orçamentos.
        </div>
      )}
      <div className="grid gap-2">
        {(list.data ?? []).map((s) => (
          <div key={s.id} className={`rounded-lg border border-zinc-200 bg-white p-3 dark:border-zinc-800 dark:bg-zinc-900 ${!s.activo ? 'opacity-60' : ''}`}>
            <div className="flex items-center justify-between gap-2">
              <div className="min-w-0">
                <div className="flex items-center gap-2">
                  <span className="font-medium">{s.nome}</span>
                  {!s.activo && (
                    <span className="rounded bg-zinc-200 px-1.5 py-0.5 text-[10px] dark:bg-zinc-700">inactivo</span>
                  )}
                </div>
                {s.descricao && <div className="truncate text-xs text-zinc-500">{s.descricao}</div>}
                <div className="mt-1 text-[11px] text-zinc-500">
                  <span className="font-medium text-emerald-700 dark:text-emerald-400">{formatCents(s.precoCents)}</span>
                  {s.garantiaDiasCliente > 0 && <> · garantia {s.garantiaDiasCliente} dias</>}
                </div>
              </div>
              <div className="flex gap-1">
                <button type="button" onClick={() => { setEditing(s); setCreating(false); }} className="rounded p-1.5 hover:bg-zinc-100 dark:hover:bg-zinc-800" title="Editar">
                  <Pencil size={14} />
                </button>
                <button type="button" onClick={() => askDelete(s)} className="rounded p-1.5 text-rose-600 hover:bg-rose-50 dark:hover:bg-rose-950/30" title="Eliminar">
                  <Trash2 size={14} />
                </button>
              </div>
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}

function ServiceForm({
  initial,
  onClose,
  onSaved,
}: {
  initial: ServiceItem | null;
  onClose: () => void;
  onSaved: () => void;
}) {
  const [nome, setNome] = useState(initial?.nome ?? '');
  const [descricao, setDescricao] = useState(initial?.descricao ?? '');
  const [precoEuro, setPrecoEuro] = useState(initial ? (initial.precoCents / 100).toFixed(2) : '');
  const [garantiaDias, setGarantiaDias] = useState(initial?.garantiaDiasCliente?.toString() ?? '0');
  const [activo, setActivo] = useState(initial?.activo ?? true);

  const saveMut = useMutation({
    mutationFn: () => {
      const cents = Math.round((Number(precoEuro.replace(',', '.')) || 0) * 100);
      const dias = Math.max(0, Math.min(3650, Number(garantiaDias) || 0));
      const payload = {
        nome: nome.trim(),
        descricao: descricao.trim() || null,
        precoCents: cents,
        garantiaDiasCliente: dias,
        activo,
      };
      return initial ? servicesApi.update(initial.id, payload) : servicesApi.create(payload);
    },
    onSuccess: () => {
      toast.success(initial ? 'Serviço actualizado.' : 'Serviço criado.');
      onSaved();
    },
    onError: (err) => toast.error(apiErrorMessage(err) || 'Erro ao guardar.'),
  });

  const input =
    'w-full rounded border border-zinc-300 px-2 py-1 text-sm dark:border-zinc-700 dark:bg-zinc-800';

  return (
    <div className="rounded-lg border border-brand-300 bg-brand-50/30 p-3 dark:border-brand-700 dark:bg-brand-950/20">
      <div className="mb-2 flex items-center justify-between">
        <h2 className="text-sm font-semibold">{initial ? 'Editar serviço' : 'Novo serviço'}</h2>
        <button type="button" onClick={onClose} className="rounded p-1 hover:bg-zinc-100 dark:hover:bg-zinc-800">
          <X size={14} />
        </button>
      </div>
      <div className="grid gap-2 sm:grid-cols-2">
        <label className="block text-xs sm:col-span-2">
          <span className="mb-0.5 block text-zinc-500">Nome *</span>
          <input
            className={input}
            placeholder="ex.: Troca ecrã iPhone 13"
            value={nome}
            onChange={(e) => setNome(e.target.value)}
            maxLength={120}
          />
        </label>
        <label className="block text-xs sm:col-span-2">
          <span className="mb-0.5 block text-zinc-500">Descrição (opcional)</span>
          <input
            className={input}
            placeholder="Detalhe para PDF"
            value={descricao}
            onChange={(e) => setDescricao(e.target.value)}
          />
        </label>
        <label className="block text-xs">
          <span className="mb-0.5 block text-zinc-500">Preço cliente (€) *</span>
          <input
            inputMode="decimal"
            className={input}
            placeholder="0,00"
            value={precoEuro}
            onChange={(e) => setPrecoEuro(e.target.value)}
          />
        </label>
        <label className="block text-xs">
          <span className="mb-0.5 block text-zinc-500">Garantia (dias)</span>
          <input
            type="number"
            min={0}
            max={3650}
            className={input}
            value={garantiaDias}
            onChange={(e) => setGarantiaDias(e.target.value)}
          />
          <span className="mt-0.5 block text-[10px] text-zinc-400">
            DL 84/2021: 730d para produto novo, 1095d para B2C reparações.
          </span>
        </label>
        <label className="inline-flex items-center gap-1.5 text-xs sm:col-span-2">
          <input type="checkbox" checked={activo} onChange={(e) => setActivo(e.target.checked)} />
          Activo
        </label>
      </div>
      <div className="mt-3 flex justify-end gap-2">
        <Button variant="secondary" onClick={onClose}>Cancelar</Button>
        <Button
          loading={saveMut.isPending}
          disabled={nome.trim().length < 2 || !precoEuro}
          onClick={() => saveMut.mutate()}
        >
          Guardar
        </Button>
      </div>
    </div>
  );
}
