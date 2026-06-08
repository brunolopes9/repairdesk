import { useMemo, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { Inbox, Receipt, Banknote, FileDown, Plus, Upload, AlertTriangle, ArrowRight, Download } from 'lucide-react';
import { Button, DetailWorkspace, InspectorRail, KpiCard, PageHeader, SectionCard, ViewTabs } from '../../components/ui';
import { liveListOptions } from '../../lib/queryOptions';
import { formatCents, formatDateOnly } from '../../lib/money';
import { downloadFile } from '../../lib/downloadPdf';
import { supplierInvoicesApi } from '../../lib/supplierInvoices/api';
import { despesasApi } from '../../lib/despesas/api';
import { documentosApi } from '../../lib/documentos/api';

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
  const navigate = useNavigate();
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
  // Sprint 515: Vendas faturado no mês (lista única de faturas) para o cartão da secção Vendas.
  const vendasMes = useQuery({
    queryKey: ['documentos-vendas-mes', mesIso],
    queryFn: () => documentosApi.listVendas({ from: mesIso, to: new Date().toISOString() }),
    staleTime: 60_000,
  });
  const vendasFaturado = vendasMes.data?.totalCents ?? 0;

  const inboxItems = inbox.data ?? [];
  const inboxValor = inboxItems.reduce((s, i) => s + (i.totalCents ?? 0), 0);
  const despesasItems = despesasMes.data?.items ?? [];
  const totalDespesasMes = despesasItems.reduce((s, d) => s + (d.valorCents ?? 0), 0);

  const rows = tab === 'inbox' ? inboxItems : (history.data ?? []);
  const loadingRows = tab === 'inbox' ? inbox.isLoading : history.isLoading;
  const rail = (
    <InspectorRail>
      <div>
        <p className="text-[11px] font-semibold uppercase tracking-[0.18em] text-zinc-500">Operação</p>
        <h2 className="mt-1 text-base font-semibold text-zinc-950 dark:text-zinc-50">Ações rápidas</h2>
        <p className="mt-1 text-sm text-zinc-500">Atalhos para tratar documentos, lançar custos e entregar tudo ao contabilista.</p>
      </div>

      <div className="grid gap-2">
        <Link to="/compras" className="flex min-h-11 items-center gap-2 rounded-lg border border-zinc-200 px-3 text-sm font-medium transition hover:bg-zinc-50 dark:border-zinc-800 dark:hover:bg-zinc-800">
          <Upload size={15} /> Importar / enviar fatura
        </Link>
        <Link to="/despesas" className="flex min-h-11 items-center gap-2 rounded-lg border border-zinc-200 px-3 text-sm font-medium transition hover:bg-zinc-50 dark:border-zinc-800 dark:hover:bg-zinc-800">
          <Plus size={15} /> Nova despesa / custo
        </Link>
        <button
          type="button"
          onClick={() => downloadFile(supplierInvoicesApi.exportZipPath(from, to), `compras_${from}_${to}.zip`)}
          className="flex min-h-11 items-center gap-2 rounded-lg border border-zinc-200 px-3 text-left text-sm font-medium transition hover:bg-zinc-50 dark:border-zinc-800 dark:hover:bg-zinc-800"
        >
          <FileDown size={15} /> Export contabilista
        </button>
      </div>

      {inboxItems.length > 0 ? (
        <div className="rounded-lg border border-amber-200 bg-amber-50 p-3 text-sm text-amber-900 dark:border-amber-900/40 dark:bg-amber-950/30 dark:text-amber-200">
          <div className="flex items-start gap-2">
            <AlertTriangle size={16} className="mt-0.5 flex-none" />
            <span><strong>{inboxItems.length}</strong> fatura(s) por decidir no inbox — {formatCents(inboxValor)}.</span>
          </div>
          <Link to="/compras" className="mt-2 inline-flex text-xs font-semibold text-amber-800 underline-offset-2 hover:underline dark:text-amber-200">
            Resolver agora
          </Link>
        </div>
      ) : (
        <div className="rounded-lg border border-emerald-200 bg-emerald-50 p-3 text-sm text-emerald-900 dark:border-emerald-900/40 dark:bg-emerald-950/30 dark:text-emerald-200">
          Inbox de compras limpo neste momento.
        </div>
      )}

      <div className="rounded-lg border border-zinc-200 p-3 dark:border-zinc-800">
        <h3 className="text-sm font-semibold text-zinc-950 dark:text-zinc-50">Resumo do mês</h3>
        <dl className="mt-3 space-y-2 text-sm">
          <div className="flex justify-between gap-3"><dt className="text-zinc-500">Vendas faturadas</dt><dd className="font-medium tabular-nums">{formatCents(vendasFaturado)}</dd></div>
          <div className="flex justify-between gap-3"><dt className="text-zinc-500">Despesas/custos</dt><dd className="font-medium tabular-nums">{formatCents(totalDespesasMes)}</dd></div>
          <div className="flex justify-between gap-3"><dt className="text-zinc-500">Lançamentos</dt><dd className="font-medium tabular-nums">{despesasItems.length}</dd></div>
          <div className="flex justify-between gap-3"><dt className="text-zinc-500">Valor em inbox</dt><dd className="font-medium tabular-nums">{formatCents(inboxValor)}</dd></div>
        </dl>
      </div>
    </InspectorRail>
  );

  return (
    <div className="space-y-5">
      <PageHeader
        title="Compras e Operação"
        description="Centro financeiro diário: faturas de fornecedor, compras para stock, custos OpEx e vendas faturadas."
        meta={<span className="text-sm text-zinc-500">Operação financeira</span>}
        actions={(
          <>
            <Button type="button" variant="secondary" leftIcon={<Upload size={15} />} onClick={() => navigate('/compras')}>
              Importar fatura
            </Button>
            <Button type="button" leftIcon={<Plus size={15} />} onClick={() => navigate('/despesas')}>
              Nova despesa
            </Button>
          </>
        )}
      />

      {/* Sprint 515: as 3 secções do centro operacional — Vendas · Compras · Despesas, clicáveis. */}
      <div className="grid grid-cols-1 gap-3 sm:grid-cols-3">
        <Link to="/documentos" className="block transition hover:opacity-90">
          <KpiCard icon={Receipt} tone="emerald" label="Vendas · Faturas" value={formatCents(vendasFaturado)} sub="faturado este mês" />
        </Link>
        <Link to="/compras" className="block transition hover:opacity-90">
          <KpiCard icon={Inbox} tone={inboxItems.length > 0 ? 'amber' : 'zinc'} label="Compras · Fornecedores"
            value={String(inboxItems.length)} sub={inboxItems.length > 0 ? `${formatCents(inboxValor)} por decidir` : 'tudo tratado'} />
        </Link>
        <Link to="/despesas" className="block transition hover:opacity-90">
          <KpiCard icon={Banknote} tone="brand" label="Despesas & custos" value={formatCents(totalDespesasMes)} sub={`${despesasItems.length} lançamentos (mês)`} />
        </Link>
      </div>

      <DetailWorkspace rail={rail}>
        <div className="space-y-4">
          <div className="overflow-hidden rounded-lg border border-zinc-200 bg-white dark:border-zinc-800 dark:bg-zinc-900">
            <div className="flex flex-col gap-3 border-b border-zinc-200 px-4 py-3 dark:border-zinc-800 sm:flex-row sm:items-center sm:justify-between">
              <ViewTabs
                value={tab}
                onChange={(value) => setTab(value as Tab)}
                tabs={[
                  { key: 'inbox', label: 'Inbox', meta: inboxItems.length },
                  { key: 'history', label: 'Histórico', meta: history.data?.length },
                ]}
                className="border-0 bg-transparent p-0 dark:bg-transparent"
              />
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
              <Button
                type="button"
                onClick={() => downloadFile(supplierInvoicesApi.exportZipPath(from, to), `compras_${from}_${to}.zip`)}
                leftIcon={<Download size={15} />}
              >
                Descarregar
              </Button>
            </div>
          </SectionCard>
        </div>
      </DetailWorkspace>
    </div>
  );
}
