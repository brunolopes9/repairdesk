import { ArrowDown, ArrowUp, ArrowUpDown } from 'lucide-react';
import type { Ordem } from '../../lib/useOrdenacao';

/** Sprint 561: cabeçalho de coluna clicável (crescente ↔ decrescente), acessível por teclado. */
export function SortableTh<C extends string>({
  label, campo, ordem, onOrdenar, align = 'left', className = '',
}: {
  label: string;
  campo: C;
  ordem: Ordem<C>;
  onOrdenar: (campo: C) => void;
  align?: 'left' | 'right';
  className?: string;
}) {
  const ativa = ordem.campo === campo;
  const Icon = !ativa ? ArrowUpDown : ordem.desc ? ArrowDown : ArrowUp;
  return (
    <th
      className={`font-medium ${align === 'right' ? 'text-right' : 'text-left'} ${className}`}
      aria-sort={ativa ? (ordem.desc ? 'descending' : 'ascending') : 'none'}
    >
      <button
        type="button"
        onClick={() => onOrdenar(campo)}
        className={`inline-flex items-center gap-1 rounded hover:text-zinc-900 focus:outline-none focus-visible:ring-2 focus-visible:ring-brand-400 dark:hover:text-zinc-100 ${ativa ? 'text-zinc-900 dark:text-zinc-100' : ''} ${align === 'right' ? 'flex-row-reverse' : ''}`}
        title={ativa ? (ordem.desc ? 'Decrescente — clica para crescente' : 'Crescente — clica para decrescente') : 'Ordenar'}
      >
        {label}
        <Icon size={12} className={ativa ? '' : 'opacity-40'} aria-hidden />
      </button>
    </th>
  );
}
