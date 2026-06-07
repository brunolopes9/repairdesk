import { useSearchParams } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import {
  ShoppingCart, Wallet, Lock, Banknote, Smartphone, CreditCard, Landmark, MoreHorizontal, PlusCircle,
} from 'lucide-react';
import { lazy, Suspense, type ReactNode } from 'react';
import { DetailWorkspace, InspectorRail } from '../../components/ui';
import { cashApi, DAILY_CLOSING_STATUS, type DailyClosingDto } from '../../lib/cash/api';
import { formatCents } from '../../lib/money';

const Vendas = lazy(() => import('../vendas/Vendas'));
const Cash = lazy(() => import('../cash/Cash'));
const FechoZReports = lazy(() => import('./FechoZReports'));

type TabKey = 'venda' | 'caixa' | 'fecho';

const TABS: Array<{ key: TabKey; label: string; icon: typeof ShoppingCart }> = [
  { key: 'venda', label: 'Venda rápida', icon: ShoppingCart },
  { key: 'caixa', label: 'Caixa de hoje', icon: Wallet },
  { key: 'fecho', label: 'Fecho & Z-Reports', icon: Lock },
];

/**
 * Sprint 383 + 403 (Fase 5c): "Balcão" — junta a POS (Venda rápida) e a Caixa (abertura, movimentos,
 * fecho/Z-Report) num só centro operacional. Reaproveita as páginas existentes em modo embedded.
 * A regra "não vendes com caixa fechada" vive dentro da própria POS (gate). Fiel ao POS e Vendas.png:
 * na aba Venda rápida a POS (produtos|carrinho) fica ao lado de um rail "Caixa do dia" → 3 colunas,
 * sem mexer na Vendas.tsx (fluxo crítico de cobrança).
 */
export default function Balcao() {
  const [params, setParams] = useSearchParams();
  const tabParam = params.get('tab');
  const tab: TabKey = tabParam === 'caixa' ? 'caixa' : tabParam === 'fecho' ? 'fecho' : 'venda';

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
        {tab === 'venda' ? (
          <DetailWorkspace
            rail={<CaixaRail
              dto={caixaHoje.data ?? null}
              aberta={caixaAberta}
              loading={caixaHoje.isLoading}
              onGoCaixa={() => setTab('caixa')}
              onGoFecho={() => setTab('fecho')}
            />}
          >
            <Vendas embedded />
          </DetailWorkspace>
        ) : tab === 'caixa' ? (
          <Cash embedded />
        ) : (
          <FechoZReports />
        )}
      </Suspense>
    </div>
  );
}

/** Rail "Caixa do dia" — 3.ª coluna da POS, fiel ao POS e Vendas.png. */
function CaixaRail({
  dto, aberta, loading, onGoCaixa, onGoFecho,
}: {
  dto: DailyClosingDto | null;
  aberta: boolean;
  loading: boolean;
  onGoCaixa: () => void;
  onGoFecho: () => void;
}) {
  return (
    <aside className="xl:sticky xl:top-4 xl:self-start">
      <InspectorRail>
        <div className="flex items-center justify-between">
          <h2 className="flex items-center gap-2 text-sm font-semibold"><Wallet size={16} /> Caixa do dia</h2>
          <span className={`rounded-full px-2 py-0.5 text-[11px] font-medium ${aberta ? 'bg-emerald-100 text-emerald-700 dark:bg-emerald-950/50 dark:text-emerald-300' : 'bg-zinc-100 text-zinc-600 dark:bg-zinc-800 dark:text-zinc-300'}`}>
            {aberta ? 'Aberta' : 'Fechada'}
          </span>
        </div>

        {loading ? (
          <p className="text-sm text-zinc-400">A carregar…</p>
        ) : !aberta ? (
          <div className="space-y-3">
            <p className="text-sm text-zinc-500">A caixa está fechada. Abre a caixa para registar as vendas de hoje.</p>
            <button
              type="button"
              onClick={onGoCaixa}
              className="flex w-full items-center justify-center gap-1.5 rounded-lg bg-brand-600 px-3 py-2 text-sm font-medium text-white transition hover:bg-brand-700"
            >
              <PlusCircle size={16} /> Abrir caixa para vender hoje
            </button>
          </div>
        ) : dto ? (
          <>
            <dl className="space-y-1.5 text-sm">
              <PayRow icon={<Banknote size={14} />} label="Dinheiro" value={dto.cashEntriesCents} />
              <PayRow icon={<Smartphone size={14} />} label="MB WAY" value={dto.mbwayCents} />
              <PayRow icon={<Landmark size={14} />} label="Multibanco" value={dto.multibancoCents} />
              <PayRow icon={<CreditCard size={14} />} label="Cartão" value={dto.cardCents} />
              {dto.otherCents > 0 && <PayRow icon={<MoreHorizontal size={14} />} label="Outros" value={dto.otherCents} />}
            </dl>

            <div className="space-y-1 border-t border-zinc-100 pt-2 dark:border-zinc-800">
              <div className="flex items-center justify-between text-xs text-zinc-500">
                <span>Fundo de abertura</span>
                <span className="tabular-nums">{formatCents(dto.openingCents)}</span>
              </div>
              {dto.cashExitsCents > 0 && (
                <div className="flex items-center justify-between text-xs text-rose-600 dark:text-rose-400">
                  <span>Saídas</span>
                  <span className="tabular-nums">− {formatCents(dto.cashExitsCents)}</span>
                </div>
              )}
              <div className="flex items-center justify-between pt-1 text-base font-semibold">
                <span>Total em caixa</span>
                <span className="tabular-nums">{formatCents(dto.expectedClosingCents)}</span>
              </div>
            </div>

            <div className="grid gap-2">
              <button
                type="button"
                onClick={onGoCaixa}
                className="flex items-center justify-center gap-1.5 rounded-lg border border-zinc-200 px-3 py-2 text-xs font-medium transition hover:bg-zinc-50 dark:border-zinc-800 dark:hover:bg-zinc-800"
              >
                <Wallet size={14} /> Movimentos da caixa
              </button>
              <button
                type="button"
                onClick={onGoFecho}
                className="flex items-center justify-center gap-1.5 rounded-lg border border-zinc-200 px-3 py-2 text-xs font-medium transition hover:bg-zinc-50 dark:border-zinc-800 dark:hover:bg-zinc-800"
              >
                <Lock size={14} /> Fechar & Z-Report
              </button>
            </div>
          </>
        ) : (
          <p className="text-sm text-zinc-400">Sem dados da caixa de hoje.</p>
        )}
      </InspectorRail>
    </aside>
  );
}

function PayRow({ icon, label, value }: { icon: ReactNode; label: string; value: number }) {
  return (
    <div className="flex items-center justify-between">
      <dt className="flex items-center gap-2 text-zinc-600 dark:text-zinc-300">
        <span className="grid h-6 w-6 place-items-center rounded-md bg-zinc-100 text-zinc-500 dark:bg-zinc-800">{icon}</span>
        {label}
      </dt>
      <dd className="tabular-nums font-medium">{formatCents(value)}</dd>
    </div>
  );
}
