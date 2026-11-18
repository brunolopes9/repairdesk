import { Link } from 'react-router-dom';
import { Code2, Package, Wrench } from 'lucide-react';
import { EmptyState, SectionCard } from '../../components/ui';
import { formatEur } from '../../lib/compras/format';
import { formatDateOnly } from '../../lib/money';
import type { PainelVendaResumo } from '../../lib/dashboard/api';
import { VENDA_ESTADO_COLOR, VENDA_ESTADO_LABEL, type VendaEstado } from '../../lib/vendas/types';

const TIPO_ICON = { 0: Package, 1: Wrench, 2: Code2 } as const;

/** Faixa de 4 px à esquerda de cada linha, com a cor do estado (como no Excel do Bruno). */
const FAIXA: Record<VendaEstado, string> = {
  0: 'bg-zinc-300 dark:bg-zinc-600',
  5: 'bg-sky-500',
  1: 'bg-amber-500',
  2: 'bg-emerald-500',
  3: 'bg-emerald-600',
  4: 'bg-zinc-300',
};

/** Reparações e serviços em curso: primeiro as prontas a levantar, depois pela data prevista. */
export function EntregasLista({ vendas }: { vendas: PainelVendaResumo[] }) {
  const atrasada = (v: PainelVendaResumo) => v.previstoPara !== null && v.estado !== 2 && new Date(v.previstoPara) < new Date(new Date().toDateString());
  return (
    <SectionCard
      title={
        <span className="flex items-center gap-2 text-sm font-semibold">
          <Wrench size={15} className="text-brand-600" /> Próximas entregas
        </span>
      }
      action={<Link to="/vendas?vista=curso" className="text-sm text-brand-700 hover:underline dark:text-brand-300">Ver todas</Link>}
      bodyClassName="p-0"
    >
      {vendas.length === 0 ? (
        <div className="p-4"><EmptyState icon={Wrench} title="Nada em curso" description="As reparações e serviços em curso aparecem aqui." compact /></div>
      ) : (
        <ul className="divide-y divide-zinc-100 dark:divide-zinc-800">
          {vendas.map((v) => {
            const Icon = TIPO_ICON[v.tipo];
            const late = atrasada(v);
            return (
              <li key={v.id} className="relative">
                <span className={`absolute inset-y-0 left-0 w-1 ${FAIXA[v.estado]}`} aria-hidden />
                <Link to={`/vendas/${v.id}`} className="flex min-h-12 items-center justify-between gap-3 py-2 pl-5 pr-4 text-sm hover:bg-zinc-50 dark:hover:bg-zinc-800/50">
                  <span className="flex min-w-0 items-center gap-2.5">
                    <Icon size={15} className="shrink-0 text-zinc-400" />
                    <span className="min-w-0">
                      <span className="block truncate font-medium">
                        <span className="tabular-nums text-zinc-400">#{v.numero}</span> {v.descricao ?? '—'}
                      </span>
                      <span className="block truncate text-xs text-zinc-500">
                        {v.cliente ?? 'Consumidor final'}
                        {v.previstoPara && (
                          <span className={late ? 'font-medium text-rose-600 dark:text-rose-400' : ''}>
                            {' · '}{late ? 'atrasada desde' : 'previsto'} {formatDateOnly(v.previstoPara)}
                          </span>
                        )}
                      </span>
                    </span>
                  </span>
                  <span className="flex shrink-0 items-center gap-3">
                    <span className={`rounded-full px-2 py-0.5 text-xs font-medium ${VENDA_ESTADO_COLOR[v.estado]}`}>{VENDA_ESTADO_LABEL[v.estado]}</span>
                    <span className="hidden w-20 text-right font-medium tabular-nums sm:block">{formatEur(v.totalCents / 100)}</span>
                  </span>
                </Link>
              </li>
            );
          })}
        </ul>
      )}
    </SectionCard>
  );
}
