import { useMemo } from 'react';
import { useQuery } from '@tanstack/react-query';
import { useSearchParams } from 'react-router-dom';
import { PageHeader } from '../../components/ui';
import { supplierInvoicesApi } from '../../lib/supplierInvoices/api';
import {
  DESPESA_CATEGORIA,
  STOCK_DESPESA_CATEGORIAS,
} from '../../lib/despesas/types';
import AprovadasTab from '../despesas/AprovadasTab';
import PorAprovarTab from '../despesas/PorAprovarTab';

type TabKey = 'pending' | 'approved';

const tabs: Array<{ key: TabKey; label: string }> = [
  { key: 'pending', label: '📥 Por aprovar' },
  { key: 'approved', label: '✅ Aprovadas' },
];

function normalizeTab(value: string | null): TabKey {
  if (value === 'pending' || value === 'approved') return value;
  return 'pending';
}

export default function ComprasTabs() {
  const [params, setParams] = useSearchParams();
  const active = normalizeTab(params.get('tab'));

  const pending = useQuery({
    queryKey: ['supplier-invoices-pending'],
    queryFn: () => supplierInvoicesApi.pending(100),
    refetchInterval: 30_000,
  });

  const counts = useMemo(() => ({
    pending: pending.data?.length ?? 0,
  }), [pending.data]);

  function setTab(tab: TabKey) {
    const next = new URLSearchParams(params);
    next.set('tab', tab);
    setParams(next, { replace: true });
  }

  return (
    <div className="space-y-4">
      <PageHeader
        title="Compras"
        description="Faturas de fornecedor, pecas, material e compras ligadas a stock."
        meta={(
          <div className="flex flex-wrap gap-1">
            {tabs.map((tab) => {
              const badge = tab.key === 'pending' ? counts.pending : null;
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
      {active === 'approved' && (
        <AprovadasTab
          title="Compras aprovadas"
          description="Despesas aprovadas como stock: pecas, material e pecas usadas."
          categoriaIn={STOCK_DESPESA_CATEGORIAS}
          includeSupplierInvoiceImports
          allowedCategorias={STOCK_DESPESA_CATEGORIAS}
          initialCategoria={DESPESA_CATEGORIA.Pecas}
          createLabel="Nova compra"
          emptyTitle="Ainda nao ha compras aprovadas"
          emptyDescription="As faturas aprovadas como stock aparecem aqui para consulta e edicao."
        />
      )}
    </div>
  );
}
