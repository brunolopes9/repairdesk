import { api } from '../api';
import { PAYMENT_METHOD, type PaymentMethod } from '../vendas/types';

export { PAYMENT_METHOD };
export type { PaymentMethod };

export const DAILY_CLOSING_STATUS = {
  Open: 0,
  Closed: 1,
  Reopened: 2,
} as const;

export const CASH_MOVEMENT_TYPE = {
  PagamentoCliente: 0,
  Reforco: 1,
  Sangria: 2,
  DespesaCaixa: 3,
  Troco: 4,
  AjusteManual: 5,
} as const;

export type DailyClosingStatus = (typeof DAILY_CLOSING_STATUS)[keyof typeof DAILY_CLOSING_STATUS];
export type CashMovementType = (typeof CASH_MOVEMENT_TYPE)[keyof typeof CASH_MOVEMENT_TYPE];

export interface CashMovementDto {
  id: string;
  type: CashMovementType;
  paymentMethod: PaymentMethod;
  amountCents: number;
  descricao: string;
  vendaId: string | null;
  reparacaoId: string | null;
  occurredAt: string;
}

export interface DailyClosingDto {
  id: string;
  date: string;
  status: DailyClosingStatus;
  locationId: string | null;
  openingCents: number;
  expectedClosingCents: number;
  actualClosingCents: number | null;
  diffCents: number | null;
  cashEntriesCents: number;
  cashExitsCents: number;
  mbwayCents: number;
  multibancoCents: number;
  cardCents: number;
  otherCents: number;
  zReportPdfUrl: string | null;
  openedAt: string | null;
  closedAt: string | null;
  notas: string | null;
  movimentos: CashMovementDto[];
}

export interface OpenDayRequest {
  openingCents: number;
  locationId?: string | null;
  notas?: string | null;
}

export interface RecordMovementRequest {
  type: CashMovementType;
  paymentMethod: PaymentMethod;
  amountCents: number;
  descricao: string;
  vendaId?: string | null;
  reparacaoId?: string | null;
  locationId?: string | null;
}

export interface CloseDayRequest {
  actualClosingCents: number;
  notas?: string | null;
}

export const cashApi = {
  today(locationId?: string | null) {
    return api.get<DailyClosingDto | null>('/cash/today', { params: { locationId } }).then((r) => r.data);
  },
  byDate(date: string, locationId?: string | null) {
    return api.get<DailyClosingDto | null>(`/cash/by-date/${encodeURIComponent(date)}`, { params: { locationId } }).then((r) => r.data);
  },
  recent(take: number, locationId?: string | null) {
    return api.get<DailyClosingDto[]>('/cash/recent', { params: { take, locationId } }).then((r) => r.data);
  },
  open(payload: OpenDayRequest) {
    return api.post<DailyClosingDto>('/cash/open', payload).then((r) => r.data);
  },
  recordMovement(payload: RecordMovementRequest) {
    return api.post<CashMovementDto>('/cash/movement', payload).then((r) => r.data);
  },
  close(id: string, payload: CloseDayRequest) {
    return api.post<DailyClosingDto>(`/cash/${id}/close`, payload).then((r) => r.data);
  },
  zReportPdfPath(id: string) {
    return `/cash/${id}/zreport.pdf`;
  },
};
