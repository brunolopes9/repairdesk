import type { PaymentMethod } from '../vendas/types';

/**
 * Sprint 303: provider de pagamento. Distinto de PaymentMethod — Method é "MBWay",
 * Provider é "Mock/Ifthenpay" (quem executa). Manual = sem provider externo (default).
 */
export const PAYMENT_PROVIDER = {
  Manual: 0,
  Mock: 1,
  Ifthenpay: 2,
} as const;

export type PaymentProvider = (typeof PAYMENT_PROVIDER)[keyof typeof PAYMENT_PROVIDER];

export const PAYMENT_STATUS = {
  NaoPago: 0,
  PagoParcial: 1,
  Pago: 2,
  Anulado: 3,
} as const;

export type PaymentStatus = (typeof PAYMENT_STATUS)[keyof typeof PAYMENT_STATUS];

export interface PaymentDto {
  id: string;
  vendaId: string;
  method: PaymentMethod;
  provider: PaymentProvider;
  amountCents: number;
  status: PaymentStatus;
  providerRef: string | null;
  externalId: string | null;
  createdAt: string;
  confirmedAt: string | null;
  expiresAt: string | null;
  failureReason: string | null;
  /**
   * Decoded from Payment.MetadataJson — contém entidade/referencia (MB) ou
   * mbWayRequestId+phone (MBWay). Lido client-side, não vem como campo distinto.
   */
  metadata?: Record<string, unknown>;
}

export interface InitiatePaymentRequest {
  vendaId: string;
  method: PaymentMethod;
  provider: PaymentProvider;
  amountCents: number;
  customerPhone?: string;
  customerEmail?: string;
  description?: string;
}
