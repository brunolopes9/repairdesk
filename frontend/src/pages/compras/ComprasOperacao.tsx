import { useMemo } from 'react';
import { Link } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { Inbox, Receipt, Banknote, FileDown, Plus, Upload, AlertTriangle, ArrowRight } from 'lucide-react';
import { KpiCard, SectionCard } from '../../components/ui';
import { liveListOptions } from '../../lib/queryOptions';
import { formatCents } from '../../lib/money';
import { supplierInvoicesApi } from '../../lib/supplierInvoices/api';
import { despesasApi } from '../../lib/despesas/api';

const STATUS_BADGE: Record<string, string> = {
  Pending: 'bg-amber-100 text-amber-700 dark:bg-amber-900/40 dark:text-amber-300',
  Approved: 'bg-emerald-100 text-emerald-700 dark:bg-emerald-900/40 dark:text-emerald-300',
  Rejected: 'bg-red-100 text-red-700 dark:bg-red-900/40 dark:text-red-300',
  Failed: 'bg-red-100 text-red-700 dark:bg-red-900/40 dark:text-red-300',
};

/**
 * Sprint 382: "Compras e Operação" — centro operacional financeiro (refs IDEIAS). Reúne o que
 * estava espalhado: inbox de faturas (a decidir), despesas/custos, e atalhos. Dados reais:
 * faturas pendentes (supplier-invoices) + despesas do mês.
 */
