import { useState } from 'react';
import { Link } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { CalendarClock, ExternalLink, Plus, Receipt, RefreshCcw } from 'lucide-react';
import Modal from '../../components/Modal';
import { toast } from '../../lib/toast';
import { formatCents, formatDateOnly } from '../../lib/money';
import { avencasApi, type Avenca, type SaveAvencaForm } from '../../lib/avencas/api';

/**
 * Sprint 546 (Doc 93 #1): avenças do cliente — faturação recorrente (mensalidades de software,
 * manutenção de website). "Emitir agora" cria o Trabalho do período + Fatura (FT) Moloni num
 * clique; a FT entra no ciclo dívida→recibo normal. O cron avisa quando uma avença fica devida.
 */
export function ClienteAvencasSection({ clienteId }: { clienteId: string }) {
  const qc = useQueryClient();
  const [editing, setEditing] = useState<Avenca | null>(null);
  const [createOpen, setCreateOpen] = useState(false);

  const list = useQuery({
    queryKey: ['avencas', clienteId],
    queryFn: () => avencasApi.list(clienteId),
    staleTime: 30_000,
  });

  const emitir = useMutation({
    mutationFn: (a: Avenca) => avencasApi.emitir(a.id),
    onSuccess: (r) => {
      toast.success(
        r.invoiceNumber ? `Fatura ${r.invoiceNumber} emitida` : 'Trabalho do período criado',
        'A avença avançou para o próximo período.',
      );
      qc.invalidateQueries({ queryKey: ['avencas'] });
      qc.invalidateQueries({ queryKey: ['documentos-vendas'] });
    },
    onError: (err) => {
      toast.fromError(err, 'Não foi possível emitir a avença.');
      // Pode ter ficado o Trabalho criado sem fatura (emissão parcial) — refrescar na mesma.
      qc.invalidateQueries({ queryKey: ['avencas'] });
    },
  });

  const items = list.data ?? [];

  return (
    <section className="rounded-lg border border-zinc-200 bg-white p-4 shadow-sm shadow-black/[0.02] dark:border-zinc-800 dark:bg-zinc-900">
      <div className="mb-3 flex items-center justify-between gap-2">
        <div>
          <h2 className="flex items-center gap-2 text-sm font-semibold text-zinc-900 dark:text-zinc-100">
            <RefreshCcw size={15} className="text-brand-600" /> Avenças (faturação recorrente)
          </h2>
          <p className="text-xs text-zinc-500">Mensalidades — o Mender avisa quando estiver na altura e emites a fatura com 1 clique.</p>
        </div>
        <button
          type="button"
          onClick={() => setCreateOpen(true)}
          className="inline-flex items-center gap-1 rounded-lg border border-zinc-200 px-2.5 py-1.5 text-xs font-medium transition hover:bg-zinc-50 dark:border-zinc-700 dark:hover:bg-zinc-800"
        >
          <Plus size={14} /> Avença
        </button>
      </div>

      {list.isLoading ? (
        <p className="text-sm text-zinc-500">A carregar…</p>
      ) : items.length === 0 ? (
        <p className="rounded-md bg-zinc-50 px-3 py-4 text-center text-sm text-zinc-500 dark:bg-zinc-950/60">
          Sem avenças. Cria uma para faturar este cliente todos os meses sem esforço.
        </p>
      ) : (
        <ul className="space-y-2">
          {items.map((a) => (
            <li key={a.id} className="flex flex-col gap-2 rounded-lg border border-zinc-200 px-3 py-2.5 dark:border-zinc-800 sm:flex-row sm:items-center sm:justify-between">
              <button type="button" onClick={() => setEditing(a)} className="min-w-0 flex-1 text-left focus:outline-none focus-visible:ring-2 focus-visible:ring-brand-400">
                <div className="flex flex-wrap items-center gap-2">
                  <span className="truncate font-medium text-zinc-950 dark:text-zinc-50">{a.descricao}</span>
                  {!a.ativa && (
                    <span className="rounded bg-zinc-100 px-1.5 py-0.5 text-[10px] font-semibold text-zinc-600 dark:bg-zinc-800 dark:text-zinc-300">inativa</span>
                  )}
                  {a.devida && (
                    <span className="rounded bg-amber-100 px-1.5 py-0.5 text-[10px] font-semibold text-amber-700 dark:bg-amber-950/40 dark:text-amber-300">devida</span>
                  )}
                </div>
                <div className="mt-0.5 flex flex-wrap items-center gap-x-3 gap-y-0.5 text-xs text-zinc-500">
                  <span className="font-semibold text-zinc-700 dark:text-zinc-300">{formatCents(a.valorCents)}</span>
                  <span>{a.periodicidadeMeses === 1 ? 'mensal' : a.periodicidadeMeses === 3 ? 'trimestral' : a.periodicidadeMeses === 12 ? 'anual' : `${a.periodicidadeMeses} meses`}</span>
                  <span className="inline-flex items-center gap-1"><CalendarClock size={12} /> próxima {formatDateOnly(a.proximaEmissao)}</span>
                  {a.ultimoTrabalhoId && (
                    <Link to={`/trabalhos/${a.ultimoTrabalhoId}`} onClick={(e) => e.stopPropagation()} className="inline-flex items-center gap-1 text-brand-600 hover:underline dark:text-brand-400">
                      último trabalho <ExternalLink size={11} />
                    </Link>
                  )}
                </div>
              </button>
              {a.ativa && (
                <button
                  type="button"
                  disabled={emitir.isPending}
                  onClick={() => {
                    if (confirm(`Emitir "${a.descricao}" (${formatCents(a.valorCents)})?\n\nCria o Trabalho do período ${a.proximaEmissao.slice(5, 7)}/${a.proximaEmissao.slice(0, 4)} e a Fatura (FT) no Moloni.`))
                      emitir.mutate(a);
                  }}
                  className={`inline-flex items-center gap-1 self-start rounded-lg px-2.5 py-1.5 text-xs font-medium text-white transition disabled:opacity-50 sm:self-auto ${a.devida ? 'bg-emerald-600 hover:bg-emerald-700' : 'bg-zinc-400 hover:bg-zinc-500 dark:bg-zinc-700 dark:hover:bg-zinc-600'}`}
                  title={a.devida ? 'O período está devido — emitir agora' : 'Emitir antecipadamente (a avença avança para o período seguinte)'}
                >
                  <Receipt size={13} /> {emitir.isPending ? 'A emitir…' : 'Emitir agora'}
                </button>
              )}
            </li>
          ))}
        </ul>
      )}

      <AvencaFormModal
        open={createOpen || !!editing}
        clienteId={clienteId}
        editing={editing}
        onClose={() => { setCreateOpen(false); setEditing(null); }}
        onSaved={() => { qc.invalidateQueries({ queryKey: ['avencas'] }); setCreateOpen(false); setEditing(null); }}
      />
    </section>
  );
}

