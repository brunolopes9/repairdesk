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

/**
 * Sprint 592: como {@link useOrdenacao}, mas a ordem vive no URL (`sort`/`dir`, via {@link useFiltrosUrl}) — ao voltar
 * a uma lista, a ordenação mantém-se.
 */
export function useOrdenacaoUrl<C extends string>(
  campoInicial: C,
  f: { sort?: string; dir?: string },
  set: (patch: { sort?: string; dir?: string }) => void,
) {
  const ordem: Ordem<C> = { campo: (f.sort || campoInicial) as C, desc: (f.dir || 'desc') === 'desc' };
  function alternar(campo: C) {
    const desc = ordem.campo === campo ? !ordem.desc : false;
    set({ sort: campo, dir: desc ? 'desc' : 'asc' });
  }
  return { ordem, alternar, params: { sort: ordem.campo, dir: ordem.desc ? 'desc' : 'asc' } as const };
}

type Valor =string | number | Date | null | undefined;

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
