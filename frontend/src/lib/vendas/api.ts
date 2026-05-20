import { api } from '../api';
import type { CreateVendaRequest, EmitVendaFaturaResponse, PaymentMethod, Venda, VendasPage } from './types';

export const vendasApi = {
  list(params: { from?: string; to?: string; clienteId?: string; page?: number; pageSize?: number } = {}) {
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
  imeiLookup(imei: string) {
    return api
      .get<VendaImeiLookup>(`/vendas/imei-lookup/${encodeURIComponent(imei)}`)
      .then((r) => r.data)
      .catch((err) => {
        if (err?.response?.status === 404) return null;
        throw err;
      });
  },
  reparacoesRelacionadas(vendaId: string) {
    return api
      .get<VendaReparacaoRelacionada[]>(`/vendas/${vendaId}/reparacoes-relacionadas`)
      .then((r) => r.data);
  },
  /** Para autocomplete de fornecedor no formulário de criar venda. */
  fornecedores() {
    return api.get<string[]>('/vendas/fornecedores').then((r) => r.data);
  },
};

export interface VendaImeiLookup {
  vendaId: string;
  numero: number;
  data: string;
  descricao: string;
  clienteNome: string | null;
}

export interface VendaReparacaoRelacionada {
  reparacaoId: string;
  reparacaoNumero: number;
  recebidoEm: string;
  equipamento: string;
  imei: string;
  /** RepairStatus enum: 0=Recebido, 1=Diagnostico, ..., 5=Entregue, 6=Cancelado */
  estado: number;
  diasDesdeAVenda: number;
  orcamentoCents: number | null;
}
