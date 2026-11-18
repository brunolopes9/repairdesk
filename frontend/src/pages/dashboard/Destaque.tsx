import type { LucideIcon } from 'lucide-react';
import { ChevronRight } from 'lucide-react';
import { Link } from 'react-router-dom';

export type DestaqueTom = 'brand' | 'emerald' | 'amber' | 'violet' | 'rose';

/** Cor de cada tom: faixa do topo, fundo do ícone e número. Tudo com versão escura. */
const TOM: Record<DestaqueTom, { faixa: string; icone: string; fundo: string }> = {
  brand: { faixa: 'bg-brand-500', icone: 'bg-brand-100 text-brand-700 dark:bg-brand-900/40 dark:text-brand-300', fundo: 'from-brand-50/80 dark:from-brand-900/20' },
  emerald: { faixa: 'bg-emerald-500', icone: 'bg-emerald-100 text-emerald-700 dark:bg-emerald-950/50 dark:text-emerald-300', fundo: 'from-emerald-50/80 dark:from-emerald-950/20' },
  amber: { faixa: 'bg-amber-500', icone: 'bg-amber-100 text-amber-800 dark:bg-amber-950/50 dark:text-amber-300', fundo: 'from-amber-50/80 dark:from-amber-950/20' },
  violet: { faixa: 'bg-violet-500', icone: 'bg-violet-100 text-violet-700 dark:bg-violet-950/50 dark:text-violet-300', fundo: 'from-violet-50/80 dark:from-violet-950/20' },
  rose: { faixa: 'bg-rose-500', icone: 'bg-rose-100 text-rose-700 dark:bg-rose-950/50 dark:text-rose-300', fundo: 'from-rose-50/80 dark:from-rose-950/20' },
};

/**
 * Cartão grande do topo do Dashboard: um número, o que quer dizer e para onde leva ao clicar.
 * `variacao` mostra a comparação (ex.: "+12% que o mês passado") a verde ou vermelho.
 */
export function Destaque({
  icon: Icon,
  titulo,
  valor,
  detalhe,
  variacao,
  variacaoBoa,
  tom = 'brand',
  to,
}: {
  icon: LucideIcon;
  titulo: string;
  valor: string;
  detalhe?: string;
  variacao?: string;
  variacaoBoa?: boolean;
  tom?: DestaqueTom;
  to: string;
}) {
  const t = TOM[tom];
  return (
    <Link
      to={to}
      className={`group relative overflow-hidden rounded-xl border border-zinc-200 bg-gradient-to-b ${t.fundo} to-white p-4 shadow-sm transition hover:-translate-y-0.5 hover:shadow-md focus-visible:outline-2 focus-visible:outline-brand-500 dark:border-zinc-800 dark:to-zinc-900`}
    >
      <span className={`absolute inset-x-0 top-0 h-1 ${t.faixa}`} aria-hidden />
      <div className="flex items-center justify-between gap-2">
        <span className="flex items-center gap-2 text-sm font-medium text-zinc-600 dark:text-zinc-300">
          <span className={`grid h-8 w-8 place-items-center rounded-lg ${t.icone}`}>
            <Icon size={16} strokeWidth={2.2} />
          </span>
          {titulo}
        </span>
        <ChevronRight size={16} className="text-zinc-300 transition group-hover:translate-x-0.5 group-hover:text-zinc-500" />
      </div>
      <p className="mt-3 text-3xl font-semibold tabular-nums tracking-tight text-zinc-900 dark:text-zinc-50">{valor}</p>
      {detalhe && <p className="mt-0.5 text-xs text-zinc-500 dark:text-zinc-400">{detalhe}</p>}
      {variacao && (
        <p
          className={`mt-2 inline-flex rounded-full px-2 py-0.5 text-xs font-medium ${
            variacaoBoa === undefined
              ? 'bg-zinc-100 text-zinc-600 dark:bg-zinc-800 dark:text-zinc-300'
              : variacaoBoa
                ? 'bg-emerald-100 text-emerald-700 dark:bg-emerald-950/50 dark:text-emerald-300'
                : 'bg-rose-100 text-rose-700 dark:bg-rose-950/50 dark:text-rose-300'
          }`}
        >
          {variacao}
        </p>
      )}
    </Link>
  );
}
