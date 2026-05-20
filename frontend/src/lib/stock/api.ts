import { api } from '../api';
import type {
  CreatePartMovimentoForm,
  ImportPartsResponse,
  Part,
  PartCategoria,
  PartForm,
  PartMovimento,
  PartsPage,
  PartUpdateForm,
} from './types';

export const stockApi = {
  list(filters: { q?: string; categoria?: PartCategoria | null; marca?: string | null; lowStockOnly?: boolean; page?: number; pageSize?: number } = {}) {
    return api
      .get<PartsPage>('/parts', {
        params: {
          q: filters.q || undefined,
          categoria: filters.categoria ?? undefined,
          marca: filters.marca || undefined,
          lowStockOnly: filters.lowStockOnly || undefined,
          page: filters.page ?? 1,
          pageSize: filters.pageSize ?? 50,
        },
      })
      .then((r) => r.data);
  },
  get(id: string) {
    return api.get<Part>(`/parts/${id}`).then((r) => r.data);
  },
  lowStock() {
    return api.get<Part[]>('/parts/low-stock').then((r) => r.data);
  },
  marcas() {
    return api.get<string[]>('/parts/marcas').then((r) => r.data);
  },
  create(payload: PartForm) {
    return api.post<Part>('/parts', payload).then((r) => r.data);
  },
  update(id: string, payload: PartUpdateForm) {
    return api.put<Part>(`/parts/${id}`, payload).then((r) => r.data);
  },
  remove(id: string) {
    return api.delete(`/parts/${id}`).then(() => undefined);
  },
  addMovimento(id: string, payload: CreatePartMovimentoForm) {
    return api.post<PartMovimento>(`/parts/${id}/movimento`, payload).then((r) => r.data);
  },
  movimentos(filters: { partId?: string; reparacaoId?: string } = {}) {
    return api
      .get<PartMovimento[]>('/parts/movimentos', {
        params: {
          partId: filters.partId || undefined,
          reparacaoId: filters.reparacaoId || undefined,
        },
      })
      .then((r) => r.data);
  },
  importCsv(csv: string) {
    return api.post<ImportPartsResponse>('/parts/import', { csv }).then((r) => r.data);
  },
  /** Sprint 119: extrai texto bruto de um PDF (encomenda/fatura fornecedor). */
  extractPdf(file: File) {
    const formData = new FormData();
    formData.append('file', file);
    return api.post<PdfExtractionResult>('/parts/extract-pdf', formData, {
      headers: { 'Content-Type': 'multipart/form-data' },
    }).then((r) => r.data);
  },
};

export interface PdfExtractionResult {
  filename: string;
  text: string;
  pageCount: number;
  pagesRead: number;
  truncated: boolean;
}
