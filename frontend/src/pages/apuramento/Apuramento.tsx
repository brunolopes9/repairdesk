import { useState } from 'react';
import { Link } from 'react-router-dom';
import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { AlertTriangle, BadgeCheck, CalendarClock, CheckCircle2, ChevronLeft, ChevronRight } from 'lucide-react';
import { useConfirm } from '../../components/ConfirmDialog';
import { Button, PageHeader, SectionCard, SkeletonCard } from '../../components/ui';
import { toast } from '../../lib/toast';
import { apuramentoApi, prazos, trimestreAnterior, type ApuramentoPendente, type TipoPendente } from '../../lib/apuramento/api';
import { formatEur } from '../../lib/compras/format';
import { apiErrorMessage } from '../../lib/errors';
import EstimativasCard from './EstimativasCard';
import LembretesFiscais from './LembretesFiscais';

const PENDENTE: Record<TipoPendente, { titulo: string; ajuda: string; link: (p: ApuramentoPendente) => string }> = {
  venda_sem_fatura: { titulo: 'Vendas entregues sem fatura', ajuda: 'Não entram no IVA até emitires a fatura.', link: (p) => `/vendas/${p.id}` },
  compra_sem_fatura: { titulo: 'Compras sem fatura', ajuda: 'Sem fatura o IVA não se deduz — pede-a ao fornecedor.', link: (p) => `/compras/${p.id}` },
  despesa_sem_iva: { titulo: 'Despesas sem IVA indicado', ajuda: 'Indica o IVA da fatura (ou "Sem IVA") para o deduzir.', link: (p) => `/despesas?tab=approved&edit=${p.id}` },
  despesa_pecas: { titulo: 'Peças registadas como despesa', ajuda: 'Confirma que não estão também em Compras (contariam a dobrar).', link: (p) => `/despesas?tab=approved&edit=${p.id}` },
};

const dataPt = (d: Date | string) => new Date(d).toLocaleDateString('pt-PT');

/**
 * Sprint 573 (Doc 94 Fase 5): IVA e resultado por trimestre, para a declaração periódica.
 * Mostra primeiro o que interessa (quanto pagar e até quando) e o que falta tratar; o detalhe vem a seguir.
 */
