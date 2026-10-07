import { useState } from 'react';
import { Link, useNavigate, useSearchParams } from 'react-router-dom';
import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { Code2, Package, Plus, Receipt, Search, Wrench } from 'lucide-react';
import { Button, EmptyState, PageHeader, SectionCard, SkeletonRow, ViewTabs } from '../../components/ui';
import { inputCls } from '../../components/ui/formClasses';
import { formatEur } from '../../lib/compras/format';
import { formatDateOnly } from '../../lib/money';
import { vendasApi } from '../../lib/vendas/api';
import {
  VENDA_ESTADO,
  VENDA_ESTADO_COLOR,
  VENDA_ESTADO_LABEL,
  VENDA_TIPO,
  VENDA_TIPO_LABEL,
  type VendaFiltro,
  type VendaTipo,
} from '../../lib/vendas/types';

/** Doc 94 Fase 4: um só ecrã para Reparações, Serviços (software/websites) e Produtos. */
const VISTAS = [
  { key: 'curso', label: 'Em curso' },
  { key: 'entregues', label: 'Entregues' },
  { key: 'fatura', label: 'Fatura por registar' },
  { key: 'todas', label: 'Todas' },
] as const;
type Vista = (typeof VISTAS)[number]['key'];

const TIPO_ICON = { 0: Package, 1: Wrench, 2: Code2 } as const;
const PAGE_SIZE = 50;

