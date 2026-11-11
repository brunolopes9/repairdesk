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
  createdAt: string;
  // Sprint 176: COGS flag - peca consumida em reparacao (nao OpEx para IVA report).
  isCogs: boolean;
  isRecorrente: boolean;
  periodicidadeMeses: number | null;
  /** Sprint 573: IVA da fatura (cêntimos); null = por indicar. */
  ivaCents?: number | null;
  /** % do IVA que é dedutível (100, 50, 0…). */
  deducaoPercent?: number;
  /** Fornecedor intra-UE: autoliquidação (fatura a 0%). */
  reverseCharge?: boolean;
  /** Sprint 622: 0 = LopesTech, 1 = Trading (sem dedução), 2 = Mista (pro rata). */
  afetacao?: AfetacaoIva;
  /** Sprint 584: nº visível (sequencial por empresa), como nas compras. */
  numero?: number;
  /** Sprint 586: fatura (PDF ou foto) desta despesa — abre em supplierInvoicesApi.pdfPath; null = por anexar. */
  supplierInvoiceImportId?: string | null;
}

export interface CreateDespesaForm {
  descricao: string;
  categoria: DespesaCategoria;
  valorCents: number;
  data: string | null;
  fornecedor: string | null;
  numeroEncomenda: string | null;
  notas: string | null;
  isCogs: boolean;
  isRecorrente: boolean;
  periodicidadeMeses: number | null;
  ivaCents?: number | null;
  deducaoPercent?: number;
  reverseCharge?: boolean;
  afetacao?: AfetacaoIva;
}

export type DespesasPage = PagedResult<Despesa>;

/** Sprint 622 (Doc 98 F6): a que atividade serve o gasto — num sujeito passivo misto define o IVA dedutível. */
export const AFETACAO = { LopesTech: 0, Trading: 1, Mista: 2 } as const;
export type AfetacaoIva = (typeof AFETACAO)[keyof typeof AFETACAO];
export const AFETACAO_LABEL: Record<AfetacaoIva, string> = {
  0: 'LopesTech (reparações, loja, software)',
  1: 'Trading (sem dedução de IVA)',
  2: 'As duas — mista (pro rata)',
};
