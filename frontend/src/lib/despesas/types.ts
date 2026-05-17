import type { PagedResult } from '../clientes/types';

export const DESPESA_CATEGORIA = {
  Pecas: 0,
  Material: 1,
  Ferramenta: 2,
  Software: 3,
  Transporte: 4,
  Comunicacoes: 5,
  Marketing: 6,
  Servicos: 7,
  Outro: 99,
} as const;
export type DespesaCategoria = (typeof DESPESA_CATEGORIA)[keyof typeof DESPESA_CATEGORIA];

export const DESPESA_LABEL: Record<DespesaCategoria, string> = {
  0: 'Peças',
  1: 'Material',
  2: 'Ferramenta',
  3: 'Software',
  4: 'Transporte',
  5: 'Comunicações',
  6: 'Marketing',
  7: 'Serviços',
  99: 'Outro',
};

export interface Despesa {
  id: string;
  descricao: string;
  categoria: DespesaCategoria;
  valorCents: number;
  data: string;
  fornecedor: string | null;
  numeroEncomenda: string | null;
  notas: string | null;
  trabalhoId: string | null;
  reparacaoId: string | null;
  createdAt: string;
}

export interface CreateDespesaForm {
  descricao: string;
  categoria: DespesaCategoria;
  valorCents: number;
  data: string | null;
  fornecedor: string | null;
  numeroEncomenda: string | null;
  notas: string | null;
  trabalhoId: string | null;
  reparacaoId: string | null;
}

export type DespesasPage = PagedResult<Despesa>;
