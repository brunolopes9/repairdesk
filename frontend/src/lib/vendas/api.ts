import { api } from '../api';
import type { PaymentMethod, PecaEncomenda, PecaEncomendaEstado, Venda, VendaEstado, VendaFaturaImport, VendaFiltro, VendasPage, VendaWrite } from './types';

export const vendasApi = {
  list(params: VendaFiltro = {}) {
    return api.get<VendasPage>('/vendas', { params }).then((r) => r.data);
  },
  get(id: string) {
    return api.get<Venda>(`/vendas/${id}`).then((r) => r.data);
  },
  /** Sprint 594: peças em falta nas reparações em curso. */
  pecasEncomenda() {
    return api.get<PecaEncomenda[]>('/vendas/pecas-encomenda').then((r) => r.data);
  },
  marcarEncomenda(vendaId: string, itemId: string, encomenda: PecaEncomendaEstado, nota?: string | null) {
    return api.post<Venda>(`/vendas/${vendaId}/itens/${itemId}/encomenda`, { encomenda, nota }).then((r) => r.data);
  },
  create(payload: VendaWrite) {
    return api.post<Venda>('/vendas', payload).then((r) => r.data);
  },
  update(id: string, payload: VendaWrite) {
    return api.put<Venda>(`/vendas/${id}`, payload).then((r) => r.data);
  },
  mudarEstado(id: string, estado: VendaEstado, paymentMethod?: PaymentMethod, porReceber = false) {
    return api.post<Venda>(`/vendas/${id}/estado`, { estado, paymentMethod, porReceber }).then((r) => r.data);
  },
  /** Sprint 650: venda entregue paga (false) ou ainda por receber (true). */
  pagamento(id: string, porReceber: boolean) {
    return api.put<Venda>(`/vendas/${id}/pagamento`, { porReceber }).then((r) => r.data);
  },
  /** Sprint 616: marca/desmarca a venda como «sem fatura» (registo interno, fora do IVA do Mender). */
  foraApuramento(id: string, ativo: boolean, motivo?: string | null) {
    return api.put<Venda>(`/vendas/${id}/fora-apuramento`, { ativo, motivo: motivo ?? null }).then((r) => r.data);
  },
  registarFatura(id: string, invoiceNumber: string | null, invoiceEmittedAt: string | null) {
    return api.put<Venda>(`/vendas/${id}/fatura`, { invoiceNumber, invoiceEmittedAt }).then((r) => r.data);
  },
  /** Sprint 565: importa o PDF de uma fatura emitida no Moloni (cria a venda ou anexa o PDF). */
  importarFatura(file: File) {
    const form = new FormData();
    form.append('file', file);
    return api.post<VendaFaturaImport>('/vendas/importar-fatura', form, {
      headers: { 'Content-Type': 'multipart/form-data' },
      timeout: 60_000,
    }).then((r) => r.data);
  },
  faturaPdfPath(id: string) {
    return `/vendas/${id}/fatura.pdf`;
  },
  exportFaturasPath(from: string, to: string) {
    return `/vendas/faturas/export?from=${encodeURIComponent(from)}&to=${encodeURIComponent(to)}`;
  },
  /** Sprint 569: talão de 80 mm (impressora de talões). */
  talaoPath(id: string) {
    return `/vendas/${id}/talao.pdf`;
  },
  reciboUrl(id: string) {
    return `${api.defaults.baseURL ?? ''}/vendas/${id}/recibo.pdf`;
  },
  listAssinaturas(id: string) {
    return api.get<AssinaturaVenda[]>(`/vendas/${id}/assinaturas`).then((r) => r.data);
  },
  saveAssinatura(id: string, tipo: 'entrada' | 'entrega' | 'tecnico', dataUrl: string) {
    return api.post<AssinaturaVenda>(`/vendas/${id}/assinaturas`, { tipo, dataUrl }).then((r) => r.data);
  },
  /** PDFs da venda (path relativo ao baseURL, para openPdfInNewTab). */
  pdfPath(id: string, doc: 'orcamento' | 'entrada' | 'entrega' | 'label') {
    return `/vendas/${id}/${doc}.pdf`;
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
};

export interface VendaImeiLookup {
  vendaId: string;
  numero: number;
  data: string;
  descricao: string;
  clienteNome: string | null;
}

export interface AssinaturaVenda {
  tipo: 'entrada' | 'entrega' | 'tecnico';
  assinadaEm: string;
}
