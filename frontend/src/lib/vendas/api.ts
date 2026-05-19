import { api } from '../api';
import type { CreateVendaRequest, EmitVendaFaturaResponse, PaymentMethod, Venda, VendasPage } from './types';

export const vendasApi = {
  list(params: { from?: string; to?: string; page?: number; pageSize?: number } = {}) {
    return api.get<VendasPage>('/vendas', { params }).then((r) => r.data);
  },
  get(id: string) {
    return api.get<Venda>(`/vendas/${id}`).then((r) => r.data);
  },
  create(payload: CreateVendaRequest) {
    return api.post<Venda>('/vendas', payload).then((r) => r.data);
  },
  marcarPaga(id: string, paymentMethod: PaymentMethod, emitirFatura = false) {
    return api.post<EmitVendaFaturaResponse>(`/vendas/${id}/marcar-paga`, { paymentMethod, emitirFatura }).then((r) => r.data);
  },
  emitirFatura(id: string) {
    return api.post<{ number: string; pdfUrl: string | null; emittedAt: string }>(`/vendas/${id}/emitir-fatura`).then((r) => r.data);
  },
  cancelar(id: string) {
    return api.post<Venda>(`/vendas/${id}/cancelar`).then((r) => r.data);
  },
  anularFatura(id: string) {
    return api.post<Venda>(`/vendas/${id}/anular-fatura`).then((r) => r.data);
  },
  limparFaturaLocal(id: string) {
    return api.post<Venda>(`/vendas/${id}/limpar-fatura-local`).then((r) => r.data);
  },
  reciboUrl(id: string) {
    return `${api.defaults.baseURL ?? ''}/vendas/${id}/recibo.pdf`;
  },
};
