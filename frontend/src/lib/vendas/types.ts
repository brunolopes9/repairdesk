import type { PagedResult } from '../clientes/types';

/** Doc 94 Fase 4 — espelha VendaTipo no backend. */
export const VENDA_TIPO = { Produto: 0, Reparacao: 1, Servico: 2 } as const;
export type VendaTipo = (typeof VENDA_TIPO)[keyof typeof VENDA_TIPO];
export const VENDA_TIPO_LABEL: Record<VendaTipo, string> = { 0: 'Produto', 1: 'Reparação', 2: 'Serviço' };

/** Espelha VendaEstado. Orçamento não mexe no stock; Cancelada devolve-o. */
export const VENDA_ESTADO = { Orcamento: 0, AEsperaPeca: 1, Pronta: 2, Entregue: 3, Cancelada: 4, EmCurso: 5 } as const;
export type VendaEstado = (typeof VENDA_ESTADO)[keyof typeof VENDA_ESTADO];
export const VENDA_ESTADO_LABEL: Record<VendaEstado, string> = {
  0: 'Orçamento',
  5: 'Em curso',
  1: 'À espera de peça',
  2: 'Pronta',
  3: 'Entregue & paga',
  4: 'Cancelada',
};
export const VENDA_ESTADO_COLOR: Record<VendaEstado, string> = {
  0: 'bg-zinc-100 text-zinc-700 dark:bg-zinc-800 dark:text-zinc-300',
  5: 'bg-sky-100 text-sky-800 dark:bg-sky-950/40 dark:text-sky-300',
  1: 'bg-amber-100 text-amber-800 dark:bg-amber-950/40 dark:text-amber-300',
  2: 'bg-brand-100 text-brand-800 dark:bg-brand-950/40 dark:text-brand-300',
  3: 'bg-emerald-100 text-emerald-700 dark:bg-emerald-950/40 dark:text-emerald-300',
  4: 'bg-zinc-100 text-zinc-400 line-through dark:bg-zinc-800 dark:text-zinc-500',
};

export const PAYMENT_METHOD = {
  Dinheiro: 0,
  Multibanco: 1,
  MBWay: 2,
  TransferenciaBancaria: 3,
  Cartao: 4,
  Outro: 99,
} as const;
export type PaymentMethod = (typeof PAYMENT_METHOD)[keyof typeof PAYMENT_METHOD];
export const PAYMENT_METHOD_LABEL: Record<PaymentMethod, string> = {
  0: 'Dinheiro',
  1: 'Multibanco',
  2: 'MB Way',
  3: 'Transferência',
  4: 'Cartão',
  99: 'Outro',
};

export const CONDICAO_ARTIGO = { NaoAplicavel: 0, Novo: 1, OpenBox: 2, Recondicionado: 3, Usado: 4 } as const;
export type CondicaoArtigo = (typeof CONDICAO_ARTIGO)[keyof typeof CONDICAO_ARTIGO];
export const CONDICAO_ARTIGO_LABEL: Record<CondicaoArtigo, string> = {
  0: '—',
  1: 'Novo',
  2: 'Open-box',
  3: 'Recondicionado',
  4: 'Usado',
};

export interface VendaClienteResumo {
  id: string;
  nome: string;
  telefone: string;
}

export interface VendaItem {
  id: string;
  /** Lote de stock de onde saiu; null = serviço / mão de obra. */
  compraLinhaId: string | null;
  descricao: string;
  quantidade: number;
  precoUnitarioCents: number;
  descontoCents: number;
  /** % (23, 13, 6, 0). */
  ivaRate: number;
  totalCents: number;
  ivaCents: number;
  imei: string | null;
  custoUnitarioPago: number | null;
  taxaIvaCompra: number | null;
  custoDasPecas: number;
  ivaAPagarEstado: number;
  lucro: number;
}

/** Ordem natural do fluxo (os números do enum não são a ordem). */
export const VENDA_FLUXO: VendaEstado[] = [0, 5, 1, 2, 3];

export interface Venda {
  id: string;
  numero: number;
  tipo: VendaTipo;
  estado: VendaEstado;
  /** Data da venda (momento da entrega). */
  data: string;
  createdAt: string;
  cliente: VendaClienteResumo | null;
  equipamento: string | null;
  problema: string | null;
  totalCents: number;
  ivaCents: number;
  ivaAPagarEstado: number;
  lucro: number;
  paymentMethod: PaymentMethod;
  invoiceNumber: string | null;
  invoiceEmittedAt: string | null;
  faturaPorRegistar: boolean;
  notas: string | null;
  items: VendaItem[];
  /** Reparações: slug do portal do cliente (/r/{slug}). */
  publicSlug: string | null;
  previstoPara: string | null;
  garantiaSlug: string | null;
}

export interface VendaLinhaWrite {
  id: string | null;
  compraLinhaId: string | null;
  descricao: string | null;
  quantidade: number;
  precoUnitarioCents: number;
  descontoCents?: number;
  ivaRate?: number | null;
  imei?: string | null;
}

export interface VendaWrite {
  tipo: VendaTipo;
  clienteId: string | null;
  equipamento: string | null;
  problema: string | null;
  notas: string | null;
  linhas: VendaLinhaWrite[];
  /** Só na criação. */
  estado?: VendaEstado;
  paymentMethod?: PaymentMethod | null;
  previstoPara?: string | null;
}

export interface VendaFiltro {
  q?: string;
  tipo?: VendaTipo;
  estado?: VendaEstado;
  emCurso?: boolean;
  faturaPorRegistar?: boolean;
  clienteId?: string;
  from?: string;
  to?: string;
  page?: number;
  pageSize?: number;
}

export type VendasPage = PagedResult<Venda>;