export default function Vendas() {
  const navigate = useNavigate();
  const [params, setParams] = useSearchParams();
  const vista = (VISTAS.find((v) => v.key === params.get('vista'))?.key ?? 'curso') as Vista;
  const tipoParam = params.get('tipo');
  const tipo = tipoParam != null && tipoParam !== '' ? (Number(tipoParam) as VendaTipo) : undefined;
  const [q, setQ] = useState('');
  const [page, setPage] = useState(1);

  function setParam(key: string, value: string | null) {
    const next = new URLSearchParams(params);
    if (value == null) next.delete(key);
    else next.set(key, value);
    setParams(next, { replace: true });
    setPage(1);
  }

  const filtro: VendaFiltro = {
    q: q.trim() || undefined,
    tipo,
    page,
    pageSize: PAGE_SIZE,
    ...(vista === 'curso' ? { emCurso: true } : {}),
    ...(vista === 'entregues' ? { estado: VENDA_ESTADO.Entregue } : {}),
    ...(vista === 'fatura' ? { faturaPorRegistar: true } : {}),
  };

  const list = useQuery({ queryKey: ['vendas', filtro], queryFn: () => vendasApi.list(filtro), placeholderData: keepPreviousData });
  const porFaturar = useQuery({ queryKey: ['vendas', 'fatura-count'], queryFn: () => vendasApi.list({ faturaPorRegistar: true, pageSize: 1 }) });

  const items = list.data?.items ?? [];
  const total = list.data?.total ?? 0;
  const pages = Math.max(1, Math.ceil(total / PAGE_SIZE));

  return (
    <div className="space-y-4">
      <PageHeader
        title="Vendas"
        description="Reparações, serviços e produtos. O IVA a entregar e o lucro são calculados em cada venda."
        actions={
          <>
            <Button variant="secondary" leftIcon={<Package size={15} />} onClick={() => navigate(`/vendas/nova?tipo=${VENDA_TIPO.Produto}`)}>Produto</Button>
            <Button variant="secondary" leftIcon={<Code2 size={15} />} onClick={() => navigate(`/vendas/nova?tipo=${VENDA_TIPO.Servico}`)}>Serviço</Button>
            <Button leftIcon={<Plus size={15} />} onClick={() => navigate(`/vendas/nova?tipo=${VENDA_TIPO.Reparacao}`)}>Reparação</Button>
          </>
        }
      />

      <ViewTabs
        value={vista}
        onChange={(v) => setParam('vista', v === 'curso' ? null : v)}
        tabs={VISTAS.map((v) => ({
          key: v.key,
          label: v.label,
          meta: v.key === 'fatura' && porFaturar.data?.total ? porFaturar.data.total : undefined,
        }))}
      />

      <SectionCard
        bodyClassName="p-0"
        title={
          <div className="flex flex-1 flex-wrap items-center gap-2">
            <label className="relative min-w-48 flex-1">
              <Search size={15} className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-zinc-400" />
              <input value={q} onChange={(e) => { setQ(e.target.value); setPage(1); }} className={`${inputCls} pl-9`} placeholder="Nº, cliente, equipamento, fatura…" aria-label="Procurar vendas" />
            </label>
            <select value={tipo ?? ''} onChange={(e) => setParam('tipo', e.target.value || null)} className={`${inputCls} w-auto`} aria-label="Tipo">
              <option value="">Todos os tipos</option>
              {Object.entries(VENDA_TIPO_LABEL).map(([v, label]) => <option key={v} value={v}>{label}</option>)}
            </select>
          </div>
        }
      >
        {!list.isLoading && items.length === 0 ? (
          <div className="p-4">
            <EmptyState
              icon={Receipt}
              title={vista === 'fatura' ? 'Todas as vendas têm fatura registada' : 'Sem vendas aqui'}
              description={vista === 'curso' ? 'Cria uma reparação, um serviço ou uma venda de produto.' : undefined}
            />
          </div>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full min-w-[46rem] text-sm">
              <thead className="border-b border-zinc-100 text-left text-xs text-zinc-500 dark:border-zinc-800">
                <tr>
                  <th className="px-4 py-2 font-medium">Nº</th>
                  <th className="px-4 py-2 font-medium">Cliente / equipamento</th>
                  <th className="px-4 py-2 font-medium">Estado</th>
                  <th className="px-4 py-2 font-medium">Data</th>
                  <th className="px-4 py-2 text-right font-medium">Total</th>
                  <th className="px-4 py-2 text-right font-medium">Lucro</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-zinc-100 dark:divide-zinc-800">
                {list.isLoading && Array.from({ length: 5 }).map((_, i) => <tr key={i}><td colSpan={6}><SkeletonRow columns={6} /></td></tr>)}
                {items.map((v) => {
                  const Icon = TIPO_ICON[v.tipo];
                  return (
                    <tr key={v.id} className="transition hover:bg-zinc-50 dark:hover:bg-zinc-800/50">
                      <td className="px-4 py-3">
                        <Link to={`/vendas/${v.id}`} className="inline-flex items-center gap-1.5 font-medium text-brand-700 hover:underline dark:text-brand-300">
                          <Icon size={14} aria-label={VENDA_TIPO_LABEL[v.tipo]} /> #{v.numero}
                        </Link>
                      </td>
                      <td className="px-4 py-3">
                        <div>{v.cliente?.nome ?? <span className="text-zinc-400">Consumidor final</span>}</div>
                        <div className="text-xs text-zinc-500">{v.equipamento ?? v.problema ?? v.items.map((i) => i.descricao).join(', ')}</div>
                      </td>
                      <td className="px-4 py-3">
                        <span className={`rounded-full px-2 py-0.5 text-xs font-medium ${VENDA_ESTADO_COLOR[v.estado]}`}>{VENDA_ESTADO_LABEL[v.estado]}</span>
                        {v.faturaPorRegistar && <span className="ml-1 rounded-full bg-amber-100 px-2 py-0.5 text-xs font-medium text-amber-800 dark:bg-amber-950/40 dark:text-amber-300">Sem fatura</span>}
                      </td>
                      <td className="px-4 py-3 tabular-nums">{formatDateOnly(v.estado === VENDA_ESTADO.Entregue ? v.data : v.createdAt)}</td>
                      <td className="px-4 py-3 text-right font-medium tabular-nums">{formatEur(v.totalCents / 100)}</td>
                      <td className={`px-4 py-3 text-right tabular-nums ${v.lucro < 0 ? 'text-rose-600' : ''}`}>{formatEur(v.lucro)}</td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        )}
        {pages > 1 && (
          <div className="flex items-center justify-between border-t border-zinc-100 px-4 py-2 text-sm dark:border-zinc-800">
            <span className="text-zinc-500">{total} vendas</span>
            <div className="flex gap-2">
              <Button size="sm" variant="secondary" disabled={page <= 1} onClick={() => setPage((p) => p - 1)}>Anterior</Button>
              <Button size="sm" variant="secondary" disabled={page >= pages} onClick={() => setPage((p) => p + 1)}>Seguinte</Button>
            </div>
          </div>
        )}
      </SectionCard>
    </div>
  );
}
