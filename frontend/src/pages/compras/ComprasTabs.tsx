import { useQuery } from '@tanstack/react-query';
import { useSearchParams } from 'react-router-dom';
import { PageHeader, ViewTabs } from '../../components/ui';
import { supplierInvoicesApi } from '../../lib/supplierInvoices/api';
import PorAprovarTab from '../despesas/PorAprovarTab';
import DocumentosTab from './DocumentosTab';
import InventarioLotesTab from './InventarioLotesTab';
import ResumoTab from './ResumoTab';
import SimuladorTab from './SimuladorTab';

/** Doc 94 Fase 3: Compras = faturas de fornecedor → stock por lote, com IVA sempre calculado. */
const TABS = [
  { key: 'docs', label: 'Documentos' },
  { key: 'stock', label: 'Stock' },
  { key: 'resumo', label: 'Resumo IVA' },
  { key: 'simulador', label: 'Simulador' },
  { key: 'pending', label: 'Faturas recebidas' },
] as const;

type TabKey = (typeof TABS)[number]['key'];

function normalizeTab(value: string | null): TabKey {
  return TABS.find((t) => t.key === value)?.key ?? 'docs';
}

export default function ComprasTabs() {
  const [params, setParams] = useSearchParams();
  const active = normalizeTab(params.get('tab'));

  // Inbox de faturas lidas por IA (upload/email) à espera de aprovação.
  const pending = useQuery({
    queryKey: ['supplier-invoices-pending'],
    queryFn: () => supplierInvoicesApi.pending(100),
    refetchInterval: 30_000,
  });

  function setTab(tab: TabKey) {
    const next = new URLSearchParams(params);
    next.set('tab', tab);
    setParams(next, { replace: true });
  }

  return (
    <div className="space-y-4">
      <PageHeader
        title="Compras"
        description="Faturas de fornecedor. Cada linha é um lote de stock, com o preço de venda e o IVA calculados."
      />

      <ViewTabs
        value={active}
        onChange={(value) => setTab(value as TabKey)}
        tabs={TABS.map((t) => ({
          key: t.key,
          label: t.label,
          meta: t.key === 'pending' && pending.data?.length ? pending.data.length : undefined,
        }))}
      />

      {active === 'docs' && <DocumentosTab />}
      {active === 'stock' && <InventarioLotesTab />}
      {active === 'resumo' && <ResumoTab />}
      {active === 'simulador' && <SimuladorTab />}
      {active === 'pending' && <PorAprovarTab />}
    </div>
  );
}
