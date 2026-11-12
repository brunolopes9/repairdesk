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
  // Sprint 633 (Doc 99): categorias do Excel «Gestão Financeira»
  FerramentasIa: 12,
  Subscricoes: 13,
  VpsCopier: 14,
  PropFirms: 15,
  Alimentacao: 20,
  CafeRestaurante: 21,
  Casa: 22,
  Ginasio: 23,
  Suplementacao: 24,
  Lazer: 25,
  Saude: 26,
  Vestuario: 27,
  SegurancaSocial: 30,
  PagamentoIva: 31,
  PagamentoIrs: 32,
  Outro: 99,
  Outras: 99,
} as const;
export type DespesaCategoria = (typeof DESPESA_CATEGORIA)[keyof typeof DESPESA_CATEGORIA];

export const DESPESA_LABEL: Record<DespesaCategoria, string> = {
  0: 'Peças',
  1: 'Material',
  2: 'Ferramentas',
  3: 'Software',
  4: 'Transportes',
  5: 'Comunicações',
  6: 'Marketing',
  7: 'Serviços',
  8: 'Renda',
  9: 'Seguros',
  10: 'Combustível',
  11: 'Peças usadas',
  12: 'Ferramentas IA',
  13: 'Subscrições',
  14: 'VPS/Copier',
  15: 'Prop firms',
  20: 'Alimentação',
  21: 'Café/Restaurante',
  22: 'Casa',
  23: 'Ginásio',
  24: 'Suplementação',
  25: 'Lazer',
  26: 'Saúde',
  27: 'Vestuário',
  30: 'Segurança Social',
  31: 'IVA pago ao Estado',
  32: 'IRS pago',
  99: 'Outras',
};

/** Sprint 633 (Doc 99): a quem pertence a despesa. Só «Negócio» entra no IVA e no IRS. */
export const AMBITO = { Negocio: 0, Pessoal: 1, Obrigacao: 2 } as const;
export type DespesaAmbito = (typeof AMBITO)[keyof typeof AMBITO];
export const AMBITO_LABEL: Record<DespesaAmbito, string> = { 0: 'Negócio', 1: 'Pessoal', 2: 'Obrigações (SS, IVA, IRS)' };
export const NATUREZA = { Gasto: 0, Investimento: 1 } as const;
export type DespesaNatureza = (typeof NATUREZA)[keyof typeof NATUREZA];

export const CATEGORIAS_POR_AMBITO: Record<DespesaAmbito, readonly DespesaCategoria[]> = {
  0: [1, 2, 3, 12, 13, 14, 15, 4, 10, 5, 6, 7, 8, 9, 99],
  1: [20, 21, 22, 4, 10, 23, 24, 25, 26, 27, 99],
  2: [30, 31, 32],
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
  ambito?: DespesaAmbito;
  natureza?: DespesaNatureza;
  /** Sprint 644: sem fatura com o NIF da empresa — só gestão (fora do IVA/IRS). */
  semFaturaValida?: boolean;
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
  ambito?: DespesaAmbito;
  natureza?: DespesaNatureza;
  semFaturaValida?: boolean;
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