function AvencaFormModal({
  open, clienteId, editing, onClose, onSaved,
}: {
  open: boolean;
  clienteId: string;
  editing: Avenca | null;
  onClose: () => void;
  onSaved: () => void;
}) {
  const hoje = new Date().toISOString().slice(0, 10);
  const [descricao, setDescricao] = useState('');
  const [valor, setValor] = useState(0);
  const [ivaRate, setIvaRate] = useState(23);
  const [periodicidade, setPeriodicidade] = useState(1);
  const [proxima, setProxima] = useState(hoje);
  const [ativa, setAtiva] = useState(true);
  const [notas, setNotas] = useState('');
  // Sincroniza o formulário quando se abre para editar (key-reset simples via prop open+editing).
  const [loadedId, setLoadedId] = useState<string | null>(null);
  if (open && editing && loadedId !== editing.id) {
    setLoadedId(editing.id);
    setDescricao(editing.descricao);
    setValor(editing.valorCents / 100);
    setIvaRate(editing.ivaRate);
    setPeriodicidade(editing.periodicidadeMeses);
    setProxima(editing.proximaEmissao.slice(0, 10));
    setAtiva(editing.ativa);
    setNotas(editing.notas ?? '');
  }
  if (!open && loadedId !== null) setLoadedId(null);

  const save = useMutation({
    mutationFn: (form: SaveAvencaForm) =>
      editing ? avencasApi.update(editing.id, form) : avencasApi.create(form),
    onSuccess: () => {
      toast.success(editing ? 'Avença atualizada' : 'Avença criada');
      onSaved();
    },
    onError: (err) => toast.fromError(err, 'Não foi possível guardar a avença.'),
  });

  const inputCls = 'w-full rounded-lg border border-zinc-300 bg-white px-3 py-2 text-sm outline-none focus:border-brand-500 dark:border-zinc-700 dark:bg-zinc-950';

  return (
    <Modal
      open={open}
      title={editing ? 'Editar avença' : 'Nova avença'}
      onClose={onClose}
      footer={<>
        <button type="button" onClick={onClose} className="rounded-md px-3 py-1.5 text-sm text-zinc-600 hover:bg-zinc-100 dark:text-zinc-300">Cancelar</button>
        <button
          type="button"
          disabled={save.isPending || !descricao.trim() || valor <= 0}
          onClick={() => save.mutate({
            clienteId,
            descricao,
            valorCents: Math.round(valor * 100),
            ivaRate,
            categoria: 2, // JobCategory.Software — o caso de uso das avenças
            periodicidadeMeses: periodicidade,
            proximaEmissao: proxima,
            ativa,
            notas: notas || null,
          })}
          className="rounded-md bg-emerald-600 px-3 py-1.5 text-sm font-medium text-white disabled:opacity-60"
        >
          {save.isPending ? 'A guardar…' : 'Guardar'}
        </button>
      </>}
    >
      <div className="space-y-3">
        <label className="block text-sm">
          <span className="mb-1 block font-medium text-zinc-700 dark:text-zinc-300">Descrição</span>
          <input value={descricao} onChange={(e) => setDescricao(e.target.value)} placeholder="Manutenção website" className={inputCls} />
        </label>
        <div className="grid grid-cols-2 gap-3">
          <label className="block text-sm">
            <span className="mb-1 block font-medium text-zinc-700 dark:text-zinc-300">Valor (€, c/ IVA)</span>
            <input type="number" min={0} step="0.01" value={valor || ''} onChange={(e) => setValor(Number(e.target.value) || 0)} className={inputCls} />
          </label>
          <label className="block text-sm">
            <span className="mb-1 block font-medium text-zinc-700 dark:text-zinc-300">IVA</span>
            <select value={ivaRate} onChange={(e) => setIvaRate(Number(e.target.value))} className={inputCls}>
              <option value={23}>23%</option>
              <option value={13}>13%</option>
              <option value={6}>6%</option>
              <option value={0}>0% (isento)</option>
            </select>
          </label>
          <label className="block text-sm">
            <span className="mb-1 block font-medium text-zinc-700 dark:text-zinc-300">Periodicidade</span>
            <select value={periodicidade} onChange={(e) => setPeriodicidade(Number(e.target.value))} className={inputCls}>
              <option value={1}>Mensal</option>
              <option value={3}>Trimestral</option>
              <option value={12}>Anual</option>
            </select>
          </label>
          <label className="block text-sm">
            <span className="mb-1 block font-medium text-zinc-700 dark:text-zinc-300">Próxima emissão</span>
            <input type="date" value={proxima} onChange={(e) => setProxima(e.target.value)} className={inputCls} />
          </label>
        </div>
        <label className="block text-sm">
          <span className="mb-1 block font-medium text-zinc-700 dark:text-zinc-300">Notas (vão para o trabalho)</span>
          <input value={notas} onChange={(e) => setNotas(e.target.value)} className={inputCls} />
        </label>
        <label className="flex items-center gap-2 text-sm">
          <input type="checkbox" checked={ativa} onChange={(e) => setAtiva(e.target.checked)} className="h-4 w-4 rounded border-zinc-300" />
          <span className="text-zinc-700 dark:text-zinc-300">Ativa (o Mender avisa quando estiver devida)</span>
        </label>
      </div>
    </Modal>
  );
}
