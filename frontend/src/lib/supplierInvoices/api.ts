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
  suggestedKind: string;
  suggestions: SkuMatchSuggestion[];
  /** Sprint 581: SKU do fornecedor lido na fatura. */
  sku?: string | null;
}

/** Linha de portes: classificação do backend (suggestedKind) ou só o nome da transportadora (LC Phones: «Asfalto»).
 *  Fonte única para a inbox e para o editor de compra. */
const TRANSPORTADORA = /^\s*(asfalto(\s*largo)?|ctt(\s*expresso)?|dhl(\s*express)?|ups|gls|dpd|seur|nacex|mrw|chronopost|correos(\s*express)?|fedex|tnt|paack)\b[\s\w-]{0,25}$/i;
export const ehPortes = (i: { suggestedKind?: string | null; description: string }) =>
  i.suggestedKind === 'Shipping' || TRANSPORTADORA.test(i.description);

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
  /** Sprint 184: regra aprendida do fornecedor (UI usa como default action). */
  fornecedorDefaultAction: 'auto' | 'stock' | 'despesa' | null;
  /** Sprint 543: categoria de Despesa aprendida/conhecida do fornecedor (valor do enum DespesaCategoria). */
  fornecedorDefaultDespesaCategoria: number | null;
  /** Sprint 560: avisos da leitura (totais que não batem, documento de formação…). */
  warnings?: string[] | null;
}

export interface ApproveSupplierInvoiceRequest {
  valorCents: number;
  descricao: string;
  categoria: DespesaCategoria;
  data: string | null;
  fornecedor: string | null;
  numeroEncomenda: string | null;
  notas: string | null;
  /** 25/11: 0 = negócio, 1 = pessoal (fora do IVA/IRS). */
  ambito?: 0 | 1;
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
    return api.post<{ id: string; wasDuplicate: boolean; status: string }>('/supplier-invoices/upload', form, {
      headers: { 'Content-Type': 'multipart/form-data' },
    }).then((r) => r.data);
  },
  // Sprint 164: upload foto papel (JPG/PNG/WebP) — Claude Vision OCR.
  uploadPhoto(file: File, fornecedorHint?: string) {
    const form = new FormData();
    form.append('file', file);
    if (fornecedorHint) form.append('fornecedorHint', fornecedorHint);
    return api.post<{ id: string; wasDuplicate: boolean; status: string }>('/supplier-invoices/upload-photo', form, {
      headers: { 'Content-Type': 'multipart/form-data' },
      timeout: 300_000, // 20/11: a IA (subscrição) lê a foto em 1–3 min
    }).then((r) => r.data);
  },
  // Sprint 163b: histórico (Approved/Rejected).
  history(take = 100) {
    return api.get<SupplierInvoiceImport[]>(`/supplier-invoices/history?take=${take}`).then((r) => r.data);
  },
  // Sprint 163b: re-corre pipeline parser+fingerprint+LLM. Útil quando key Anthropic foi
  // adicionada DEPOIS do ingest original.
  reprocess(id: string) {
    return api.post<SupplierInvoiceImport>(`/supplier-invoices/${id}/reprocess`).then((r) => r.data);
  },
  approve(id: string, req: ApproveSupplierInvoiceRequest) {
    return api.post<SupplierInvoiceImport>(`/supplier-invoices/${id}/approve`, req).then((r) => r.data);
  },
  // Sprint 160b: aprovar como stock — cria/incrementa Parts + PartMovimentos + SkuMapping.
  reject(id: string, reason: string | null) {
    return api.post<SupplierInvoiceImport>(`/supplier-invoices/${id}/reject`, { reason }).then((r) => r.data);
  },
  exportZipPath(from: string, to: string, fornecedorId?: string, tipo: 'Todas' | 'Compras' | 'Despesas' = 'Todas') {
    const forn = fornecedorId ? `&fornecedorId=${encodeURIComponent(fornecedorId)}` : '';
    return `/supplier-invoices/export?from=${encodeURIComponent(from)}&to=${encodeURIComponent(to)}&tipo=${tipo}${forn}`;
  },
};