export default function ComprasOperacao() {
  const mesIso = useMemo(() => {
    const d = new Date();
    return new Date(d.getFullYear(), d.getMonth(), 1).toISOString();
  }, []);

  const inbox = useQuery({
    queryKey: ['supplier-invoices-pending'],
    queryFn: () => supplierInvoicesApi.pending(100),
    ...liveListOptions,
  });

  const despesasMes = useQuery({
    queryKey: ['despesas-mes', mesIso],
    queryFn: () => despesasApi.list({ from: mesIso, pageSize: 500 }),
    staleTime: 60_000,
  });

  const inboxItems = inbox.data ?? [];
  const inboxValor = inboxItems.reduce((s, i) => s + (i.totalCents ?? 0), 0);
  const despesasItems = despesasMes.data?.items ?? [];
  const totalDespesasMes = despesasItems.reduce((s, d) => s + (d.valorCents ?? 0), 0);

  return (
    <div className="space-y-5">
      <div className="flex flex-col gap-3 sm:flex-row sm:items-end sm:justify-between">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">Compras e Operação</h1>
          <p className="text-sm text-zinc-500">Faturas que chegam, compras em stock e custos operacionais — num só sítio.</p>
        </div>
        <div className="flex gap-2">
          <Link to="/compras" className="flex h-9 items-center gap-1.5 rounded-lg border border-zinc-200 px-3 text-sm font-medium transition hover:bg-zinc-100 dark:border-zinc-800 dark:hover:bg-zinc-800">
            <Upload size={15} /> Importar fatura
          </Link>
          <Link to="/despesas" className="flex h-9 items-center gap-1.5 rounded-lg bg-brand-600 px-3 text-sm font-medium text-white shadow-sm transition hover:bg-brand-700">
            <Plus size={16} strokeWidth={2.5} /> Nova despesa
          </Link>
        </div>
      </div>

      {/* KPIs */}
      <div className="grid grid-cols-2 gap-3 lg:grid-cols-4">
        <KpiCard icon={Inbox} tone={inboxItems.length > 0 ? 'amber' : 'zinc'} label="Inbox de faturas"
          value={String(inboxItems.length)} sub="a decidir" />
        <KpiCard icon={Receipt} tone="brand" label="Valor em inbox" value={formatCents(inboxValor)} />
        <KpiCard icon={Banknote} tone="emerald" label="Despesas (mês)" value={formatCents(totalDespesasMes)} />
        <KpiCard icon={FileDown} tone="zinc" label="Lançamentos (mês)" value={String(despesasItems.length)} sub="despesas" />
      </div>

      <div className="grid grid-cols-1 gap-4 lg:grid-cols-3">
        {/* Inbox de faturas — coluna principal */}
        <div className="lg:col-span-2">
          <SectionCard
            title="Inbox de faturas"
            action={<Link to="/compras" className="text-xs font-medium text-brand-600 hover:underline dark:text-brand-400">Resolver tudo →</Link>}
            bodyClassName="p-0"
          >
            {inbox.isLoading ? (
              <div className="p-4 text-sm text-zinc-500">A carregar…</div>
            ) : inboxItems.length === 0 ? (
              <div className="flex flex-col items-center gap-2 p-10 text-center text-sm text-zinc-500">
                <Inbox className="text-zinc-300" size={28} />
                Sem faturas por decidir. Tudo tratado! 🎉
              </div>
            ) : (
              <ul className="divide-y divide-zinc-100 dark:divide-zinc-800">
                {inboxItems.slice(0, 10).map((f) => (
                  <li key={f.id}>
                    <Link to="/compras" className="flex items-center gap-3 px-4 py-2.5 transition hover:bg-zinc-50 dark:hover:bg-zinc-800/50">
                      <span className="min-w-0 flex-1">
                        <span className="block truncate text-sm font-medium">{f.fornecedorName ?? 'Fornecedor por identificar'}</span>
                        <span className="block truncate text-xs text-zinc-500">{f.totalCents != null ? formatCents(f.totalCents) : 'valor por confirmar'}</span>
                      </span>
                      <span className={`rounded-full px-2 py-0.5 text-[11px] font-medium ${STATUS_BADGE[f.status] ?? STATUS_BADGE.Pending}`}>{f.status}</span>
                      <ArrowRight size={15} className="text-zinc-300" />
                    </Link>
                  </li>
                ))}
              </ul>
            )}
          </SectionCard>
        </div>

        {/* Coluna direita: ações + alertas + atalhos */}
        <div className="space-y-4">
          <SectionCard title="Ações rápidas">
            <div className="flex flex-col gap-2">
              <Link to="/compras" className="flex items-center gap-2 rounded-lg border border-zinc-200 px-3 py-2 text-sm transition hover:bg-zinc-50 dark:border-zinc-800 dark:hover:bg-zinc-800"><Upload size={15} /> Importar / enviar fatura</Link>
              <Link to="/despesas" className="flex items-center gap-2 rounded-lg border border-zinc-200 px-3 py-2 text-sm transition hover:bg-zinc-50 dark:border-zinc-800 dark:hover:bg-zinc-800"><Plus size={15} /> Nova despesa / custo</Link>
              <Link to="/despesas" className="flex items-center gap-2 rounded-lg border border-zinc-200 px-3 py-2 text-sm transition hover:bg-zinc-50 dark:border-zinc-800 dark:hover:bg-zinc-800"><FileDown size={15} /> Export contabilista</Link>
            </div>
          </SectionCard>

          {inboxItems.length > 0 && (
            <SectionCard title="Alertas">
              <div className="flex items-start gap-2 text-sm">
                <AlertTriangle size={16} className="mt-0.5 flex-none text-amber-500" />
                <span><strong>{inboxItems.length}</strong> fatura(s) por decidir no inbox — {formatCents(inboxValor)}. <Link to="/compras" className="text-brand-600 hover:underline dark:text-brand-400">resolver</Link></span>
              </div>
            </SectionCard>
          )}

          <SectionCard title="Resumo do mês">
            <dl className="space-y-2 text-sm">
              <div className="flex justify-between"><dt className="text-zinc-500">Despesas/custos</dt><dd className="font-medium tabular-nums">{formatCents(totalDespesasMes)}</dd></div>
              <div className="flex justify-between"><dt className="text-zinc-500">Lançamentos</dt><dd className="font-medium tabular-nums">{despesasItems.length}</dd></div>
              <div className="flex justify-between"><dt className="text-zinc-500">Faturas no inbox</dt><dd className="font-medium tabular-nums">{inboxItems.length}</dd></div>
            </dl>
            <Link to="/despesas" className="mt-3 block text-xs font-medium text-brand-600 hover:underline dark:text-brand-400">Ver despesas & custos →</Link>
          </SectionCard>
        </div>
      </div>
    </div>
  );
}
