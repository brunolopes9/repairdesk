import { useEffect, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { isAxiosError } from 'axios';
import Modal from './Modal';
import { despesasApi } from '../lib/despesas/api';
import { reparacoesApi } from '../lib/reparacoes/api';
import { trabalhosApi } from '../lib/trabalhos/api';
import { REPAIR_STATUS } from '../lib/reparacoes/types';
import { TRABALHO_STATUS } from '../lib/trabalhos/types';
import {
  DESPESA_CATEGORIA,
  DESPESA_LABEL,
  type Despesa,
  type DespesaCategoria,
} from '../lib/despesas/types';
import { formatCents, formatDateOnly, parseEuros } from '../lib/money';
import { SkeletonRow } from './ui';

interface Props {
  trabalhoId?: string;
  reparacaoId?: string;
  invalidateKeys?: readonly unknown[][];
  /** Quando true, oculta botões de adicionar/remover/editar — só leitura. */
  readOnly?: boolean;
}

export default function DespesasImputadas({ trabalhoId, reparacaoId, invalidateKeys = [], readOnly = false }: Props) {
  const qc = useQueryClient();
  const [modalOpen, setModalOpen] = useState(false);
  const [editing, setEditing] = useState<Despesa | null>(null);

  const queryKey = ['despesas-imputadas', trabalhoId ?? null, reparacaoId ?? null] as const;
  const list = useQuery({
    queryKey,
    queryFn: () => despesasApi.list({ trabalhoId, reparacaoId, pageSize: 50 }),
  });

  function invalidate() {
    qc.invalidateQueries({ queryKey });
    qc.invalidateQueries({ queryKey: ['despesas'] });
    qc.invalidateQueries({ queryKey: ['dashboard'] });
    invalidateKeys.forEach((k) => qc.invalidateQueries({ queryKey: k }));
  }

  const remove = useMutation({
    mutationFn: (id: string) => despesasApi.remove(id),
    onSuccess: invalidate,
  });

  const items = list.data?.items ?? [];
  const total = items.reduce((s, d) => s + d.valorCents, 0);

  const isRep = !!reparacaoId;
  // Sprint 115: clarificar diferença para o utilizador. PecasUsadas (stock interno) vs
  // DespesasImputadas (compras específicas ao fornecedor) tinham o mesmo título "Peças usadas"
  // em reparações, parecia duplicado.
  const titulo = isRep ? 'Compras ao fornecedor' : 'Despesas imputadas';
  const emptyMsg = isRep
    ? 'Encomendas específicas para esta reparação (ex: Touch+Frame Samsung A15 da Tudo4Mobile). Diferente de "Peças do stock" — aqui são compras ao fornecedor com nº de encomenda e custo de transporte.'
    : 'Adiciona despesas diretamente ligadas a este trabalho.';

  return (
    <section className="rounded-xl border border-zinc-200 bg-white p-4 dark:border-zinc-800 dark:bg-zinc-900">
      <div className="flex items-center justify-between">
        <h2 className="text-sm font-semibold">{titulo}</h2>
        {!readOnly && (
          <button
            type="button"
            onClick={() => setModalOpen(true)}
            className="min-h-10 rounded-md bg-brand-600 px-3 py-2 text-xs font-medium text-white transition hover:bg-brand-700"
          >
            + Adicionar
          </button>
        )}
      </div>

      {list.isLoading ? (
        <div className="mt-3 rounded-lg border border-zinc-200 dark:border-zinc-800">
          <SkeletonRow columns={2} />
          <SkeletonRow columns={2} />
        </div>
      ) : items.length === 0 ? (
        <div className="mt-3 rounded-lg border border-dashed border-zinc-300 p-3 text-center text-xs text-zinc-500 dark:border-zinc-700">
          {readOnly ? 'Sem registos.' : emptyMsg}
        </div>
      ) : (
        <ul className="mt-2 divide-y divide-zinc-100 dark:divide-zinc-800">
          {items.map((d) => (
            <li key={d.id} className="flex flex-col gap-2 py-2 text-sm sm:flex-row sm:items-center sm:justify-between">
              <button
                type="button"
                disabled={readOnly}
                onClick={() => !readOnly && setEditing(d)}
                className="min-w-0 flex-1 text-left disabled:cursor-default"
              >
                <div className="flex items-center gap-2">
                  <span className="rounded-full bg-zinc-100 px-2 py-0.5 text-[10px] text-zinc-700 dark:bg-zinc-800 dark:text-zinc-300">
                    {DESPESA_LABEL[d.categoria]}
                  </span>
                  <span className="text-[11px] text-zinc-500">{formatDateOnly(d.data)}</span>
                </div>
                <div className="mt-0.5 truncate">{d.descricao}</div>
                {d.fornecedor && (
                  <div className="text-[11px] text-zinc-500">
                    {d.fornecedor}{d.numeroEncomenda && ` · Enc. ${d.numeroEncomenda}`}
                  </div>
                )}
                {!d.fornecedor && d.numeroEncomenda && (
                  <div className="text-[11px] text-zinc-500">Enc. {d.numeroEncomenda}</div>
                )}
              </button>
              <div className="flex items-center gap-2">
                <span className="font-medium text-red-600 dark:text-red-400">−{formatCents(d.valorCents)}</span>
                {!readOnly && (
                  <button
                    type="button"
                    onClick={() => remove.mutate(d.id)}
                    disabled={remove.isPending}
                    className="grid h-10 w-10 place-items-center rounded-md text-xs text-zinc-400 hover:bg-zinc-100 hover:text-zinc-700 dark:hover:bg-zinc-800"
                    aria-label="Remover"
                  >
                    ✕
                  </button>
                )}
              </div>
            </li>
          ))}
        </ul>
      )}

      {items.length > 0 && (
        <div className="mt-2 flex justify-between border-t border-zinc-200 pt-2 text-sm dark:border-zinc-800">
          <span className="text-zinc-500">Total imputado</span>
          <span className="font-semibold text-red-600 dark:text-red-400">−{formatCents(total)}</span>
        </div>
      )}

      <DespesaFormModal
        open={modalOpen}
        trabalhoId={trabalhoId}
        reparacaoId={reparacaoId}
        onClose={() => setModalOpen(false)}
        onSaved={() => { invalidate(); setModalOpen(false); }}
      />

      <DespesaFormModal
        open={!!editing}
        editing={editing}
        trabalhoId={trabalhoId}
        reparacaoId={reparacaoId}
        onClose={() => setEditing(null)}
        onSaved={() => { invalidate(); setEditing(null); }}
      />
    </section>
  );
}

export function DespesaFormModal({
  open,
  editing,
  trabalhoId,
  reparacaoId,
  onClose,
  onSaved,
}: {
  open: boolean;
  editing?: Despesa | null;
  trabalhoId?: string;
  reparacaoId?: string;
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
  const [error, setError] = useState<string | null>(null);
  // Linkagem opcional só aparece quando não há trabalhoId/reparacaoId via props
  const showLinkPicker = !trabalhoId && !reparacaoId && !editing;
  const [linkType, setLinkType] = useState<'none' | 'reparacao' | 'trabalho'>('none');
  const [linkId, setLinkId] = useState<string>('');

  const reparacoesAbertas = useQuery({
    queryKey: ['despesa-link-reparacoes'],
    queryFn: () => reparacoesApi.list({ pageSize: 100 }),
    enabled: open && showLinkPicker,
  });
  const trabalhosAbertos = useQuery({
    queryKey: ['despesa-link-trabalhos'],
    queryFn: () => trabalhosApi.list({ pageSize: 100 }),
    enabled: open && showLinkPicker,
  });

  const reparacaoOptions = (reparacoesAbertas.data?.items ?? []).filter(
    (r) => r.estado !== REPAIR_STATUS.Cancelado,
  );
  const trabalhoOptions = (trabalhosAbertos.data?.items ?? []).filter(
    (t) => t.status !== TRABALHO_STATUS.Cancelado,
  );

  useEffect(() => {
    if (editing) {
      setDescricao(editing.descricao);
      setCategoria(editing.categoria);
      setValor((editing.valorCents / 100).toFixed(2));
      setFornecedor(editing.fornecedor ?? '');
      setNumeroEncomenda(editing.numeroEncomenda ?? '');
      setData(editing.data.slice(0, 10));
      setNotas(editing.notas ?? '');
    } else if (open) {
      setDescricao('');
      setCategoria(DESPESA_CATEGORIA.Pecas);
      setValor('');
      setFornecedor('');
      setNumeroEncomenda('');
      setData(new Date().toISOString().slice(0, 10));
      setNotas('');
      setLinkType('none');
      setLinkId('');
    }
    setError(null);
  }, [editing, open]);

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
        trabalhoId:
          editing?.trabalhoId ??
          trabalhoId ??
          (showLinkPicker && linkType === 'trabalho' && linkId ? linkId : null),
        reparacaoId:
          editing?.reparacaoId ??
          reparacaoId ??
          (showLinkPicker && linkType === 'reparacao' && linkId ? linkId : null),
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

  return (
    <Modal
      open={open}
      title={(() => {
        // Sprint 142: contextualiza o título — no contexto de reparação chama-se "compra ao
        // fornecedor" (Bruno reportou que "despesa" era confuso). Trabalhos e geral mantêm-se.
        const isRep = !!reparacaoId;
        if (editing) return isRep ? 'Editar compra ao fornecedor' : 'Editar despesa';
        return isRep ? 'Adicionar compra ao fornecedor' : 'Imputar despesa';
      })()}
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
              {Object.entries(DESPESA_CATEGORIA).map(([_, v]) => <option key={v} value={v}>{DESPESA_LABEL[v]}</option>)}
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
        {showLinkPicker && (
          <Field label="Associar a (opcional)">
            <div className="space-y-2">
              <div className="flex flex-col gap-2 sm:flex-row">
                <button type="button" onClick={() => { setLinkType('none'); setLinkId(''); }} className={`min-h-11 flex-1 rounded-md border px-3 py-2 text-xs transition ${linkType === 'none' ? 'border-brand-500 bg-brand-50 text-brand-700 dark:border-brand-400 dark:bg-brand-950/30 dark:text-brand-300' : 'border-zinc-300 text-zinc-600 hover:bg-zinc-50 dark:border-zinc-700 dark:text-zinc-400 dark:hover:bg-zinc-900'}`}>Stock / overhead</button>
                <button type="button" onClick={() => { setLinkType('reparacao'); setLinkId(''); }} className={`min-h-11 flex-1 rounded-md border px-3 py-2 text-xs transition ${linkType === 'reparacao' ? 'border-brand-500 bg-brand-50 text-brand-700 dark:border-brand-400 dark:bg-brand-950/30 dark:text-brand-300' : 'border-zinc-300 text-zinc-600 hover:bg-zinc-50 dark:border-zinc-700 dark:text-zinc-400 dark:hover:bg-zinc-900'}`}>Reparação</button>
                <button type="button" onClick={() => { setLinkType('trabalho'); setLinkId(''); }} className={`min-h-11 flex-1 rounded-md border px-3 py-2 text-xs transition ${linkType === 'trabalho' ? 'border-brand-500 bg-brand-50 text-brand-700 dark:border-brand-400 dark:bg-brand-950/30 dark:text-brand-300' : 'border-zinc-300 text-zinc-600 hover:bg-zinc-50 dark:border-zinc-700 dark:text-zinc-400 dark:hover:bg-zinc-900'}`}>Trabalho</button>
              </div>
              {linkType === 'reparacao' && (
                <select value={linkId} onChange={e => setLinkId(e.target.value)} className={inputCls}>
                  <option value="">— escolhe reparação —</option>
                  {reparacaoOptions.map(r => (
                    <option key={r.id} value={r.id}>#{r.numero} · {r.equipamento}{r.cliente?.nome ? ` · ${r.cliente.nome}` : ''}</option>
                  ))}
                </select>
              )}
              {linkType === 'trabalho' && (
                <select value={linkId} onChange={e => setLinkId(e.target.value)} className={inputCls}>
                  <option value="">— escolhe trabalho —</option>
                  {trabalhoOptions.map(t => (
                    <option key={t.id} value={t.id}>#{t.numero} · {t.titulo}</option>
                  ))}
                </select>
              )}
              <p className="text-[11px] text-zinc-500">
                Associa para o custo entrar no lucro real desse trabalho. Deixa em "Stock / overhead" se for compra para inventário ou despesa geral (renda, internet…).
              </p>
            </div>
          </Field>
        )}
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
