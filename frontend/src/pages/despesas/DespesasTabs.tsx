import { useMemo } from 'react';
import { useQuery } from '@tanstack/react-query';
import { useSearchParams } from 'react-router-dom';
import { PageHeader } from '../../components/ui';
import { despesasApi } from '../../lib/despesas/api';
import { supplierInvoicesApi } from '../../lib/supplierInvoices/api';
import AprovadasTab from './AprovadasTab';
import PorAprovarTab from './PorAprovarTab';
import RecorrentesTab from './RecorrentesTab';

type TabKey = 'pending' | 'approved' | 'recurring';

const tabs: Array<{ key: TabKey; label: string }> = [
  { key: 'pending', label: '📥 Por aprovar' },
  { key: 'approved', label: '✅ Aprovadas' },
  { key: 'recurring', label: '🔁 Recorrentes' },
];

function normalizeTab(value: string | null): TabKey {
  if (value === 'pending' || value === 'approved' || value === 'recurring') return value;
  return 'approved';
}

export default function DespesasTabs() {
  const [params, setParams] = useSearchParams();
  const active = normalizeTab(params.get('tab'));

  const pending = useQuery({
    queryKey: ['supplier-invoices-pending'],
    queryFn: () => supplierInvoicesApi.pending(100),
    refetchInterval: 30_000,
  });
  const recurring = useQuery({
    queryKey: ['despesas', 'recorrentes-count'],
    queryFn: () => despesasApi.list({ isRecorrente: true, pageSize: 1 }),
  });

  const counts = useMemo(() => ({
    pending: pending.data?.length ?? 0,
    recurring: recurring.data?.total ?? 0,
  }), [pending.data, recurring.data]);

  function setTab(tab: TabKey) {
    const next = new URLSearchParams(params);
    next.set('tab', tab);
    setParams(next, { replace: true });
  }

  return (
    <div className="space-y-4">
      <PageHeader
        title="Despesas"
        description="Facturas por aprovar, despesas aprovadas e custos recorrentes no mesmo fluxo."
        meta={(
          <div className="flex flex-wrap gap-1">
            {tabs.map((tab) => {
              const badge = tab.key === 'pending' ? counts.pending : tab.key === 'recurring' ? counts.recurring : null;
              return (
                <button
                  key={tab.key}
                  type="button"
                  onClick={() => setTab(tab.key)}
                  className={`min-h-10 rounded-lg px-3 py-2 text-sm font-medium transition ${active === tab.key ? 'bg-zinc-900 text-white dark:bg-zinc-100 dark:text-zinc-900' : 'bg-white text-zinc-600 ring-1 ring-zinc-200 hover:bg-zinc-50 dark:bg-zinc-900 dark:text-zinc-300 dark:ring-zinc-800 dark:hover:bg-zinc-800'}`}
                >
                  {tab.label}
                  {badge !== null && <span className="ml-2 rounded-full bg-zinc-100 px-1.5 py-0.5 text-[11px] text-zinc-700 dark:bg-zinc-800 dark:text-zinc-200">{badge}</span>}
                </button>
              );
            })}
          </div>
        )}
      />

      {active === 'pending' && <PorAprovarTab />}
      {active === 'approved' && <AprovadasTab />}
      {active === 'recurring' && <RecorrentesTab />}
    </div>
  );
}