export default function Apuramento() {
  const [periodo, setPeriodo] = useState(trimestreAnterior);
  const q = useQuery({
    queryKey: ['apuramento', periodo.ano, periodo.trimestre],
    queryFn: () => apuramentoApi.trimestre(periodo.ano, periodo.trimestre),
    placeholderData: keepPreviousData,
  });
  const mover = (d: number) => setPeriodo((p) => {
    const t = p.trimestre + d;
    return t < 1 ? { ano: p.ano - 1, trimestre: 4 } : t > 4 ? { ano: p.ano + 1, trimestre: 1 } : { ano: p.ano, trimestre: t };
  });
  const r = q.data;
  const qc = useQueryClient();
  const confirm = useConfirm();
  // Sprint 576: trimestre declarado e pago — os valores que foram à declaração ficam guardados.
  const fechar = useMutation({
    mutationFn: () => apuramentoApi.fechar(periodo.ano, periodo.trimestre),
    onSuccess: () => { toast.success('Trimestre marcado como declarado e pago'); qc.invalidateQueries({ queryKey: ['apuramento'] }); },
    onError: (err) => toast.fromError(err, 'Não foi possível marcar.'),
  });
  const reabrir = useMutation({
    mutationFn: () => apuramentoApi.reabrir(periodo.ano, periodo.trimestre),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['apuramento'] }),
    onError: (err) => toast.fromError(err, 'Não foi possível reabrir.'),
  });
  const { entrega, pagamento } = prazos(periodo.ano, periodo.trimestre);
  const grupos = (r?.pendentes ?? []).reduce<Partial<Record<TipoPendente, ApuramentoPendente[]>>>((g, p) => { (g[p.tipo] ??= []).push(p); return g; }, {});

  return (
    <div className="space-y-4">
      <PageHeader title="IVA e resultados" description="Apuramento do IVA por trimestre (declaração periódica) e o lucro do período."
        actions={
          <div className="flex w-full items-center justify-between gap-1 rounded-lg border border-zinc-200 bg-white p-1 sm:w-auto dark:border-zinc-800 dark:bg-zinc-900">
            <button type="button" onClick={() => mover(-1)} aria-label="Trimestre anterior" className="grid h-9 w-9 place-items-center rounded-md hover:bg-zinc-100 dark:hover:bg-zinc-800"><ChevronLeft size={16} /></button>
            <span className="min-w-28 text-center text-sm font-semibold tabular-nums">{periodo.trimestre}.º trim. {periodo.ano}</span>
            <button type="button" onClick={() => mover(1)} aria-label="Trimestre seguinte" className="grid h-9 w-9 place-items-center rounded-md hover:bg-zinc-100 dark:hover:bg-zinc-800"><ChevronRight size={16} /></button>
          </div>
        } />

      {q.isLoading ? <SkeletonCard /> : q.isError || !r ? <p className="text-sm text-rose-600">{apiErrorMessage(q.error)}</p> : (
        <div className={`space-y-4 ${q.isFetching ? 'opacity-60' : ''}`}>
          <LembretesFiscais />
          {r.fecho && (
            <div className={`flex flex-col gap-2 rounded-xl border p-3 text-sm sm:flex-row sm:items-center sm:justify-between ${Math.abs(r.fecho.diferenca) >= 0.01
              ? 'border-rose-300 bg-rose-50 text-rose-900 dark:border-rose-900/50 dark:bg-rose-950/30 dark:text-rose-200'
              : 'border-emerald-200 bg-emerald-50 text-emerald-900 dark:border-emerald-900/50 dark:bg-emerald-950/30 dark:text-emerald-200'}`}>
              <span className="flex items-start gap-2">
                <BadgeCheck size={16} className="mt-0.5 shrink-0" />
                <span>
                  Declarado e pago em {dataPt(r.fecho.fechadoEm)}: {formatEur(r.fecho.saldo)}.
                  {Math.abs(r.fecho.diferenca) >= 0.01 && <> <strong>Depois disso mudou {formatEur(r.fecho.diferenca)}</strong> — alguma compra, venda ou despesa deste trimestre foi alterada; fala com o contabilista sobre uma declaração de substituição.</>}
                </span>
              </span>
              <button type="button" className="self-start text-xs underline sm:self-auto"
                onClick={async () => { if (await confirm({ title: 'Reabrir o trimestre?', description: 'Deixa de estar marcado como declarado e pago.', confirmLabel: 'Reabrir' })) reabrir.mutate(); }}>
                Reabrir
              </button>
            </div>
          )}

          {/* Quanto e até quando */}
          <div className="grid grid-cols-1 gap-3 md:grid-cols-3">
            <div className={`rounded-xl border p-4 md:col-span-2 ${r.saldoFinal > 0 ? 'border-amber-300 bg-amber-50 dark:border-amber-900/50 dark:bg-amber-950/30' : 'border-emerald-300 bg-emerald-50 dark:border-emerald-900/50 dark:bg-emerald-950/30'}`}>
              <p className="text-sm text-zinc-600 dark:text-zinc-300">{r.saldoFinal > 0 ? 'IVA a entregar ao Estado' : r.saldoFinal < 0 ? 'IVA a recuperar (fica a crédito para o próximo trimestre)' : 'IVA a entregar'}</p>
              <p className="mt-1 text-3xl font-bold tabular-nums">{formatEur(Math.abs(r.saldoFinal))}</p>
              <p className="mt-1 text-xs text-zinc-600 dark:text-zinc-400">
                {formatEur(r.ivaLiquidado)} liquidado − {formatEur(r.ivaDedutivel)} dedutível{r.reporte > 0 ? ` − ${formatEur(r.reporte)} de crédito do trimestre anterior` : ''} · {dataPt(r.de)} a {dataPt(r.ate)} · {r.faturas} fatura(s)
              </p>
            </div>
            <div className="rounded-xl border border-zinc-200 bg-white p-4 text-sm dark:border-zinc-800 dark:bg-zinc-900">
              <p className="flex items-center gap-1.5 font-medium"><CalendarClock size={15} /> Prazos (regime trimestral)</p>
              <p className="mt-2 text-zinc-600 dark:text-zinc-300">Declaração até <strong>{dataPt(entrega)}</strong></p>
              {r.saldoFinal > 0 && <p className="text-zinc-600 dark:text-zinc-300">Pagamento até <strong>{dataPt(pagamento)}</strong></p>}
              {!r.fecho && (
                <Button type="button" size="sm" variant="secondary" className="mt-3" leftIcon={<BadgeCheck size={14} />} loading={fechar.isPending}
                  onClick={async () => { if (await confirm({ title: `Marcar o ${r.trimestre}.º trimestre como declarado e pago?`, description: `Guarda os valores de agora (${formatEur(r.saldoFinal)}). Se algo deste trimestre mudar depois, aparece a diferença.`, confirmLabel: 'Marcar' })) fechar.mutate(); }}>
                  Marcar como declarado
                </Button>
              )}
            </div>
          </div>

          {/* O que falta tratar */}
          {r.pendentes.length === 0 ? (
            <p className="flex items-center gap-2 rounded-xl border border-emerald-200 bg-emerald-50 p-3 text-sm text-emerald-800 dark:border-emerald-900/50 dark:bg-emerald-950/30 dark:text-emerald-300">
              <CheckCircle2 size={16} /> Nada por tratar neste trimestre — o apuramento está completo.
            </p>
          ) : (
            <SectionCard title={<span className="flex items-center gap-2 text-sm font-semibold text-amber-800 dark:text-amber-300"><AlertTriangle size={15} /> Por tratar antes de declarar ({r.pendentes.length})</span>} bodyClassName="divide-y divide-zinc-100 dark:divide-zinc-800">
              {(Object.keys(grupos) as TipoPendente[]).map((tipo) => (
                <div key={tipo} className="px-4 py-3">
                  <p className="text-sm font-medium">{PENDENTE[tipo].titulo} <span className="text-zinc-500">({grupos[tipo]!.length})</span></p>
                  <p className="mb-2 text-xs text-zinc-500">{PENDENTE[tipo].ajuda}</p>
                  <ul className="space-y-1">
                    {grupos[tipo]!.slice(0, 20).map((p) => (
                      <li key={p.id}>
                        <Link to={PENDENTE[tipo].link(p)} className="flex items-baseline justify-between gap-3 rounded-md px-2 py-1.5 text-sm hover:bg-zinc-50 dark:hover:bg-zinc-800/50">
                          <span className="min-w-0 truncate text-brand-700 dark:text-brand-300">{p.descricao}</span>
                          <span className="flex-none text-xs tabular-nums text-zinc-500">{dataPt(p.data)} · {formatEur(p.valor)}</span>
                        </Link>
                      </li>
                    ))}
                    {grupos[tipo]!.length > 20 && <li className="px-2 text-xs text-zinc-500">e mais {grupos[tipo]!.length - 20}…</li>}
                  </ul>
                </div>
              ))}
            </SectionCard>
          )}

          <div className="grid grid-cols-1 gap-4 lg:grid-cols-2">
            <SectionCard title="IVA liquidado (vendas faturadas)">
              <dl className="space-y-2 text-sm tabular-nums">
                {r.vendasPorTaxa.length === 0 && <p className="text-zinc-500">Sem faturas emitidas neste trimestre.</p>}
                {r.vendasPorTaxa.map((t) => (
                  <Linha key={t.taxa} label={t.taxa === 0 ? 'Isento / 0%' : `Taxa ${t.taxa}%`}
                    sub={t.taxa === 0 ? `base ${formatEur(t.base)} · UE ${formatEur(r.vendasIsentasUe)} (vai à recapitulativa) · fora da UE ${formatEur(r.vendasIsentasForaUe)}` : `base ${formatEur(t.base)}`} valor={t.iva} />
                ))}
                {r.ivaAutoliquidado > 0 && <Linha label="Autoliquidação (compras e serviços UE)" sub="liquida e deduz" valor={r.ivaAutoliquidado} />}
                <Linha label="Total liquidado" valor={r.ivaLiquidado} forte />
              </dl>
            </SectionCard>
            <SectionCard title="IVA dedutível">
              <dl className="space-y-2 text-sm tabular-nums">
                <Linha label="Compras de peças (nacionais, com fatura)" sub={`base ${formatEur(r.compras.baseNacional)}`} valor={r.compras.ivaNacional} />
                <Linha label="Portes das compras" valor={r.compras.ivaPortes} />
                <Linha label="Despesas" sub={r.despesas.ivaNaoDedutivel > 0 ? `${formatEur(r.despesas.ivaNaoDedutivel)} não dedutível` : undefined} valor={r.despesas.ivaDedutivel} />
                {r.ivaAutoliquidado > 0 && <Linha label="Autoliquidação (compras e serviços UE)" valor={r.ivaAutoliquidado} />}
                <Linha label="Total dedutível" valor={r.ivaDedutivel} forte />
              </dl>
              {r.compras.baseComIvaEstrangeiro > 0 && (
                <p className="mt-3 text-xs text-zinc-500">
                  Compras na UE com IVA do país do fornecedor ({formatEur(r.compras.baseComIvaEstrangeiro)}): esse IVA não se deduz cá — conta só como custo.
                </p>
              )}
              {r.compras.baseForaUe > 0 && (
                <p className="mt-3 text-xs text-amber-700 dark:text-amber-400">
                  Compras fora da UE ({formatEur(r.compras.baseForaUe)}): o IVA pago na alfândega não está incluído — junta o documento de importação.
                </p>
              )}
            </SectionCard>
          </div>

          <EstimativasCard e={r.estimativas} ano={r.ano} trimestre={r.trimestre} />

          <div className="grid grid-cols-1 gap-4 lg:grid-cols-2">
            <SectionCard title="Resultado do trimestre (sem IVA)">
              <dl className="space-y-2 text-sm tabular-nums">
                <Linha label="Vendas faturadas da loja" valor={r.resultado.vendas} />
                <Linha label="Custo das peças vendidas" valor={-r.resultado.custoPecasVendidas} />
                <Linha label="Despesas" sub="inclui IVA não dedutível" valor={-r.resultado.gastos} />
                <Linha label="Resultado da loja" valor={r.resultado.resultado} forte cor />
                {r.resultado.trading > 0 && <Linha label="Trading (à parte, fora da loja)" sub="rendimentos faturados no trimestre" valor={r.resultado.trading} />}
              </dl>
              <p className="mt-3 text-xs text-zinc-500">
                Compras de stock no trimestre: {formatEur(r.resultado.comprasDeStock)} — só contam como custo quando as peças se vendem.
              </p>
            </SectionCard>
            <SectionCard title="Campos da declaração periódica" bodyClassName="p-0">
              <table className="w-full text-sm">
                <tbody className="divide-y divide-zinc-100 dark:divide-zinc-800">
                  {r.campos.map((c, i) => (
                    <tr key={i}>
                      <td className="w-12 px-4 py-2 text-center font-mono text-xs text-zinc-500">{c.campo}</td>
                      <td className="py-2 pr-2">{c.descricao}</td>
                      <td className="px-4 py-2 text-right tabular-nums">{formatEur(c.valor)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
              <p className="border-t border-zinc-100 px-4 py-3 text-xs text-zinc-500 dark:border-zinc-800">
                Valores para conferir com o contabilista antes de submeter no Portal das Finanças.
              </p>
            </SectionCard>
          </div>
        </div>
      )}
    </div>
  );
}

function Linha({ label, sub, valor, forte, cor }: { label: string; sub?: string; valor: number; forte?: boolean; cor?: boolean }) {
  return (
    <div className={`flex items-baseline justify-between gap-4 ${forte ? 'border-t border-zinc-100 pt-2 font-semibold dark:border-zinc-800' : ''}`}>
      <dt className="min-w-0">
        <span className={forte ? '' : 'text-zinc-600 dark:text-zinc-300'}>{label}</span>
        {sub && <span className="block text-xs font-normal text-zinc-500">{sub}</span>}
      </dt>
      <dd className={cor ? (valor < 0 ? 'text-rose-600' : 'text-emerald-600') : ''}>{formatEur(valor)}</dd>
    </div>
  );
}
