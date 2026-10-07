import { useEffect, useState } from 'react';
import { useMutation } from '@tanstack/react-query';
import { isAxiosError } from 'axios';
import Modal from './Modal';
import { despesasApi } from '../lib/despesas/api';
import {
  DESPESA_CATEGORIA,
  DESPESA_LABEL,
  type Despesa,
  type DespesaCategoria,
} from '../lib/despesas/types';
import { parseEuros } from '../lib/money';

export function DespesaFormModal({
  open,
  editing,
  initialRecorrente = false,
  initialCategoria = DESPESA_CATEGORIA.Pecas,
  allowedCategorias,
  onClose,
  onSaved,
}: {
  open: boolean;
  editing?: Despesa | null;
  initialRecorrente?: boolean;
  initialCategoria?: DespesaCategoria;
  allowedCategorias?: readonly DespesaCategoria[];
  onClose: () => void;
  onSaved: () => void;
}) {
  const [descricao, setDescricao] = useState('');
  const [categoria, setCategoria] = useState<DespesaCategoria>(DESPESA_CATEGORIA.Pecas);
  const [valor, setValor] = useState('');
  const [fornecedor, setFornecedor] = useState('');
  const [numeroEncomenda, setNumeroEncomenda] = useState('');
  const [data, setData] = useState(() => new Date().toISOString().slice(0, 10));
  const [notas, setNotas] = useState('');
  const [isRecorrente, setIsRecorrente] = useState(initialRecorrente);
  const [periodicidadeMeses, setPeriodicidadeMeses] = useState<number>(1);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (editing) {
      setDescricao(editing.descricao);
      setCategoria(editing.categoria);
      setValor((editing.valorCents / 100).toFixed(2));
      setFornecedor(editing.fornecedor ?? '');
      setNumeroEncomenda(editing.numeroEncomenda ?? '');
      setData(editing.data.slice(0, 10));
      setNotas(editing.notas ?? '');
      setIsRecorrente(editing.isRecorrente);
      setPeriodicidadeMeses(editing.periodicidadeMeses ?? 1);
    } else if (open) {
      setDescricao('');
      setCategoria(initialCategoria);
      setValor('');
      setFornecedor('');
      setNumeroEncomenda('');
      setData(new Date().toISOString().slice(0, 10));
      setNotas('');
      setIsRecorrente(initialRecorrente);
      setPeriodicidadeMeses(1);
    }
    setError(null);
  }, [editing, initialCategoria, open]);

  const save = useMutation({
    mutationFn: () => {
      const valorCents = parseEuros(valor) ?? 0;
      const payload = {
        descricao: descricao.trim(),
        categoria,
        valorCents,
        data: data ? new Date(data).toISOString() : null,
        fornecedor: fornecedor.trim() || null,
        numeroEncomenda: numeroEncomenda.trim() || null,
        notas: notas.trim() || null,
        // Sprint 176/177: preserva flag COGS quando edita; novas despesas manuais são OpEx.
        isCogs: editing?.isCogs ?? false,
        isRecorrente,
        periodicidadeMeses: isRecorrente ? periodicidadeMeses : null,
      };
      if (editing) {
        return despesasApi.update(editing.id, {
          ...payload,
          data: payload.data ?? new Date().toISOString(),
        });
      }
      return despesasApi.create(payload);
    },
    onSuccess: onSaved,
    onError: (err) => {
      if (isAxiosError(err)) {
        const data = err.response?.data as { detail?: string; errors?: Record<string, string[]> } | undefined;
        if (data?.errors) setError(Object.values(data.errors).flat().join(' '));
        else setError(data?.detail ?? 'Erro');
      }
    },
  });

  const valorCents = parseEuros(valor);
  const categoriaOptions = allowedCategorias?.length
    ? Array.from(new Set([...(editing ? [editing.categoria] : []), ...allowedCategorias]))
    : Array.from(new Set(Object.values(DESPESA_CATEGORIA)));

  return (
    <Modal
      open={open}
      title={editing ? 'Editar despesa' : 'Nova despesa'}
      onClose={onClose}
      footer={<>
        <button type="button" onClick={onClose} className="rounded-md px-3 py-1.5 text-sm text-zinc-600 hover:bg-zinc-100 dark:text-zinc-300">Cancelar</button>
        <button
          type="button"
          disabled={!descricao || !valorCents || save.isPending}
          onClick={() => save.mutate()}
          className="rounded-md bg-brand-600 px-3 py-1.5 text-sm font-medium text-white disabled:opacity-60"
        >
          {save.isPending ? 'A guardar…' : editing ? 'Guardar' : 'Adicionar'}
        </button>
      </>}
    >
      <div className="space-y-3">
        {error && <div className="rounded-lg bg-red-50 px-3 py-2 text-sm text-red-700 dark:bg-red-950/40 dark:text-red-300">{error}</div>}
        <Field label="Descrição *">
          <input value={descricao} onChange={e => setDescricao(e.target.value)} className={inputCls} autoFocus />
        </Field>
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
          <Field label="Categoria">
            <select value={categoria} onChange={e => setCategoria(Number(e.target.value) as DespesaCategoria)} className={inputCls}>
              {categoriaOptions.map((v) => <option key={v} value={v}>{DESPESA_LABEL[v]}</option>)}
            </select>
          </Field>
          <Field label="Valor (€) *">
            <input inputMode="decimal" value={valor} onChange={e => setValor(e.target.value)} placeholder="0,00" className={inputCls} />
          </Field>
        </div>
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
          <Field label="Data">
            <input type="date" value={data} onChange={e => setData(e.target.value)} className={inputCls} />
          </Field>
          <Field label="Fornecedor">
            <input value={fornecedor} onChange={e => setFornecedor(e.target.value)} className={inputCls} placeholder="ex: Tudo4Mobile" />
          </Field>
        </div>
        <Field label="Nº encomenda no fornecedor">
          <input value={numeroEncomenda} onChange={e => setNumeroEncomenda(e.target.value)} className={inputCls} placeholder="opcional — para histórico" />
        </Field>
        <Field label="Recorrência">
          <div className="rounded-lg border border-zinc-200 p-3 dark:border-zinc-800">
            <label className="flex items-center gap-2 text-sm text-zinc-700 dark:text-zinc-300">
              <input type="checkbox" checked={isRecorrente} onChange={e => setIsRecorrente(e.target.checked)} />
              <span>Despesa recorrente</span>
            </label>
            {isRecorrente && (
              <select value={periodicidadeMeses} onChange={e => setPeriodicidadeMeses(Number(e.target.value))} className={`${inputCls} mt-3`}>
                <option value={1}>Mensal</option>
                <option value={3}>Trimestral</option>
                <option value={12}>Anual</option>
              </select>
            )}
          </div>
        </Field>
        <Field label="Notas">
          <textarea rows={2} value={notas} onChange={e => setNotas(e.target.value)} className={inputCls + ' resize-none'} />
        </Field>
      </div>
    </Modal>
  );
}

const inputCls = 'min-h-11 w-full rounded-lg border border-zinc-300 bg-white px-3 py-2 text-sm outline-none focus:border-brand-500 focus:ring-2 focus:ring-brand-200 dark:border-zinc-700 dark:bg-zinc-950';

function Field({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div className="space-y-1">
      <label className="text-xs font-medium uppercase tracking-wide text-zinc-500">{label}</label>
      {children}
    </div>
  );
}
