import type { PagedResult } from '../clientes/types';

export const VENDA_STATUS = {
  Pendente: 0,
  Paga: 1,
  Cancelada: 2,
} as const;

export const VENDA_ORIGEM = {
  Balcao: 0,
  Online: 1,
  Importacao: 2,
} as const;

export type VendaOrigem = (typeof VENDA_ORIGEM)[keyof typeof VENDA_ORIGEM];

export const VENDA_ORIGEM_LABEL: Record<VendaOrigem, string> = {
  0: 'Balcão',
  1: 'Online',
  2: 'Importada',
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
export type VendaStatus = (typeof VENDA_STATUS)[keyof typeof VENDA_STATUS];

export interface VendaClienteResumo {
  id: string;
  nome: string;
  telefone: string;
}

export const CONDICAO_ARTIGO = {
  NaoAplicavel: 0,
  Novo: 1,
  OpenBox: 2,
  Recondicionado: 3,
  Usado: 4,
} as const;

export type CondicaoArtigo = (typeof CONDICAO_ARTIGO)[keyof typeof CONDICAO_ARTIGO];

export const CONDICAO_ARTIGO_LABEL: Record<CondicaoArtigo, string> = {
  0: '—',
  1: 'Novo',
  2: 'Open-box',
  3: 'Recondicionado',
  4: 'Usado',
};

export interface VendaItem {
  id: string;
  partId: string | null;
  partSku: string | null;
  descricao: string;
  quantidade: number;
  precoUnitarioCents: number;
  descontoCents: number;
  ivaRate: number;
  totalCents: number;
  ivaCents: number;
  imei: string | null;
  imei2: string | null;
  fornecedorNome: string | null;
  condicao: CondicaoArtigo;
  /** ISO date — até quando o fornecedor cobre garantia B2B (snapshot na venda). */
  garantiaFornecedorAteAo: string | null;
}

export interface Venda {
  id: string;
  numero: number;
  data: string;
  cliente: VendaClienteResumo | null;
  totalCents: number;
  ivaCents: number;
  paymentMethod: PaymentMethod;
  status: VendaStatus;
  invoiceProvider: number;
  invoiceExternalId: string | null;
  invoicePdfUrl: string | null;
  invoiceNumber: string | null;
  invoiceEmittedAt: string | null;
  notas: string | null;
  items: VendaItem[];
  origem: VendaOrigem;
}

export interface CreateVendaItemRequest {
  partId: string | null;
  descricao: string | null;
  quantidade: number;
  precoUnitarioCents: number;
  descontoCents: number;
  ivaRate: number;
  imei?: string | null;
  imei2?: string | null;
  fornecedorNome?: string | null;
  condicao?: CondicaoArtigo | null;
  /** ISO date (YYYY-MM-DD ou ISO completo) — opcional. */
  garantiaFornecedorAteAo?: string | null;
}

export interface CreateVendaRequest {
  clienteId: string | null;
  items: CreateVendaItemRequest[];
  notas: string | null;
}

export interface EmitVendaFaturaResponse {
  venda: Venda;
  invoice: { number: string; pdfUrl: string | null; emittedAt: string } | null;
}

export type VendasPage = PagedResult<Venda>;
