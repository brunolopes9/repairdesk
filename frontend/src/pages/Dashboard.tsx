import type { ReactNode } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { AlertTriangle, Banknote, Boxes, Code2, Package, Plus, Receipt, ShoppingCart, Wallet, Wrench } from 'lucide-react';
import { Button, EmptyState, KpiCard, PageHeader, SectionCard, SkeletonCard } from '../components/ui';
import { dashboardApi } from '../lib/dashboard/api';
import { formatEur } from '../lib/compras/format';
import { formatDateOnly } from '../lib/money';
import { apiErrorMessage } from '../lib/errors';
import { VENDA_ESTADO_COLOR, VENDA_ESTADO_LABEL, VENDA_TIPO } from '../lib/vendas/types';

const TIPO_ICON = { 0: Package, 1: Wrench, 2: Code2 } as const;

/**
 * Doc 94: Dashboard do modelo novo — o mês em números (vendas, IVA a entregar, lucro, despesas),
 * o que está em curso, alertas que pedem ação e as próximas entregas.
 */
export default function Dashboard() {
  const navigate = useNavigate();
  const painel = useQuery({ queryKey: ['dashboard'], queryFn: dashboardApi.get, refetchInterval: 60_000 });

  if (painel.isLoading) return <SkeletonCard />;
  if (painel.isError || !painel.data) return <p className="text-sm text-rose-600">{apiErrorMessage(painel.error)}</p>;
  const { mes, emCurso, alertas, stock, proximasEntregas } = painel.data;
  const mesLabel = new Intl.DateTimeFormat('pt-PT', { month: 'long', year: 'numeric' }).format(new Date(mes.de));

  const avisos: { n: number; texto: string; to: string }[] = [
    { n: alertas.faturasPorRegistar, texto: 'venda(s) entregue(s) sem nº de fatura', to: '/vendas?vista=fatura' },
    { n: alertas.faturasRecebidasPorAprovar, texto: 'fatura(s) de fornecedor por aprovar', to: '/compras?tab=pending' },
    { n: alertas.comprasSemFatura, texto: 'compra(s) sem fatura (IVA não dedutível)', to: '/compras' },
    { n: alertas.comprasComDiferenca, texto: 'compra(s) que não batem com o total do documento', to: '/compras' },
  ].filter((a) => a.n > 0);

  return (
    <div className="space-y-4">
      <PageHeader
        title="Dashboard"
        description={`Resumo de ${mesLabel}.`}
        actions={
          <>
            <Button variant="secondary" leftIcon={<Package size={15} />} onClick={() => navigate(`/vendas/nova?tipo=${VENDA_TIPO.Produto}`)}>Venda</Button>
            <Button variant="secondary" leftIcon={<Receipt size={15} />} onClick={() => navigate('/compras/nova')}>Compra</Button>
            <Button leftIcon={<Plus size={15} />} onClick={() => navigate(`/vendas/nova?tipo=${VENDA_TIPO.Reparacao}`)}>Reparação</Button>
          </>
        }
      />

      <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
        <KpiCard icon={ShoppingCart} label="Faturado no mês" value={formatEur(mes.faturado)} sub={`${mes.vendas} venda(s) entregue(s)`} />
        <KpiCard icon={Banknote} label="IVA a entregar (vendas)" value={formatEur(mes.ivaAEntregar)} sub={`IVA nas faturas: ${formatEur(mes.ivaNasVendas)}`} tone="amber" />
        <KpiCard icon={Wallet} label="Lucro das vendas" value={formatEur(mes.lucroVendas)} sub="Depois de peças e IVA" tone="emerald" />
        <KpiCard icon={Receipt} label="Despesas do mês" value={formatEur(mes.despesas)} sub="Pagas, c/ IVA" tone="zinc" />
      </div>

      {avisos.length > 0 && (
        <div role="alert" className="space-y-1 rounded-xl border border-amber-300 bg-amber-50 p-3 text-sm text-amber-900 dark:border-amber-900/50 dark:bg-amber-950/30 dark:text-amber-200">
          {avisos.map((a) => (
            <Link key={a.to + a.texto} to={a.to} className="flex items-center gap-2 hover:underline">
              <AlertTriangle size={14} className="shrink-0" /> <strong>{a.n}</strong> {a.texto}
            </Link>
          ))}
        </div>
      )}

      <div className="grid gap-4 lg:grid-cols-[1fr_20rem]">
        <SectionCard title="Próximas entregas" action={<Link to="/vendas" className="text-sm text-brand-700 hover:underline dark:text-brand-300">Ver vendas</Link>} bodyClassName="p-0">
          {proximasEntregas.length === 0 ? (
            <div className="p-4"><EmptyState icon={Wrench} title="Nada em curso" description="As reparações e serviços em curso aparecem aqui." compact /></div>
          ) : (
            <ul className="divide-y divide-zinc-100 dark:divide-zinc-800">
              {proximasEntregas.map((v) => {
                const Icon = TIPO_ICON[v.tipo];
                return (
                  <li key={v.id}>
                    <Link to={`/vendas/${v.id}`} className="flex min-h-14 items-center justify-between gap-3 px-4 py-2 text-sm hover:bg-zinc-50 dark:hover:bg-zinc-800/50">
                      <span className="flex min-w-0 items-center gap-2">
                        <Icon size={15} className="shrink-0 text-zinc-400" />
                        <span className="min-w-0">
                          <span className="block truncate font-medium">#{v.numero} · {v.descricao ?? '—'}</span>
                          <span className="block truncate text-xs text-zinc-500">
                            {v.cliente ?? 'Consumidor final'}{v.previstoPara ? ` · previsto ${formatDateOnly(v.previstoPara)}` : ''}
                          </span>
                        </span>
                      </span>
                      <span className="flex shrink-0 items-center gap-2">
                        <span className={`rounded-full px-2 py-0.5 text-xs font-medium ${VENDA_ESTADO_COLOR[v.estado]}`}>{VENDA_ESTADO_LABEL[v.estado]}</span>
                        <span className="w-20 text-right tabular-nums">{formatEur(v.totalCents / 100)}</span>
                      </span>
                    </Link>
                  </li>
                );
              })}
            </ul>
          )}
        </SectionCard>

        <div className="space-y-4">
          <SectionCard title="Em curso">
            <dl className="grid grid-cols-2 gap-3 text-sm">
              <Contador label="Orçamentos" valor={emCurso.orcamentos} />
              <Contador label="Em curso" valor={emCurso.emCurso} />
              <Contador label="À espera de peça" valor={emCurso.aEsperaPeca} />
              <Contador label="Prontas a levantar" valor={emCurso.prontas} destaque />
            </dl>
          </SectionCard>
          <SectionCard title={<span className="flex items-center gap-2 text-sm font-semibold"><Boxes size={15} /> Stock</span>}>
            <dl className="space-y-1 text-sm tabular-nums">
              <Linha label="Unidades em stock">{stock.unidades}</Linha>
              <Linha label="Pago pelo stock">{formatEur(stock.valorPago)}</Linha>
              <Linha label="Lucro se vender tudo">{formatEur(stock.lucroSeVenderTudo)}</Linha>
            </dl>
          </SectionCard>
        </div>
      </div>
    </div>
  );
}

function Contador({ label, valor, destaque }: { label: string; valor: number; destaque?: boolean }) {
  return (
    <div className={`rounded-lg p-3 ${destaque && valor > 0 ? 'bg-emerald-50 dark:bg-emerald-950/30' : 'bg-zinc-50 dark:bg-zinc-800/50'}`}>
      <dt className="text-xs text-zinc-500">{label}</dt>
      <dd className="text-2xl font-semibold tabular-nums">{valor}</dd>
    </div>
  );
}

function Linha({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div className="flex justify-between gap-4">
      <dt className="text-zinc-500">{label}</dt>
      <dd className="font-medium">{children}</dd>
    </div>
  );
}
