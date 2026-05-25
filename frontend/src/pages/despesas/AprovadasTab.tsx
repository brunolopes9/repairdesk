import { useEffect, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient, keepPreviousData } from '@tanstack/react-query';
import { Plus, ReceiptText, Search } from 'lucide-react';
import Modal from '../../components/Modal';
import { Button, EmptyState, PageHeader, SkeletonCard } from '../../components/ui';
import { DespesaFormModal } from '../../components/DespesasImputadas';
import { despesasApi } from '../../lib/despesas/api';
import {
  DESPESA_CATEGORIA,
  DESPESA_LABEL,
  type Despesa,
  type DespesaCategoria,
} from '../../lib/despesas/types';
import { formatCents, formatDateOnly } from '../../lib/money';

interface AprovadasTabProps {
  title?: string;
  description?: string;
  categoriaIn?: readonly DespesaCategoria[];
  includeSupplierInvoiceImports?: boolean;
  excludeSupplierInvoiceImports?: boolean;
  allowedCategorias?: readonly DespesaCategoria[];
  initialCategoria?: DespesaCategoria;
  createLabel?: string;
  emptyTitle?: string;
  emptyDescription?: string;
  showCategoriaFilter?: boolean;
  showRecurringToggle?: boolean;
}

export default function AprovadasTab({
  title = 'Despesas aprovadas',
  description = 'Custos aprovados e prontos para relatorios financeiros.',
  categoriaIn,
  includeSupplierInvoiceImports = false,
  excludeSupplierInvoiceImports = false,
  allowedCategorias,
  initialCategoria = DESPESA_CATEGORIA.Pecas,
  createLabel = 'Nova',
  emptyTitle = 'Ainda nao ha registos',
  emptyDescription = 'Cria o primeiro registo para manter o custo operacional visivel.',
  showCategoriaFilter = true,
  showRecurringToggle = false,
}: AprovadasTabProps) {
  const qc = useQueryClient();
  const [params, setParams] = useSearchParams();
  const editId = params.get('edit');
  const [categoria, setCategoria] = useState<DespesaCategoria | null>(null);
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);
  const [onlyRecurring, setOnlyRecurring] = useState(false);
  const [createOpen, setCreateOpen] = useState(false);
  const [editing, setEditing] = useState<Despesa | null>(null);
  const [confirmDelete, setConfirmDelete] = useState<Despesa | null>(null);

  const list = useQuery({
    queryKey: ['despesas', categoriaIn ?? null, includeSupplierInvoiceImports, excludeSupplierInvoiceImports, categoria, search, onlyRecurring, page],
    queryFn: () => despesasApi.list({
      q: search,
      categoria,
      categoriaIn,
      includeSupplierInvoiceImports,
      excludeSupplierInvoiceImports,
      isRecorrente: showRecurringToggle && onlyRecurring ? true : undefined,
      page,
      pageSize: 20,
    }),
    placeholderData: keepPreviousData,
  });

  const editTarget = useQuery({
    queryKey: ['despesa', editId],
    queryFn: () => despesasApi.get(editId!),
    enabled: !!editId,
  });

  useEffect(() => {
    if (editTarget.data && !editing) setEditing(editTarget.data);
  }, [editTarget.data, editing]);

  function clearEditParam() {
    if (!editId) return;
    const next = new URLSearchParams(params);
    next.delete('edit');
    setParams(next, { replace: true });
  }

  const remove = useMutation({
    mutationFn: (d: Despesa) => despesasApi.remove(d.id),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['despesas'] });
      qc.invalidateQueries({ queryKey: ['dashboard'] });
      setConfirmDelete(null);
    },
  });

  function invalidateAfterSave() {
    qc.invalidateQueries({ queryKey: ['despesas'] });
    qc.invalidateQueries({ queryKey: ['dashboard'] });
  }

  const items = list.data?.items ?? [];
  const total = list.data?.total ?? 0;
  const lastPage = Math.max(1, Math.ceil(total / 20));
  const totalCents = items.reduce((sum, d) => sum + d.valorCents, 0);
  const categoryOptions = allowedCategorias?.length
    ? allowedCategorias
    : Array.from(new Set(Object.values(DESPESA_CATEGORIA)));

  return (
    <div className="space-y-4">
      <PageHeader
        title={title}
        description={description}
        meta={<span className="text-sm text-zinc-500">{total} {total === 1 ? 'registo' : 'registos'} - pagina: {formatCents(totalCents)}</span>}
        actions={<Button type="button" onClick={() => setCreateOpen(true)} leftIcon={<Plus size={15} />}>{createLabel}</Button>}
      />

      <div className="flex flex-col gap-2 sm:flex-row sm:items-center">
        {showCategoriaFilter && (
          <select
            value={categoria ?? ''}
            onChange={(e) => { setCategoria(e.target.value === '' ? null : (Number(e.target.value) as DespesaCategoria)); setPage(1); }}
            className="min-h-11 rounded-lg border border-zinc-300 bg-white px-3 py-2 text-sm outline-none focus:border-brand-500 focus:ring-2 focus:ring-brand-200 focus-visible:ring-2 focus-visible:ring-brand-400 dark:border-zinc-700 dark:bg-zinc-950"
          >
            <option value="">Todas categorias</option>
            {categoryOptions.map((v) => (
              <option key={v} value={v}>{DESPESA_LABEL[v]}</option>
            ))}
          </select>
        )}
        <div className="relative min-w-0 flex-1">
          <Search size={16} className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-zinc-400" />
          <input
            type="search"
            placeholder="Pesquisar descricao, fornecedor..."
            value={search}
            onChange={(e) => { setSearch(e.target.value); setPage(1); }}
            className="min-h-11 w-full rounded-lg border border-zinc-300 bg-white py-2 pl-9 pr-3 text-sm outline-none focus:border-brand-500 focus:ring-2 focus:ring-brand-200 focus-visible:ring-2 focus-visible:ring-brand-400 dark:border-zinc-700 dark:bg-zinc-950"
          />
        </div>
        {showRecurringToggle && (
          <label className="flex min-h-11 items-center gap-2 rounded-lg border border-zinc-300 bg-white px-3 py-2 text-sm text-zinc-700 dark:border-zinc-700 dark:bg-zinc-950 dark:text-zinc-300">
            <input
              type="checkbox"
              checked={onlyRecurring}
              onChange={(e) => { setOnlyRecurring(e.target.checked); setPage(1); }}
            />
            <span>Mostrar recorrentes</span>
          </label>
        )}
      </div>

      <ul className="space-y-2">
        {list.isLoading && Array.from({ length: 4 }).map((_, index) => <SkeletonCard key={index} />)}
        {items.map((d) => (
          <li key={d.id} className="flex flex-col gap-3 rounded-xl border border-zinc-200 bg-white dark:border-zinc-800 dark:bg-zinc-900 sm:flex-row sm:items-center sm:justify-between">
            <button
              type="button"
              onClick={() => setEditing(d)}
              className="min-w-0 flex-1 px-4 py-3 text-left focus:outline-none focus-visible:ring-2 focus-visible:ring-brand-400"
            >
              <div className="flex flex-wrap items-center gap-2">
                <span className="rounded-full bg-zinc-100 px-2 py-0.5 text-[10px] font-medium text-zinc-700 dark:bg-zinc-800 dark:text-zinc-300">
                  {DESPESA_LABEL[d.categoria]}
                </span>
                <span className="text-xs text-zinc-500">{formatDateOnly(d.data)}</span>
                {(d.trabalhoId || d.reparacaoId) && (
                  <span className="rounded bg-blue-100 px-1.5 py-0.5 text-[9px] font-medium text-blue-700 dark:bg-blue-950/40 dark:text-blue-300">
                    {d.reparacaoId ? 'reparacao' : 'trabalho'}
                  </span>
                )}
                {d.isRecorrente && (
                  <span className="rounded bg-emerald-100 px-1.5 py-0.5 text-[9px] font-medium text-emerald-700 dark:bg-emerald-950/40 dark:text-emerald-300">
                    recorrente
                  </span>
                )}
                {d.isCogs && (
                  <span className="rounded bg-amber-100 px-1.5 py-0.5 text-[9px] font-medium text-amber-700 dark:bg-amber-950/40 dark:text-amber-300">
                    COGS
                  </span>
                )}
              </div>
              <div className="mt-1 truncate font-medium">{d.descricao}</div>
              {d.fornecedor && (
                <div className="text-xs text-zinc-500">
                  {d.fornecedor}{d.numeroEncomenda && ` - Enc. ${d.numeroEncomenda}`}
                </div>
              )}
            </button>
            <div className="flex items-center justify-between gap-3 px-4 pb-3 sm:justify-end sm:pb-0">
              <span className="font-semibold text-red-600 dark:text-red-400">-{formatCents(d.valorCents)}</span>
              <button
                type="button"
                onClick={() => setConfirmDelete(d)}
                className="grid h-10 w-10 place-items-center rounded-md text-xs text-zinc-500 hover:bg-zinc-100 focus:outline-none focus-visible:ring-2 focus-visible:ring-brand-400 dark:hover:bg-zinc-800"
                aria-label="Apagar"
              >
                x
              </button>
            </div>
          </li>
        ))}
        {items.length === 0 && !list.isLoading && (
          <li>
            <EmptyState
              icon={search || categoria != null || onlyRecurring ? Search : ReceiptText}
              title={search || categoria != null || onlyRecurring ? 'Nenhum registo encontrado' : emptyTitle}
              description={search || categoria != null || onlyRecurring ? 'Ajusta os filtros para encontrares o registo certo.' : emptyDescription}
              action={!(search || categoria != null || onlyRecurring) ? <Button type="button" onClick={() => setCreateOpen(true)} leftIcon={<Plus size={15} />}>{createLabel}</Button> : undefined}
            />
          </li>
        )}
      </ul>

      {lastPage > 1 && (
        <div className="flex items-center justify-between gap-3 text-xs text-zinc-500">
          <button disabled={page <= 1} onClick={() => setPage(p => p - 1)} className="min-h-11 rounded-md px-3 py-2 focus:outline-none focus-visible:ring-2 focus-visible:ring-brand-400 disabled:opacity-40">Anterior</button>
          <span>{page} / {lastPage}</span>
          <button disabled={page >= lastPage} onClick={() => setPage(p => p + 1)} className="min-h-11 rounded-md px-3 py-2 focus:outline-none focus-visible:ring-2 focus-visible:ring-brand-400 disabled:opacity-40">Seguinte</button>
        </div>
      )}

      <DespesaFormModal
        open={createOpen}
        initialCategoria={initialCategoria}
        allowedCategorias={allowedCategorias}
        onClose={() => setCreateOpen(false)}
        onSaved={() => { invalidateAfterSave(); setCreateOpen(false); }}
      />

      <DespesaFormModal
        open={!!editing}
        editing={editing}
        initialCategoria={initialCategoria}
        allowedCategorias={allowedCategorias}
        onClose={() => { setEditing(null); clearEditParam(); }}
        onSaved={() => { invalidateAfterSave(); setEditing(null); clearEditParam(); }}
      />

      <Modal
        open={!!confirmDelete}
        title="Apagar registo"
        onClose={() => setConfirmDelete(null)}
        footer={<>
          <button type="button" onClick={() => setConfirmDelete(null)} className="rounded-md px-3 py-1.5 text-sm text-zinc-600 hover:bg-zinc-100 dark:text-zinc-300">Cancelar</button>
          <button type="button" disabled={remove.isPending}
            onClick={() => confirmDelete && remove.mutate(confirmDelete)}
            className="rounded-md bg-red-600 px-3 py-1.5 text-sm font-medium text-white disabled:opacity-60">
            {remove.isPending ? 'A apagar...' : 'Apagar'}
          </button>
        </>}
      >
        <p className="text-sm">Apagar <strong>{confirmDelete?.descricao}</strong> ({formatCents(confirmDelete?.valorCents ?? 0)})?</p>
      </Modal>
    </div>
  );
}
