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
  Renda: 8,
  Seguros: 9,
  Combustivel: 10,
  PecasUsadas: 11,
  Outro: 99,
  Outras: 99,
} as const;
export type DespesaCategoria = (typeof DESPESA_CATEGORIA)[keyof typeof DESPESA_CATEGORIA];

export const DESPESA_LABEL: Record<DespesaCategoria, string> = {
  0: 'Pecas',
  1: 'Material',
  2: 'Ferramenta',
  3: 'Software',
  4: 'Transporte',
  5: 'Comunicacoes',
  6: 'Marketing',
  7: 'Servicos',
  8: 'Renda',
  9: 'Seguros',
  10: 'Combustivel',
  11: 'Pecas usadas',
  99: 'Outras',
};

export const STOCK_DESPESA_CATEGORIAS = [
  DESPESA_CATEGORIA.Pecas,
  DESPESA_CATEGORIA.Material,
  DESPESA_CATEGORIA.PecasUsadas,
] as const satisfies readonly DespesaCategoria[];

export const OPEX_DESPESA_CATEGORIAS = [
  DESPESA_CATEGORIA.Renda,
  DESPESA_CATEGORIA.Software,
  DESPESA_CATEGORIA.Combustivel,
  DESPESA_CATEGORIA.Comunicacoes,
  DESPESA_CATEGORIA.Seguros,
  DESPESA_CATEGORIA.Outras,
] as const satisfies readonly DespesaCategoria[];

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
  // Sprint 176: COGS flag - peca consumida em reparacao (nao OpEx para IVA report).
  isCogs: boolean;
  isRecorrente: boolean;
  periodicidadeMeses: number | null;
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
  isCogs: boolean;
  isRecorrente: boolean;
  periodicidadeMeses: number | null;
}

export type DespesasPage = PagedResult<Despesa>;
