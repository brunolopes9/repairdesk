import { useState } from 'react';

/**
 * Sprint 561: ordenação de tabelas por clique no cabeçalho. 1.º clique numa coluna = crescente,
 * 2.º = decrescente. Nas listas paginadas o servidor ordena (`sort`/`dir`); nas outras usa-se
 * {@link ordenarLista}.
 */
export interface Ordem<C extends string> {
  campo: C;
  desc: boolean;
}

export function useOrdenacao<C extends string>(campoInicial: C, descInicial = true) {
  const [ordem, setOrdem] = useState<Ordem<C>>({ campo: campoInicial, desc: descInicial });
  function alternar(campo: C) {
    setOrdem((o) => (o.campo === campo ? { campo, desc: !o.desc } : { campo, desc: false }));
  }
  return { ordem, alternar, params: { sort: ordem.campo, dir: ordem.desc ? 'desc' : 'asc' } as const };
}

type Valor = string | number | Date | null | undefined;

/** Ordena em memória com comparação natural em pt-PT (números e datas pelo valor, texto sem acentos). */
export function ordenarLista<T, C extends string>(items: readonly T[], ordem: Ordem<C>, chaves: Record<C, (item: T) => Valor>): T[] {
  const chave = chaves[ordem.campo];
  const fator = ordem.desc ? -1 : 1;
  return [...items].sort((a, b) => fator * comparar(chave(a), chave(b)));
}

function comparar(a: Valor, b: Valor): number {
  if (a == null || a === '') return b == null || b === '' ? 0 : 1; // vazios sempre no fim
  if (b == null || b === '') return -1;
  if (a instanceof Date || b instanceof Date) return new Date(a).getTime() - new Date(b).getTime();
  if (typeof a === 'number' && typeof b === 'number') return a - b;
  return String(a).localeCompare(String(b), 'pt-PT', { numeric: true, sensitivity: 'base' });
}
