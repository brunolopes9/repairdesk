import { useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { Inbox, Receipt, Banknote, FileDown, Plus, Upload, AlertTriangle, ArrowRight, Download } from 'lucide-react';
import { KpiCard, SectionCard } from '../../components/ui';
import { liveListOptions } from '../../lib/queryOptions';
import { formatCents, formatDateOnly } from '../../lib/money';
import { downloadFile } from '../../lib/downloadPdf';
import { supplierInvoicesApi } from '../../lib/supplierInvoices/api';
import { despesasApi } from '../../lib/despesas/api';

const STATUS_BADGE: Record<string, string> = {
  Pending: 'bg-amber-100 text-amber-700 dark:bg-amber-900/40 dark:text-amber-300',
  Approved: 'bg-emerald-100 text-emerald-700 dark:bg-emerald-900/40 dark:text-emerald-300',
  Rejected: 'bg-red-100 text-red-700 dark:bg-red-900/40 dark:text-red-300',
  Failed: 'bg-red-100 text-red-700 dark:bg-red-900/40 dark:text-red-300',
};
const STATUS_LABEL: Record<string, string> = {
  Pending: 'Por decidir',
  Approved: 'Aprovada',
  Rejected: 'Rejeitada',
  Failed: 'Falhou',
};
const CONF_BADGE: Record<string, string> = {
  High: 'bg-emerald-50 text-emerald-600 dark:bg-emerald-950/40 dark:text-emerald-300',
  Medium: 'bg-amber-50 text-amber-600 dark:bg-amber-950/40 dark:text-amber-300',
  Low: 'bg-red-50 text-red-600 dark:bg-red-950/40 dark:text-red-300',
};

type Tab = 'inbox' | 'history';

/**
 * Sprint 382 + 401 (Fase 4): "Compras e Operação" — centro operacional financeiro. Layout fiel ao
 * mockup `Compras e Operação.png`: KPIs no topo, tabela de faturas (Inbox/Histórico) à esquerda +
 * export personalizado, rail Ações/Alertas/Resumo à direita. Aprovar mantém-se no fluxo /compras
 * (precisa de categorização completa); aqui a tabela faz triagem e liga ao detalhe.
 */
export default function ComprasOperacao() {
  const [tab, setTab] = useState<Tab>('inbox');
  const mesIso = useMemo(() => {
    const d = new Date();
    return new Date(d.getFullYear(), d.getMonth(), 1).toISOString();
  }, []);
  const today = new Date().toISOString().slice(0, 10);
  const monthStart = useMemo(() => {
    const d = new Date();
    return new Date(d.getFullYear(), d.getMonth(), 1).toISOString().slice(0, 10);
  }, []);
  const [from, setFrom] = useState(monthStart);
  const [to, setTo] = useState(today);

  const inbox = useQuery({
    queryKey: ['supplier-invoices-pending'],
    queryFn: () => supplierInvoicesApi.pending(100),
    ...liveListOptions,
  });
  const history = useQuery({
    queryKey: ['supplier-invoices-history'],
    queryFn: () => supplierInvoicesApi.history(100),
    enabled: tab === 'history',
    staleTime: 30_000,
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

  const rows = tab === 'inbox' ? inboxItems : (history.data ?? []);
  const loadingRows = tab === 'inbox' ? inbox.isLoading : history.isLoading;

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
        {/* Coluna principal: tabela de faturas + export */}
        <div className="space-y-4 lg:col-span-2">
          <div className="overflow-hidden rounded-xl border border-zinc-200 bg-white dark:border-zinc-800 dark:bg-zinc-900">
            <div className="flex items-center justify-between gap-2 border-b border-zinc-200 px-4 py-2.5 dark:border-zinc-800">
              <div className="flex gap-1">
                <button
                  type="button"
                  onClick={() => setTab('inbox')}
                  className={`rounded-lg px-3 py-1.5 text-sm font-medium transition ${tab === 'inbox' ? 'bg-brand-600 text-white' : 'text-zinc-600 hover:bg-zinc-100 dark:text-zinc-300 dark:hover:bg-zinc-800'}`}
                >
                  Inbox{inboxItems.length > 0 ? ` · ${inboxItems.length}` : ''}
                </button>
                <button
                  type="button"
                  onClick={() => setTab('history')}
                  className={`rounded-lg px-3 py-1.5 text-sm font-medium transition ${tab === 'history' ? 'bg-brand-600 text-white' : 'text-zinc-600 hover:bg-zinc-100 dark:text-zinc-300 dark:hover:bg-zinc-800'}`}
                >
                  Histórico
                </button>
              </div>
              <Link to="/compras" className="text-xs font-medium text-brand-600 hover:underline dark:text-brand-400">Abrir compras →</Link>
            </div>

            {loadingRows ? (
              <div className="p-4 text-sm text-zinc-500">A carregar…</div>
            ) : rows.length === 0 ? (
              <div className="flex flex-col items-center gap-2 p-10 text-center text-sm text-zinc-500">
                <Inbox className="text-zinc-300" size={28} />
                {tab === 'inbox' ? 'Sem faturas por decidir. Tudo tratado! 🎉' : 'Sem faturas no histórico.'}
              </div>
            ) : (
              <div className="overflow-x-auto">
                <table className="w-full text-sm">
                  <thead>
                    <tr className="border-b border-zinc-200 text-left text-[11px] font-semibold uppercase tracking-wide text-zinc-500 dark:border-zinc-800">
                      <th className="px-4 py-2.5">Fornecedor</th>
                      <th className="px-4 py-2.5">Documento</th>
                      <th className="px-4 py-2.5">Data</th>
                      <th className="px-4 py-2.5 text-right">Valor</th>
                      <th className="px-4 py-2.5">Estado</th>
                      <th className="px-4 py-2.5 text-right">Ação</th>
                    </tr>
                  </thead>
                  <tbody>
                    {rows.slice(0, 12).map((f) => (
                      <tr key={f.id} className="border-b border-zinc-100 transition last:border-0 hover:bg-zinc-50 dark:border-zinc-800/60 dark:hover:bg-zinc-800/50">
                        <td className="px-4 py-3">
                          <div className="flex items-center gap-2">
                            <span className="truncate font-medium">{f.fornecedorName ?? 'Por identificar'}</span>
                            {f.parseConfidence && CONF_BADGE[f.parseConfidence] && (
                              <span className={`rounded px-1.5 py-0.5 text-[10px] font-medium ${CONF_BADGE[f.parseConfidence]}`} title="Confiança do parser">{f.parseConfidence}</span>
                            )}
                          </div>
                        </td>
                        <td className="px-4 py-3 text-zinc-500">{f.documentNumber ?? '—'}</td>
                        <td className="px-4 py-3 text-zinc-500">{formatDateOnly(f.documentDate ?? f.createdAt)}</td>
                        <td className="px-4 py-3 text-right font-medium tabular-nums">{f.totalCents != null ? formatCents(f.totalCents) : '—'}</td>
                        <td className="px-4 py-3">
                          <span className={`rounded-full px-2 py-0.5 text-[11px] font-medium ${STATUS_BADGE[f.status] ?? STATUS_BADGE.Pending}`}>{STATUS_LABEL[f.status] ?? f.status}</span>
                        </td>
                        <td className="px-4 py-3 text-right">
                          <Link
                            to="/compras"
                            className="inline-flex items-center gap-1 rounded-md border border-zinc-200 px-2.5 py-1 text-xs font-medium text-zinc-700 transition hover:bg-zinc-100 dark:border-zinc-700 dark:text-zinc-200 dark:hover:bg-zinc-800"
                          >
                            {f.status === 'Pending' ? 'Rever' : 'Ver'} <ArrowRight size={13} />
                          </Link>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </div>

          {/* Export personalizado */}
          <SectionCard title="Export personalizado">
            <p className="mb-3 text-xs text-zinc-500">Descarrega um ZIP com as faturas de fornecedor do período escolhido — para o contabilista.</p>
            <div className="flex flex-wrap items-end gap-3">
              <label className="text-xs text-zinc-500">
                De
                <input type="date" value={from} max={to} onChange={(e) => setFrom(e.target.value)}
                  className="mt-1 block rounded-lg border border-zinc-300 bg-white px-2 py-1.5 text-sm dark:border-zinc-700 dark:bg-zinc-950" />
              </label>
              <label className="text-xs text-zinc-500">
                Até
                <input type="date" value={to} min={from} max={today} onChange={(e) => setTo(e.target.value)}
                  className="mt-1 block rounded-lg border border-zinc-300 bg-white px-2 py-1.5 text-sm dark:border-zinc-700 dark:bg-zinc-950" />
              </label>
              <button
                type="button"
                onClick={() => downloadFile(supplierInvoicesApi.exportZipPath(from, to), `compras_${from}_${to}.zip`)}
                className="flex h-9 items-center gap-1.5 rounded-lg bg-brand-600 px-3 text-sm font-medium text-white transition hover:bg-brand-700"
              >
                <Download size={15} /> Descarregar
              </button>
            </div>
          </SectionCard>
        </div>

        {/* Coluna direita: ações + alertas + resumo */}
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
              <div className="flex justify-between"><dt className="text-zinc-500">Valor em inbox</dt><dd className="font-medium tabular-nums">{formatCents(inboxValor)}</dd></div>
            </dl>
            <Link to="/despesas" className="mt-3 block text-xs font-medium text-brand-600 hover:underline dark:text-brand-400">Ver despesas & custos →</Link>
          </SectionCard>
        </div>
      </div>
    </div>
  );
}
