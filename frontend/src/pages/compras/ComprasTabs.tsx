import { useMemo } from 'react';
import { useQuery } from '@tanstack/react-query';
import { useSearchParams } from 'react-router-dom';
import { PageHeader, ViewTabs } from '../../components/ui';
import { supplierInvoicesApi } from '../../lib/supplierInvoices/api';
import {
  DESPESA_CATEGORIA,
  STOCK_DESPESA_CATEGORIAS,
} from '../../lib/despesas/types';
import AprovadasTab from '../despesas/AprovadasTab';
import PorAprovarTab from '../despesas/PorAprovarTab';

type TabKey = 'pending' | 'approved';

const tabs: Array<{ key: TabKey; label: string }> = [
  { key: 'pending', label: 'Por aprovar' },
  { key: 'approved', label: 'Aprovadas' },
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
        meta={<span className="text-sm text-zinc-500">Inbox fornecedor + compras aprovadas para stock</span>}
      />

      <ViewTabs
        value={active}
        onChange={(value) => setTab(value as TabKey)}
        tabs={tabs.map((tab) => ({
          key: tab.key,
          label: tab.label,
          meta: tab.key === 'pending' ? counts.pending : 'stock',
        }))}
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
