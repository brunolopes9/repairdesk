import { api } from '../api';
import type { InitiatePaymentRequest, PaymentDto } from './types';

export const paymentsApi = {
  initiate(payload: InitiatePaymentRequest) {
    return api.post<PaymentDto>('/payments', payload).then((r) => r.data);
  },
  get(id: string) {
    return api.get<PaymentDto>(`/payments/${id}`).then((r) => r.data);
  },
  listByVenda(vendaId: string) {
    return api.get<PaymentDto[]>(`/payments/by-venda/${vendaId}`).then((r) => r.data);
  },
};
