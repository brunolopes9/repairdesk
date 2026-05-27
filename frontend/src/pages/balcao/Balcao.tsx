import { useSearchParams } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { ShoppingCart, Wallet } from 'lucide-react';
import { lazy, Suspense } from 'react';
import { cashApi, DAILY_CLOSING_STATUS } from '../../lib/cash/api';

const Vendas = lazy(() => import('../vendas/Vendas'));
const Cash = lazy(() => import('../cash/Cash'));

type TabKey = 'venda' | 'caixa';

const TABS: Array<{ key: TabKey; label: string; icon: typeof ShoppingCart }> = [
  { key: 'venda', label: 'Venda rápida', icon: ShoppingCart },
  { key: 'caixa', label: 'Caixa de hoje', icon: Wallet },
];

/**
 * Sprint 383 (Doc 86): "Balcão" — junta a POS (Venda rápida) e a Caixa (abertura, movimentos,
 * fecho/Z-Report) num só centro operacional. Reaproveita as páginas existentes em modo embedded.
 * A regra "não vendes com caixa fechada" vive dentro da própria POS (gate), por isso vale aqui e
 * em /vendas. A pill de estado da caixa fica sempre visível no topo.
 */
export default function Balcao() {
  const [params, setParams] = useSearchParams();
  const tab: TabKey = params.get('tab') === 'caixa' ? 'caixa' : 'venda';

  const caixaHoje = useQuery({
    queryKey: ['cash', 'today', null],
    queryFn: () => cashApi.today(null),
    staleTime: 15_000,
  });
  const caixaAberta =
    caixaHoje.data?.status === DAILY_CLOSING_STATUS.Open ||
    caixaHoje.data?.status === DAILY_CLOSING_STATUS.Reopened;

  function setTab(key: TabKey) {
    setParams(key === 'venda' ? {} : { tab: key }, { replace: true });
  }

  return (
    <div className="space-y-5">
      <div className="flex flex-col gap-3 sm:flex-row sm:items-end sm:justify-between">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">Balcão</h1>
          <p className="text-sm text-zinc-500">Vende, gere a caixa e fecha o dia — tudo no mesmo sítio.</p>
        </div>
        {caixaHoje.isSuccess && (
          <span
            className={`inline-flex items-center gap-1.5 self-start rounded-full px-3 py-1 text-xs font-medium ${
              caixaAberta
                ? 'bg-emerald-100 text-emerald-700 dark:bg-emerald-950/50 dark:text-emerald-300'
                : 'bg-zinc-100 text-zinc-600 dark:bg-zinc-800 dark:text-zinc-300'
            }`}
          >
            <span className={`h-1.5 w-1.5 rounded-full ${caixaAberta ? 'bg-emerald-500' : 'bg-zinc-400'}`} />
            {caixaAberta ? 'Caixa aberta' : 'Caixa fechada'}
          </span>
        )}
      </div>

      {/* Tabs */}
      <div className="flex gap-1 border-b border-zinc-200 dark:border-zinc-800">
        {TABS.map(({ key, label, icon: Icon }) => (
          <button
            key={key}
            type="button"
            onClick={() => setTab(key)}
            className={`-mb-px flex items-center gap-2 border-b-2 px-4 py-2.5 text-sm font-medium transition ${
              tab === key
                ? 'border-brand-600 text-brand-700 dark:border-brand-400 dark:text-brand-300'
                : 'border-transparent text-zinc-500 hover:text-zinc-800 dark:hover:text-zinc-200'
            }`}
          >
            <Icon size={16} /> {label}
          </button>
        ))}
      </div>

      <Suspense fallback={<div className="py-10 text-center text-sm text-zinc-500">A carregar…</div>}>
        {tab === 'venda' ? <Vendas embedded /> : <Cash embedded />}
      </Suspense>
    </div>
  );
}
