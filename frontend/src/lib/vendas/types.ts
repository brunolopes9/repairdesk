import type { PagedResult } from '../clientes/types';

export const VENDA_STATUS = {
  Pendente: 0,
  Paga: 1,
  Cancelada: 2,
} as const;

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
}

export interface CreateVendaItemRequest {
  partId: string | null;
  descricao: string | null;
  quantidade: number;
  precoUnitarioCents: number;
  descontoCents: number;
  ivaRate: number;
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
