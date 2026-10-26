import { api } from '../api';
import type { PaymentMethod, Venda } from '../vendas/types';

/** Sprint 567: ligação ao Moloni ON — os documentos fiscais emitem-se lá a partir do Mender. */
export interface MoloniSerie {
  id: number;
  nome: string;
  predefinida: boolean;
}

export interface MoloniEstado {
  configurado: boolean;
  companyId: number | null;
  series: MoloniSerie[];
  serieServico: number | null;
  serieReparacao: number | null;
  serieProduto: number | null;
}

export type MoloniDocumento = 'Fatura' | 'FaturaRecibo';

/** Motivos de isenção mais usados (código AT). Linhas a 0% exigem um. */
export const MOTIVOS_ISENCAO = [
  { codigo: 'M40', label: 'M40 — Autoliquidação (cliente estrangeiro, serviços)' },
  { codigo: 'M16', label: 'M16 — Art. 14.º do RITI (entrega intracomunitária)' },
  { codigo: 'M05', label: 'M05 — Art. 14.º do CIVA (exportação)' },
  { codigo: 'M07', label: 'M07 — Art. 9.º do CIVA (isenção)' },
  { codigo: 'M99', label: 'M99 — Não sujeito / não tributado' },
] as const;

export type MoloniTipoDocumento = 'Fatura' | 'FaturaRecibo' | 'Recibo' | 'Orcamento';

export interface MoloniDocumentoLista {
  documentId: number;
  tipo: MoloniTipoDocumento;
  numero: string;
  data: string | null;
  cliente: string | null;
  nif: string | null;
  total: number;
  rascunho: boolean;
  anulado: boolean;
  pagoPercentagem: number | null;
  vendaId: string | null;
  vendaNumero: number | null;
}

export interface MoloniPagina<T> {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
}

export interface MoloniProduto {
  productId: number;
  referencia: string;
  nome: string;
  /** 1 produto · 2 serviço · 3 outro */
  tipo: number;
  preco: number;
  precoComIva: number;
  taxa: number;
  motivoIsencao: string | null;
  categoriaId: number | null;
  categoria: string | null;
  unidadeId: number | null;
  unidade: string | null;
  podeApagar: boolean;
}

export interface MoloniProdutoWrite {
  nome: string;
  referencia: string;
  tipo: number;
  precoComIva: number;
  taxa: number;
  motivoIsencao: string | null;
  categoriaId: number | null;
  unidadeId: number | null;
}

export interface MoloniReferencias {
  categorias: { id: number; nome: string }[];
  unidades: { id: number; nome: string }[];
  taxas: number[];
}

export const moloniApi = {
  documentos(tipo: MoloniTipoDocumento, q: string, page: number) {
    return api.get<MoloniPagina<MoloniDocumentoLista>>('/moloni/documentos', { params: { tipo, q: q || undefined, page, pageSize: 25 }, timeout: 30_000 }).then((r) => r.data);
  },
  documentoPdfPath(tipo: MoloniTipoDocumento, documentId: number) {
    return `/moloni/documentos/${tipo}/${documentId}.pdf`;
  },
  reciboDeFatura(documentId: number, paymentMethod: PaymentMethod) {
    return api.post(`/moloni/documentos/Fatura/${documentId}/recibo`, { paymentMethod }, { timeout: 60_000 });
  },
  anularDocumento(tipo: MoloniTipoDocumento, documentId: number, motivo: string) {
    return api.post(`/moloni/documentos/${tipo}/${documentId}/anular`, { motivo }, { timeout: 60_000 });
  },
  artigos(q: string, page: number) {
    return api.get<MoloniPagina<MoloniProduto>>('/moloni/artigos', { params: { q: q || undefined, page, pageSize: 25 }, timeout: 30_000 }).then((r) => r.data);
  },
  referencias() {
    return api.get<MoloniReferencias>('/moloni/referencias').then((r) => r.data);
  },
  criarArtigo(req: MoloniProdutoWrite) {
    return api.post<MoloniProduto>('/moloni/artigos', req).then((r) => r.data);
  },
  atualizarArtigo(id: number, req: MoloniProdutoWrite) {
    return api.put<MoloniProduto>(`/moloni/artigos/${id}`, req).then((r) => r.data);
  },
  apagarArtigo(id: number) {
    return api.delete(`/moloni/artigos/${id}`);
  },
  importarClientes() {
    return api.post<{ ligados: number; criados: number; jaLigados: number; total: number }>('/moloni/clientes/importar', {}, { timeout: 120_000 }).then((r) => r.data);
  },
  estado() {
    return api.get<MoloniEstado>('/moloni/estado').then((r) => r.data);
  },
  configurar(req: { apiKey: string | null; serieServico?: number | null; serieReparacao?: number | null; serieProduto?: number | null }) {
    return api.put<MoloniEstado>('/moloni', req, { timeout: 60_000 }).then((r) => r.data);
  },
  remover() {
    return api.delete('/moloni');
  },
  emitir(vendaId: string, req: { tipo: MoloniDocumento; serieId?: number | null; paymentMethod?: PaymentMethod | null; vencimentoDias?: number; motivoIsencao?: string | null }) {
    return api.post<Venda>(`/vendas/${vendaId}/moloni/emitir`, req, { timeout: 90_000 }).then((r) => r.data);
  },
  recibo(vendaId: string, paymentMethod: PaymentMethod | null) {
    return api.post<Venda>(`/vendas/${vendaId}/moloni/recibo`, { paymentMethod }, { timeout: 60_000 }).then((r) => r.data);
  },
  anular(vendaId: string, motivo: string) {
    return api.post<Venda>(`/vendas/${vendaId}/moloni/anular`, { motivo }, { timeout: 60_000 }).then((r) => r.data);
  },
  orcamento(vendaId: string, req: { serieId?: number | null; validadeDias?: number; motivoIsencao?: string | null }) {
    return api.post<Venda>(`/vendas/${vendaId}/moloni/orcamento`, req, { timeout: 90_000 }).then((r) => r.data);
  },
  /** Para openPdfInNewTab: documento = fatura | recibo | orcamento (pedido ao Moloni na hora). */
  pdfPath(vendaId: string, documento: 'fatura' | 'recibo' | 'orcamento') {
    return `/vendas/${vendaId}/moloni/${documento}.pdf`;
  },
};
