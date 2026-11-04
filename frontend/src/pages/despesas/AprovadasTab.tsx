import { useMemo, useState, type ReactNode } from 'react';
import { Navigate, useNavigate, useSearchParams } from 'react-router-dom';
import { useMutation, useQuery, useQueryClient, keepPreviousData } from '@tanstack/react-query';
import { Banknote, CalendarClock, FileText, Layers3, Plus, ReceiptText, Repeat2, Search, Trash2, TrendingDown } from 'lucide-react';
import Modal from '../../components/Modal';
import { Button, DateRangeFilter, DetailWorkspace, EmptyState, InspectorRail, PageHeader, Paginacao, SkeletonCard } from '../../components/ui';
import { useFiltrosUrl } from '../../lib/useFiltrosUrl';
import { DespesaFormModal } from '../../components/DespesaFormModal';
import { FaturaDespesa } from './FaturaDespesa';
import { BotaoImprimir } from '../../components/BotaoImprimir';
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
  initialCategoria = DESPESA_CATEGORIA.Outro,
  createLabel = 'Nova',
  emptyTitle = 'Ainda nao ha registos',
  emptyDescription = 'Cria o primeiro registo para manter o custo operacional visivel.',
  showCategoriaFilter = true,
  showRecurringToggle = false,
}: AprovadasTabProps) {
  const qc = useQueryClient();
  const navigate = useNavigate();
  const [params] = useSearchParams();
  // Links antigos (/despesas?edit=…) abrem agora a página da despesa.
  const editId = params.get('edit');
  // Sprint 592: filtros e página no URL — ao voltar de uma despesa, a lista está como se deixou.
  const { f, set, page, setPage } = useFiltrosUrl({ tab: '', categoria: '', q: '', recorrentes: '', de: '', ate: '', page: '1' });
  const categoria = f.categoria === '' ? null : (Number(f.categoria) as DespesaCategoria);
  const search = f.q;
  const onlyRecurring = f.recorrentes === '1';
  const datas = { de: f.de, ate: f.ate };
  const [createOpen, setCreateOpen] = useState(false);
  const [confirmDelete, setConfirmDelete] = useState<Despesa | null>(null);
  const list = useQuery({
    queryKey: ['despesas', categoriaIn ?? null, includeSupplierInvoiceImports, excludeSupplierInvoiceImports, categoria, search, onlyRecurring, datas.de, datas.ate, page],
    queryFn: () => despesasApi.list({
      q: search,
      categoria,
      categoriaIn,
      includeSupplierInvoiceImports,
      excludeSupplierInvoiceImports,
      isRecorrente: showRecurringToggle && onlyRecurring ? true : undefined,
      from: datas.de || undefined,
      to: datas.ate || undefined,
      page,
      pageSize: 20,
    }),
    placeholderData: keepPreviousData,
  });

  // Sprint 574: os totais são de todas as despesas do filtro — não só das 20 desta página.
  const resumo = useQuery({
    queryKey: ['despesas', 'resumo', categoriaIn ?? null, includeSupplierInvoiceImports, excludeSupplierInvoiceImports, categoria, search, onlyRecurring, datas.de, datas.ate],
    queryFn: () => despesasApi.resumo({
      q: search,
      categoria,
      categoriaIn,
      includeSupplierInvoiceImports,
      excludeSupplierInvoiceImports,
      isRecorrente: showRecurringToggle && onlyRecurring ? true : undefined,
      from: datas.de || undefined,
      to: datas.ate || undefined,
    }),
    placeholderData: keepPreviousData,
  });

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
    qc.invalidateQueries({ queryKey: ['apuramento'] });
  }

  const items = list.data?.items ?? [];
  const total = list.data?.total ?? 0;
  const lastPage = Math.max(1, Math.ceil(total / 20));
  const categoryOptions = allowedCategorias?.length
    ? allowedCategorias
    : Array.from(new Set(Object.values(DESPESA_CATEGORIA)));
  const expenseStats = useMemo(() => {
    const r = resumo.data;
    return {
      totalCents: r?.totalCents ?? 0,
      ivaCents: r?.ivaCents ?? 0,
      semIva: r?.semIva ?? 0,
      recurringCount: r?.recorrentes ?? 0,
      topCategories: (r?.porCategoria ?? []).slice(0, 6).map((c) => [c.categoria, { count: c.registos, totalCents: c.totalCents }] as const),
    };
  }, [resumo.data]);
  const activeFilters = [
    categoria != null ? DESPESA_LABEL[categoria] : null,
    search.trim() ? 'pesquisa' : null,
    showRecurringToggle && onlyRecurring ? 'recorrentes' : null,
    datas.de || datas.ate ? 'datas' : null,
  ].filter(Boolean).length;
  const abrir = (d: Despesa) => navigate(`/despesas/${d.id}`);
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
          Totais de todas as despesas do filtro (todas as páginas).
        </p>
      </div>

      <div className="grid grid-cols-2 gap-2">
        <ExpenseMetric icon={<Banknote size={16} />} label="Total" value={formatCents(expenseStats.totalCents)} tone="emerald" />
        <ExpenseMetric icon={<FileText size={16} />} label="Registos" value={String(total)} />
        <ExpenseMetric icon={<Layers3 size={16} />} label="IVA nas faturas" value={formatCents(expenseStats.ivaCents)} tone="blue" />
        <ExpenseMetric icon={<Repeat2 size={16} />} label="Sem IVA indicado" value={String(expenseStats.semIva)}
          caption={expenseStats.semIva ? 'abre e indica o IVA' : undefined} tone="amber" />
      </div>

      <div className="rounded-lg border border-zinc-200 p-3 dark:border-zinc-800">
        <div className="flex items-center justify-between gap-2">
          <p className="text-sm font-semibold text-zinc-900 dark:text-zinc-100">Por categoria</p>
          <span className="text-xs text-zinc-500">{expenseStats.recurringCount} recorrente(s)</span>
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
              Sem despesas neste filtro.
            </p>
          )}
        </div>
      </div>

      {showRecurringToggle && (
        <button
          type="button"
          onClick={() => set({ recorrentes: onlyRecurring ? '' : '1' })}
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

  if (editId) return <Navigate to={`/despesas/${editId}`} replace />;

  return (
    <div className="space-y-4">
      <PageHeader
        title={title}
        description={description}
        meta={<span className="text-sm text-zinc-500">{total} {total === 1 ? 'registo' : 'registos'} · total {formatCents(expenseStats.totalCents)}</span>}
        actions={<>
          {/* Sprint 593: imprime as despesas da lista (ex.: o mês inteiro) num só PDF — a lista e as faturas. */}
          <BotaoImprimir caminho="/imprimir/despesas" total={total} nome={`Despesas${datas.de ? '_' + datas.de : ''}${datas.ate && datas.ate !== datas.de ? '_a_' + datas.ate : ''}.pdf`}
            params={{ q: search, categoria: categoria ?? undefined, isRecorrente: showRecurringToggle && onlyRecurring ? true : undefined, from: datas.de, to: datas.ate }} />
          <Button type="button" onClick={() => setCreateOpen(true)} leftIcon={<Plus size={15} />}>{createLabel}</Button>
        </>}
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
                onClick={() => set({ categoria: '', q: '', recorrentes: '', de: '', ate: '' })}
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
                onChange={(e) => set({ categoria: e.target.value })}
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
                placeholder="Pesquisar nº, fornecedor, documento ou descrição…"
                value={search}
                onChange={(e) => set({ q: e.target.value })}
                className="min-h-11 w-full rounded-lg border border-zinc-300 bg-white py-2 pl-9 pr-3 text-sm outline-none focus:border-brand-500 focus:ring-2 focus:ring-brand-200 focus-visible:ring-2 focus-visible:ring-brand-400 dark:border-zinc-700 dark:bg-zinc-950"
              />
            </div>
            <DateRangeFilter value={datas} onChange={(v) => set({ de: v.de, ate: v.ate })} />
          </div>
        </section>

        <Paginacao posicao="cima" page={page} pages={lastPage} total={total} label="despesas" onPage={setPage} />

        {/* Sprint 584: uma linha por fatura, como nas Compras — Nº, data, fornecedor, documento, IVA e total. */}
        {items.length > 0 && (
          <div className="hidden overflow-x-auto rounded-lg border border-zinc-200 bg-white shadow-sm shadow-black/[0.015] dark:border-zinc-800 dark:bg-zinc-900 md:block">
            <table className="w-full min-w-[56rem] text-sm">
              <thead className="bg-zinc-50 text-left text-xs uppercase tracking-wide text-zinc-500 dark:bg-zinc-800/60">
                <tr>
                  <th className="px-3 py-2.5 font-medium">Nº</th>
                  <th className="px-3 py-2.5 font-medium">Data</th>
                  <th className="px-3 py-2.5 font-medium">Fornecedor</th>
                  <th className="px-3 py-2.5 font-medium">Documento</th>
                  <th className="px-3 py-2.5 font-medium">Descrição</th>
                  <th className="px-3 py-2.5 font-medium">Categoria</th>
                  <th className="px-3 py-2.5 text-right font-medium">IVA</th>
                  <th className="px-3 py-2.5 text-right font-medium">Total</th>
                  <th className="w-10" />
                </tr>
              </thead>
              <tbody className="divide-y divide-zinc-100 dark:divide-zinc-800">
                {items.map((d) => (
                  <tr key={d.id} onClick={() => abrir(d)} className="cursor-pointer transition hover:bg-zinc-50 dark:hover:bg-zinc-800/50">
                    <td className="px-3 py-2.5 tabular-nums text-zinc-500">{d.numero || '—'}</td>
                    <td className="whitespace-nowrap px-3 py-2.5 tabular-nums">{formatDateOnly(d.data)}</td>
                    <td className="px-3 py-2.5">
                      <span className="font-medium text-zinc-900 dark:text-zinc-100">{d.fornecedor ?? '—'}</span>
                      {d.reverseCharge && <span className="ml-1 text-[10px] font-semibold text-sky-700 dark:text-sky-300">UE</span>}
                    </td>
                    <td className="px-3 py-1.5 text-xs text-zinc-600 dark:text-zinc-300">
                      {/* Sprint 586: a fatura abre (ou anexa-se) aqui, ao lado do nº do documento — como nas compras. */}
                      <span className="flex items-center gap-1">
                        <FaturaDespesa despesa={d} compact />
                        <span>{d.numeroEncomenda ?? '—'}</span>
                      </span>
                    </td>
                    <td className="max-w-[18rem] px-3 py-2.5">
                      <span className="block truncate" title={d.descricao}>{d.descricao}</span>
                      {d.isRecorrente && <span className="text-[10px] font-medium text-emerald-700 dark:text-emerald-300">recorrente</span>}
                    </td>
                    <td className="px-3 py-2.5 text-xs text-zinc-600 dark:text-zinc-300">{DESPESA_LABEL[d.categoria]}</td>
                    <td className="whitespace-nowrap px-3 py-2.5 text-right tabular-nums text-xs">
                      {d.reverseCharge ? <span className="text-sky-700 dark:text-sky-300">autoliq.</span>
                        : d.ivaCents == null ? <span className="font-medium text-amber-700 dark:text-amber-400">por indicar</span>
                        : formatCents(d.ivaCents)}
                    </td>
                    <td className="whitespace-nowrap px-3 py-2.5 text-right font-semibold tabular-nums">{formatCents(d.valorCents)}</td>
                    <td className="px-2 py-2.5 text-right">
                      <button type="button" onClick={(e) => { e.stopPropagation(); setConfirmDelete(d); }} aria-label="Apagar"
                        className="grid h-8 w-8 place-items-center rounded-md text-zinc-400 hover:bg-zinc-100 hover:text-red-600 dark:hover:bg-zinc-800">
                        <Trash2 size={14} />
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}

        <ul className="space-y-2 md:hidden">
          {list.isLoading && Array.from({ length: 4 }).map((_, index) => <SkeletonCard key={index} />)}
          {items.map((d) => (
            <li key={d.id} className="group flex flex-col overflow-hidden rounded-lg border border-zinc-200 bg-white shadow-sm shadow-black/[0.015] transition hover:border-zinc-300 dark:border-zinc-800 dark:bg-zinc-900 dark:hover:border-zinc-700 sm:flex-row sm:items-stretch sm:justify-between">
              <button
                type="button"
                onClick={() => abrir(d)}
                className="min-w-0 flex-1 px-4 py-3 text-left focus:outline-none focus-visible:ring-2 focus-visible:ring-brand-400"
              >
                <div className="flex flex-wrap items-center gap-2">
                  <span className="rounded-full bg-zinc-100 px-2 py-0.5 text-[10px] font-medium text-zinc-700 dark:bg-zinc-800 dark:text-zinc-300">
                    {DESPESA_LABEL[d.categoria]}
                  </span>
                  <span className="text-xs text-zinc-500">{d.numero ? `#${d.numero} · ` : ''}{formatDateOnly(d.data)}</span>
                  {d.isRecorrente && (
                    <span className="rounded bg-emerald-100 px-1.5 py-0.5 text-[9px] font-medium text-emerald-700 dark:bg-emerald-950/40 dark:text-emerald-300">
                      recorrente
                    </span>
                  )}
                  {d.ivaCents == null && !d.reverseCharge && (
                    <span className="rounded bg-amber-100 px-1.5 py-0.5 text-[9px] font-medium text-amber-800 dark:bg-amber-950/40 dark:text-amber-300">
                      IVA por indicar
                    </span>
                  )}
                  {d.reverseCharge && (
                    <span className="rounded bg-sky-100 px-1.5 py-0.5 text-[9px] font-medium text-sky-800 dark:bg-sky-950/40 dark:text-sky-300">
                      UE · autoliquidação
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
                <FaturaDespesa despesa={d} compact />
                <div className="text-right">
                  <div className="text-[10px] font-semibold uppercase tracking-[0.14em] text-zinc-400">Saida</div>
                  <span className="font-semibold text-red-600 dark:text-red-400">-{formatCents(d.valorCents)}</span>
                </div>
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

        <Paginacao page={page} pages={lastPage} total={total} label="despesas" onPage={setPage} />
      </DetailWorkspace>

      <DespesaFormModal
        open={createOpen}
        initialCategoria={initialCategoria}
        allowedCategorias={allowedCategorias}
        onClose={() => setCreateOpen(false)}
        onSaved={() => { invalidateAfterSave(); setCreateOpen(false); }}
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
