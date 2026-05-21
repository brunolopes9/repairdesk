import { api } from '../api';
import type { DespesaCategoria } from '../despesas/types';

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
