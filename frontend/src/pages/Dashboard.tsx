import type { ReactNode } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { AlertTriangle, Banknote, Boxes, CalendarCheck, Package, PackageCheck, Plus, Receipt, ShoppingCart, Wallet } from 'lucide-react';
import { Button, PageHeader, SectionCard, SkeletonCard } from '../components/ui';
import { dashboardApi } from '../lib/dashboard/api';
import { formatEur } from '../lib/compras/format';
import { apiErrorMessage } from '../lib/errors';
import LembretesFiscais from './apuramento/LembretesFiscais';
import { AgendaProximos } from './agendamentos/AgendaProximos';
import { RotinaAgora } from './rotina/RotinaAgora';
import { VENDA_TIPO } from '../lib/vendas/types';
import { Destaque } from './dashboard/Destaque';
import { Vendas30Dias } from './dashboard/Vendas30Dias';
import { EntregasLista } from './dashboard/EntregasLista';

/**
 * Doc 94 + redesenho 18/11/2026: em cima, 4 números grandes que se clicam (hoje, o mês contra o mês
 * passado, lucro, prontas a levantar); a seguir o gráfico dos últimos 30 dias e as próximas entregas;
 * à direita a agenda, o que está em curso, as contas do mês e o stock.
 */
export default function Dashboard() {
  const navigate = useNavigate();
  const painel = useQuery({ queryKey: ['dashboard'], queryFn: dashboardApi.get, refetchInterval: 60_000 });

  if (painel.isLoading) return <SkeletonCard />;
  if (painel.isError || !painel.data) return <p className="text-sm text-rose-600">{apiErrorMessage(painel.error)}</p>;
  const { mes, emCurso, alertas, stock, proximasEntregas, ritmo } = painel.data;
  const mesLabel = new Intl.DateTimeFormat('pt-PT', { month: 'long', year: 'numeric' }).format(new Date(mes.de));

  const anterior = ritmo?.faturadoMesAnteriorAteHoje ?? 0;
  const variacao = anterior > 0 ? Math.round(((mes.faturado - anterior) / anterior) * 100) : null;
  const paradas = ritmo?.paradas ?? 0;

  const avisos: { n: number; texto: string; to: string }[] = [
    { n: alertas.faturasPorRegistar, texto: 'venda(s) entregue(s) sem nº de fatura', to: '/vendas?vista=fatura' },
    { n: alertas.faturasRecebidasPorAprovar, texto: 'fatura(s) de fornecedor por aprovar', to: '/compras?tab=pending' },
    { n: alertas.comprasSemFatura, texto: 'compra(s) sem fatura (IVA não dedutível)', to: '/compras' },
  ].filter((a) => a.n > 0);

  return (
    <div className="space-y-5">
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

      <RotinaAgora />

      <LembretesFiscais />

      <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
        <Destaque
          icon={CalendarCheck}
          titulo="Hoje"
          valor={formatEur(ritmo?.faturadoHoje ?? 0)}
          detalhe={`${ritmo?.vendasHoje ?? 0} venda(s) entregue(s)`}
          tom="brand"
          to="/vendas?vista=entregues"
        />
        <Destaque
          icon={ShoppingCart}
          titulo="Este mês"
          valor={formatEur(mes.faturado)}
          detalhe={`${mes.vendas} venda(s) entregue(s)`}
          variacao={variacao === null ? undefined : `${variacao >= 0 ? '+' : '−'}${Math.abs(variacao)}% que o mês passado até hoje`}
          variacaoBoa={variacao === null ? undefined : variacao >= 0}
          tom="emerald"
          to="/vendas?vista=entregues"
        />
        <Destaque
          icon={Wallet}
          titulo="Lucro do mês"
          valor={formatEur(mes.lucroVendas)}
          detalhe="Depois de peças e IVA"
          tom="violet"
          to="/apuramento"
        />
        <Destaque
          icon={PackageCheck}
          titulo="Prontas a levantar"
          valor={String(emCurso.prontas)}
          detalhe={`${emCurso.emCurso + emCurso.aEsperaPeca + emCurso.orcamentos} ainda em curso`}
          variacao={paradas > 0 ? `${paradas} parada(s) há mais de 3 dias` : undefined}
          variacaoBoa={paradas > 0 ? false : undefined}
          tom="amber"
          to="/vendas?vista=curso"
        />
      </div>

      {avisos.length > 0 && (
        <div role="alert" className="space-y-1 rounded-xl border border-l-4 border-amber-300 border-l-amber-500 bg-amber-50 p-3 text-sm text-amber-900 dark:border-amber-900/50 dark:bg-amber-950/30 dark:text-amber-200">
          {avisos.map((a) => (
            <Link key={a.to + a.texto} to={a.to} className="flex items-center gap-2 hover:underline">
              <AlertTriangle size={14} className="shrink-0" /> <strong>{a.n}</strong> {a.texto}
            </Link>
          ))}
        </div>
      )}

      <div className="grid grid-cols-1 gap-4 lg:grid-cols-[1fr_20rem]">
        <div className="min-w-0 space-y-4">
          {ritmo && <Vendas30Dias dias={ritmo.ultimos30Dias} />}
          <EntregasLista vendas={proximasEntregas} />
        </div>

        <div className="space-y-4">
          <AgendaProximos />
          <SectionCard title="Em curso">
            <dl className="grid grid-cols-2 gap-2 text-sm">
              <Contador label="Orçamentos" valor={emCurso.orcamentos} cor="zinc" />
              <Contador label="Em curso" valor={emCurso.emCurso} cor="sky" />
              <Contador label="À espera de peça" valor={emCurso.aEsperaPeca} cor="amber" />
              <Contador label="Prontas a levantar" valor={emCurso.prontas} cor="emerald" />
            </dl>
          </SectionCard>
          <SectionCard title={<span className="flex items-center gap-2 text-sm font-semibold"><Banknote size={15} className="text-amber-600" /> Contas do mês</span>}>
            <dl className="space-y-1.5 text-sm tabular-nums">
              <Linha label="IVA das vendas">{formatEur(mes.ivaNasVendas)}</Linha>
              <Linha label="Despesas pagas (c/ IVA)">{formatEur(mes.despesas)}</Linha>
            </dl>
            {/* Sprint 578: o IVA das vendas não é o que se paga ao Estado; o do trimestre está em IVA e resultados. */}
            <p className="mt-2 text-xs text-zinc-500">
              O IVA a pagar do trimestre (com compras e despesas) está em{' '}
              <Link to="/apuramento" className="text-brand-700 hover:underline dark:text-brand-300">IVA e resultados</Link>.
            </p>
          </SectionCard>
          <SectionCard title={<span className="flex items-center gap-2 text-sm font-semibold"><Boxes size={15} className="text-brand-600" /> Stock</span>}>
            <dl className="space-y-1.5 text-sm tabular-nums">
              <Linha label="Unidades em stock">{stock.unidades}</Linha>
              <Linha label="Pago pelo stock">{formatEur(stock.valorPago)}</Linha>
              <Linha label="Lucro se vender tudo"><span className="text-emerald-700 dark:text-emerald-400">{formatEur(stock.lucroSeVenderTudo)}</span></Linha>
            </dl>
          </SectionCard>
        </div>
      </div>
    </div>
  );
}

const CONTADOR = {
  zinc: 'border-l-zinc-300 bg-zinc-50 dark:bg-zinc-800/50',
  sky: 'border-l-sky-500 bg-sky-50 dark:bg-sky-950/30',
  amber: 'border-l-amber-500 bg-amber-50 dark:bg-amber-950/30',
  emerald: 'border-l-emerald-500 bg-emerald-50 dark:bg-emerald-950/30',
} as const;

function Contador({ label, valor, cor }: { label: string; valor: number; cor: keyof typeof CONTADOR }) {
  return (
    <Link to="/vendas?vista=curso" className={`block rounded-lg border-l-4 p-3 transition hover:brightness-95 ${CONTADOR[cor]} ${valor === 0 ? 'opacity-60' : ''}`}>
      <dt className="text-xs text-zinc-600 dark:text-zinc-400">{label}</dt>
      <dd className="text-2xl font-semibold tabular-nums">{valor}</dd>
    </Link>
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
