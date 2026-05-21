import { api } from '../api';
import type { DespesaCategoria } from '../despesas/types';

export interface SkuMatchSuggestion {
  partId: string;
  partName: string;
  partSku: string;
  /** 0..1 — quanto maior, melhor match. */
  score: number;
  /** "auto" (já mapeado), "fuzzy" (similaridade nome). */
  matchType: 'auto' | 'fuzzy';
}

export interface SupplierInvoiceItem {
  description: string;
  quantity: number;
  lineTotalCents: number;
  brand: string | null;
  model: string | null;
  suggestions: SkuMatchSuggestion[];
}

export interface SupplierInvoiceImport {
  id: string;
  fornecedorId: string | null;
  fornecedorName: string | null;
  documentNumber: string | null;
  documentDate: string | null;
  totalCents: number | null;
  status: 'Pending' | 'Approved' | 'Rejected' | 'Failed';
  parseConfidence: 'None' | 'Low' | 'Medium' | 'High' | null;
  createdAt: string;
  pdfRelativePath: string;
  // Sprint 158: items parseados + sugestões fuzzy.
  items: SupplierInvoiceItem[] | null;
}

export interface ApproveSupplierInvoiceRequest {
  valorCents: number;
  descricao: string;
  categoria: DespesaCategoria;
  data: string | null;
  fornecedor: string | null;
  numeroEncomenda: string | null;
  notas: string | null;
}

export const supplierInvoicesApi = {
  pending(take = 100) {
    return api.get<SupplierInvoiceImport[]>(`/supplier-invoices/pending?take=${take}`).then((r) => r.data);
  },
  /** Devolve URL absoluto para abrir PDF directamente (relative para api.get). */
  pdfPath(id: string) {
    return `/supplier-invoices/${id}/pdf`;
  },
  approve(id: string, req: ApproveSupplierInvoiceRequest) {
    return api.post<SupplierInvoiceImport>(`/supplier-invoices/${id}/approve`, req).then((r) => r.data);
  },
  reject(id: string, reason: string | null) {
    return api.post<SupplierInvoiceImport>(`/supplier-invoices/${id}/reject`, { reason }).then((r) => r.data);
  },
  exportZipPath(from: string, to: string) {
    return `/supplier-invoices/export?from=${encodeURIComponent(from)}&to=${encodeURIComponent(to)}`;
  },
};
