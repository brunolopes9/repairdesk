import { useEffect, useMemo, useState, type ReactNode } from 'react';
import { useSearchParams } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient, keepPreviousData } from '@tanstack/react-query';
import { Banknote, CalendarClock, FileText, Layers3, PackagePlus, Plus, ReceiptText, Repeat2, Search, TrendingDown } from 'lucide-react';
import Modal from '../../components/Modal';
import { Button, DetailWorkspace, EmptyState, InspectorRail, PageHeader, SkeletonCard } from '../../components/ui';
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
  // Sprint 540: converter compra Peças/Material (limbo) em peça de stock real.
  const [convertTarget, setConvertTarget] = useState<Despesa | null>(null);
  const [convertQtd, setConvertQtd] = useState(1);
  const [convertNome, setConvertNome] = useState('');
  const [convertSku, setConvertSku] = useState('');

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

  const convert = useMutation({
    mutationFn: (vars: { id: string; quantidade: number; nome: string; sku: string }) =>
      despesasApi.converterStock(vars.id, {
        quantidade: vars.quantidade,
        nome: vars.nome.trim() || undefined,
        sku: vars.sku.trim() || undefined,
      }),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: ['despesas'] });
      qc.invalidateQueries({ queryKey: ['dashboard'] });
      qc.invalidateQueries({ queryKey: ['parts'] });
      qc.invalidateQueries({ queryKey: ['stock'] });
      setConvertTarget(null);
    },
  });

  function openConvert(d: Despesa) {
    setConvertTarget(d);
    setConvertQtd(1);
    setConvertNome(d.descricao);
    setConvertSku('');
  }

  function invalidateAfterSave() {
    qc.invalidateQueries({ queryKey: ['despesas'] });
    qc.invalidateQueries({ queryKey: ['dashboard'] });
  }

  const items = list.data?.items ?? [];
  const total = list.data?.total ?? 0;
  const lastPage = Math.max(1, Math.ceil(total / 20));
  const categoryOptions = allowedCategorias?.length
    ? allowedCategorias
    : Array.from(new Set(Object.values(DESPESA_CATEGORIA)));
  const expenseStats = useMemo(() => {
    const byCategory = new Map<DespesaCategoria, { count: number; totalCents: number }>();
    let pageTotalCents = 0;
    let recurringCount = 0;
    let cogsCount = 0;
    let linkedCount = 0;
    let supplierCount = 0;

    for (const item of items) {
      pageTotalCents += item.valorCents;
      if (item.isRecorrente) recurringCount += 1;
      if (item.isCogs) cogsCount += 1;
      if (item.trabalhoId || item.reparacaoId) linkedCount += 1;
      if (item.fornecedor) supplierCount += 1;

      const current = byCategory.get(item.categoria) ?? { count: 0, totalCents: 0 };
      byCategory.set(item.categoria, {
        count: current.count + 1,
        totalCents: current.totalCents + item.valorCents,
      });
    }

    return {
      pageTotalCents,
      recurringCount,
      cogsCount,
      linkedCount,
      supplierCount,
      topCategories: Array.from(byCategory.entries())
        .sort((a, b) => b[1].totalCents - a[1].totalCents)
        .slice(0, 4),
    };
  }, [items]);
  const activeFilters = [
    categoria != null ? DESPESA_LABEL[categoria] : null,
    search.trim() ? 'pesquisa' : null,
    showRecurringToggle && onlyRecurring ? 'recorrentes' : null,
  ].filter(Boolean).length;
  const workspaceMode = includeSupplierInvoiceImports ? 'compras' : excludeSupplierInvoiceImports ? 'opex' : 'financeiro';
  const maxCategoryCents = Math.max(...expenseStats.topCategories.map(([, data]) => data.totalCents), 1);
  const financeRail = (
    <InspectorRail>
      <div>
        <p className="text-[11px] font-semibold uppercase tracking-[0.18em] text-zinc-500">
          {workspaceMode === 'compras' ? 'Stock e fornecedor' : workspaceMode === 'opex' ? 'Operacao diaria' : 'Financeiro'}
        </p>
        <h3 className="mt-1 text-base font-semibold text-zinc-950 dark:text-zinc-50">
          {workspaceMode === 'compras' ? 'Compras aprovadas' : workspaceMode === 'opex' ? 'Despesas OpEx' : 'Custos aprovados'}
        </h3>
        <p className="mt-1 text-sm text-zinc-500">
          Leitura rapida desta pagina para perceber valor, origem e impacto antes de abrir cada registo.
        </p>
      </div>

      <div className="grid grid-cols-2 gap-2">
        <ExpenseMetric icon={<Banknote size={16} />} label="Pagina" value={formatCents(expenseStats.pageTotalCents)} tone="emerald" />
        <ExpenseMetric icon={<FileText size={16} />} label="Registos" value={String(items.length)} caption={`${total} no filtro`} />
        <ExpenseMetric icon={<Repeat2 size={16} />} label="Recorrentes" value={String(expenseStats.recurringCount)} tone="blue" />
        <ExpenseMetric
          icon={<Layers3 size={16} />}
          label={workspaceMode === 'compras' ? 'COGS' : 'Ligados'}
          value={String(workspaceMode === 'compras' ? expenseStats.cogsCount : expenseStats.linkedCount)}
          tone="amber"
        />
      </div>

      <div className="rounded-lg border border-zinc-200 p-3 dark:border-zinc-800">
        <div className="flex items-center justify-between gap-2">
          <p className="text-sm font-semibold text-zinc-900 dark:text-zinc-100">Categorias nesta vista</p>
          <span className="text-xs text-zinc-500">{expenseStats.supplierCount} c/ fornecedor</span>
        </div>
        <div className="mt-3 space-y-3">
          {expenseStats.topCategories.length > 0 ? expenseStats.topCategories.map(([category, data]) => (
            <div key={category} className="space-y-1.5">
              <div className="flex items-center justify-between gap-2 text-xs">
                <span className="font-medium text-zinc-700 dark:text-zinc-300">{DESPESA_LABEL[category]}</span>
                <span className="text-zinc-500">{formatCents(data.totalCents)}</span>
              </div>
              <div className="h-1.5 rounded-full bg-zinc-100 dark:bg-zinc-800">
                <div
                  className="h-full rounded-full bg-brand-500"
                  style={{ width: `${Math.max(8, Math.round((data.totalCents / maxCategoryCents) * 100))}%` }}
                />
              </div>
            </div>
          )) : (
            <p className="rounded-md bg-zinc-50 px-3 py-4 text-center text-sm text-zinc-500 dark:bg-zinc-950/60">
              Sem categorias nesta pagina.
            </p>
          )}
        </div>
      </div>

      {showRecurringToggle && (
        <button
          type="button"
          onClick={() => { setOnlyRecurring((value) => !value); setPage(1); }}
          className={`flex w-full items-center justify-between rounded-lg border px-3 py-2 text-sm transition ${
            onlyRecurring
              ? 'border-brand-500 bg-brand-50 text-brand-700 dark:border-brand-400 dark:bg-brand-950/30 dark:text-brand-200'
              : 'border-zinc-200 bg-white text-zinc-700 hover:bg-zinc-50 dark:border-zinc-800 dark:bg-zinc-900 dark:text-zinc-300 dark:hover:bg-zinc-800/70'
          }`}
        >
          <span className="inline-flex items-center gap-2"><CalendarClock size={15} /> Mostrar recorrentes</span>
          <span className="text-xs">{onlyRecurring ? 'ON' : 'OFF'}</span>
        </button>
      )}

      <div className="rounded-lg bg-zinc-950 p-3 text-sm text-white dark:bg-zinc-50 dark:text-zinc-950">
        <div className="flex items-start gap-2">
          <TrendingDown size={16} className="mt-0.5 shrink-0 text-emerald-300 dark:text-emerald-600" />
          <p>
            {workspaceMode === 'compras'
              ? 'Mantem compras de stock separadas do OpEx para margem real por reparacao.'
              : 'Mantem renda, software e custos fixos fora do stock para uma operacao mais legivel.'}
          </p>
        </div>
      </div>
    </InspectorRail>
  );

  return (
    <div className="space-y-4">
      <PageHeader
        title={title}
        description={description}
        meta={<span className="text-sm text-zinc-500">{total} {total === 1 ? 'registo' : 'registos'} - pagina: {formatCents(expenseStats.pageTotalCents)}</span>}
        actions={<Button type="button" onClick={() => setCreateOpen(true)} leftIcon={<Plus size={15} />}>{createLabel}</Button>}
      />

      <DetailWorkspace rail={financeRail}>
        <section className="rounded-lg border border-zinc-200 bg-white p-3 shadow-sm shadow-black/[0.02] dark:border-zinc-800 dark:bg-zinc-900">
          <div className="mb-3 flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
            <div>
              <p className="text-sm font-semibold text-zinc-900 dark:text-zinc-100">Filtros financeiros</p>
              <p className="text-xs text-zinc-500">
                {activeFilters > 0 ? `${activeFilters} filtro${activeFilters === 1 ? '' : 's'} ativo${activeFilters === 1 ? '' : 's'}` : 'Todos os registos desta area'}
              </p>
            </div>
            {activeFilters > 0 && (
              <button
                type="button"
                onClick={() => { setCategoria(null); setSearch(''); setOnlyRecurring(false); setPage(1); }}
                className="self-start rounded-md px-2 py-1 text-xs font-medium text-zinc-500 hover:bg-zinc-100 hover:text-zinc-800 focus:outline-none focus-visible:ring-2 focus-visible:ring-brand-400 dark:hover:bg-zinc-800 dark:hover:text-zinc-100 sm:self-auto"
              >
                Limpar filtros
              </button>
            )}
          </div>
          <div className="flex flex-col gap-2 lg:flex-row lg:items-center">
            {showCategoriaFilter && (
              <select
                value={categoria ?? ''}
                onChange={(e) => { setCategoria(e.target.value === '' ? null : (Number(e.target.value) as DespesaCategoria)); setPage(1); }}
                className="min-h-11 rounded-lg border border-zinc-300 bg-white px-3 py-2 text-sm outline-none focus:border-brand-500 focus:ring-2 focus:ring-brand-200 focus-visible:ring-2 focus-visible:ring-brand-400 dark:border-zinc-700 dark:bg-zinc-950 lg:w-56"
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
          </div>
        </section>

        <ul className="space-y-2">
          {list.isLoading && Array.from({ length: 4 }).map((_, index) => <SkeletonCard key={index} />)}
          {items.map((d) => (
            <li key={d.id} className="group flex flex-col overflow-hidden rounded-lg border border-zinc-200 bg-white shadow-sm shadow-black/[0.015] transition hover:border-zinc-300 dark:border-zinc-800 dark:bg-zinc-900 dark:hover:border-zinc-700 sm:flex-row sm:items-stretch sm:justify-between">
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
                <div className="mt-1 truncate font-medium text-zinc-950 dark:text-zinc-50">{d.descricao}</div>
                {d.fornecedor && (
                  <div className="text-xs text-zinc-500">
                    {d.fornecedor}{d.numeroEncomenda && ` - Enc. ${d.numeroEncomenda}`}
                  </div>
                )}
              </button>
              <div className="flex items-center justify-between gap-3 border-t border-zinc-100 px-4 py-3 dark:border-zinc-800 sm:min-w-44 sm:justify-end sm:border-l sm:border-t-0">
                <div className="text-right">
                  <div className="text-[10px] font-semibold uppercase tracking-[0.14em] text-zinc-400">Saida</div>
                  <span className="font-semibold text-red-600 dark:text-red-400">-{formatCents(d.valorCents)}</span>
                </div>
                {(d.categoria === DESPESA_CATEGORIA.Pecas || d.categoria === DESPESA_CATEGORIA.Material) && (
                  <button
                    type="button"
                    onClick={() => openConvert(d)}
                    className="inline-flex items-center gap-1 rounded-md border border-emerald-200 bg-emerald-50 px-2 py-1.5 text-xs font-medium text-emerald-700 hover:bg-emerald-100 focus:outline-none focus-visible:ring-2 focus-visible:ring-brand-400 dark:border-emerald-900/50 dark:bg-emerald-950/30 dark:text-emerald-300"
                    title="Mover esta compra para o Stock (cria peça + entrada, sem mexer no IVA)"
                  >
                    <PackagePlus size={14} /> Stock
                  </button>
                )}
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
          <div className="flex items-center justify-between gap-3 rounded-lg border border-zinc-200 bg-white px-3 py-2 text-xs text-zinc-500 dark:border-zinc-800 dark:bg-zinc-900">
            <button disabled={page <= 1} onClick={() => setPage(p => p - 1)} className="min-h-11 rounded-md px-3 py-2 focus:outline-none focus-visible:ring-2 focus-visible:ring-brand-400 disabled:opacity-40">Anterior</button>
            <span>{page} / {lastPage}</span>
            <button disabled={page >= lastPage} onClick={() => setPage(p => p + 1)} className="min-h-11 rounded-md px-3 py-2 focus:outline-none focus-visible:ring-2 focus-visible:ring-brand-400 disabled:opacity-40">Seguinte</button>
          </div>
        )}
      </DetailWorkspace>

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

      <Modal
        open={!!convertTarget}
        title="Converter em stock"
        onClose={() => setConvertTarget(null)}
        footer={<>
          <button type="button" onClick={() => setConvertTarget(null)} className="rounded-md px-3 py-1.5 text-sm text-zinc-600 hover:bg-zinc-100 dark:text-zinc-300">Cancelar</button>
          <button type="button" disabled={convert.isPending || convertQtd < 1 || !convertNome.trim()}
            onClick={() => convertTarget && convert.mutate({ id: convertTarget.id, quantidade: convertQtd, nome: convertNome, sku: convertSku })}
            className="rounded-md bg-emerald-600 px-3 py-1.5 text-sm font-medium text-white disabled:opacity-60">
            {convert.isPending ? 'A converter...' : 'Criar peça no stock'}
          </button>
        </>}
      >
        <div className="space-y-3">
          <p className="text-sm text-zinc-600 dark:text-zinc-300">
            Cria uma peça no Stock a partir desta compra (<strong>{formatCents(convertTarget?.valorCents ?? 0)}</strong>) e remove-a das despesas.
            O efeito no IVA é o mesmo — só passa a estar visível no inventário e disponível para reparações.
          </p>
          <label className="block text-sm">
            <span className="mb-1 block font-medium text-zinc-700 dark:text-zinc-300">Nome da peça</span>
            <input value={convertNome} onChange={(e) => setConvertNome(e.target.value)} className="w-full rounded-lg border border-zinc-300 bg-white px-3 py-2 text-sm outline-none focus:border-brand-500 dark:border-zinc-700 dark:bg-zinc-950" />
          </label>
          <div className="grid grid-cols-2 gap-3">
            <label className="block text-sm">
              <span className="mb-1 block font-medium text-zinc-700 dark:text-zinc-300">Quantidade</span>
              <input type="number" min={1} value={convertQtd} onChange={(e) => setConvertQtd(Math.max(1, Number(e.target.value) || 1))} className="w-full rounded-lg border border-zinc-300 bg-white px-3 py-2 text-sm outline-none focus:border-brand-500 dark:border-zinc-700 dark:bg-zinc-950" />
            </label>
            <label className="block text-sm">
              <span className="mb-1 block font-medium text-zinc-700 dark:text-zinc-300">SKU (opcional)</span>
              <input value={convertSku} onChange={(e) => setConvertSku(e.target.value)} className="w-full rounded-lg border border-zinc-300 bg-white px-3 py-2 text-sm outline-none focus:border-brand-500 dark:border-zinc-700 dark:bg-zinc-950" />
            </label>
          </div>
          <p className="text-xs text-zinc-500">
            Custo unitário: <strong>{formatCents(Math.round((convertTarget?.valorCents ?? 0) / Math.max(1, convertQtd)))}</strong>
            {' '}· categoria inicial "Outro" (ajustável depois no Stock).
          </p>
        </div>
      </Modal>
    </div>
  );
}

function ExpenseMetric({
  icon,
  label,
  value,
  caption,
  tone = 'zinc',
}: {
  icon: ReactNode;
  label: string;
  value: string;
  caption?: string;
  tone?: 'zinc' | 'emerald' | 'blue' | 'amber';
}) {
  const toneClass = {
    zinc: 'bg-zinc-50 text-zinc-700 dark:bg-zinc-950/60 dark:text-zinc-300',
    emerald: 'bg-emerald-50 text-emerald-700 dark:bg-emerald-950/30 dark:text-emerald-300',
    blue: 'bg-blue-50 text-blue-700 dark:bg-blue-950/30 dark:text-blue-300',
    amber: 'bg-amber-50 text-amber-700 dark:bg-amber-950/30 dark:text-amber-300',
  }[tone];

  return (
    <div className="rounded-lg border border-zinc-200 p-3 dark:border-zinc-800">
      <div className={`mb-3 inline-flex h-8 w-8 items-center justify-center rounded-md ${toneClass}`}>
        {icon}
      </div>
      <p className="text-[11px] font-semibold uppercase tracking-[0.14em] text-zinc-500">{label}</p>
      <p className="mt-1 text-lg font-semibold text-zinc-950 dark:text-zinc-50">{value}</p>
      {caption && <p className="mt-1 text-xs text-zinc-500">{caption}</p>}
    </div>
  );
}
