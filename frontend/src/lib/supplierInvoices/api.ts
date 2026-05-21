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

// Sprint 160b: aprovar items como Stock (cria Parts + PartMovimento).
export type ApproveAsStockAction = 'existing' | 'new' | 'skip';

export interface ApproveAsStockItem {
  description: string;
  quantity: number;
  unitCostCents: number;
  action: ApproveAsStockAction;
  existingPartId?: string | null;
  newSku?: string | null;
  newName?: string | null;
  newMarca?: string | null;
  newModelo?: string | null;
  supplierSku?: string | null;
}

export interface ApproveAsStockRequest {
  items: ApproveAsStockItem[];
}

export const supplierInvoicesApi = {
  pending(take = 100) {
    return api.get<SupplierInvoiceImport[]>(`/supplier-invoices/pending?take=${take}`).then((r) => r.data);
  },
  /** Devolve URL absoluto para abrir PDF directamente (relative para api.get). */
  pdfPath(id: string) {
    return `/supplier-invoices/${id}/pdf`;
  },
  // Sprint 160c: upload manual PDF (sem n8n IMAP). Aceita File do browser.
  uploadPdf(file: File, fornecedorHint?: string) {
    const form = new FormData();
    form.append('file', file);
    if (fornecedorHint) form.append('fornecedorHint', fornecedorHint);
    return api.post('/supplier-invoices/upload', form, {
      headers: { 'Content-Type': 'multipart/form-data' },
    }).then((r) => r.data);
  },
  approve(id: string, req: ApproveSupplierInvoiceRequest) {
    return api.post<SupplierInvoiceImport>(`/supplier-invoices/${id}/approve`, req).then((r) => r.data);
  },
  // Sprint 160b: aprovar como stock — cria/incrementa Parts + PartMovimentos + SkuMapping.
  approveAsStock(id: string, req: ApproveAsStockRequest) {
    return api.post<SupplierInvoiceImport>(`/supplier-invoices/${id}/approve-stock`, req).then((r) => r.data);
  },
  reject(id: string, reason: string | null) {
    return api.post<SupplierInvoiceImport>(`/supplier-invoices/${id}/reject`, { reason }).then((r) => r.data);
  },
  exportZipPath(from: string, to: string) {
    return `/supplier-invoices/export?from=${encodeURIComponent(from)}&to=${encodeURIComponent(to)}`;
  },
};
