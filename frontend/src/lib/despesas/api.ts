import { api } from '../api';
import type { CreateDespesaForm, Despesa, DespesaCategoria, DespesasPage } from './types';

export interface UpdateDespesaForm extends CreateDespesaForm {}

interface ListFilters {
  q?: string;
  categoria?: DespesaCategoria | null;
  categoriaIn?: readonly DespesaCategoria[];
  includeSupplierInvoiceImports?: boolean;
  excludeSupplierInvoiceImports?: boolean;
  from?: string;
  to?: string;
  isRecorrente?: boolean;
  page?: number;
  pageSize?: number;
}

/** Sprint 574: totais do filtro inteiro (a lista é paginada; os totais não). */
export interface DespesasResumo {
  registos: number;
  totalCents: number;
  ivaCents: number;
  /** Despesas sem IVA indicado (contam no apuramento como "por tratar"). */
  semIva: number;
  recorrentes: number;
  porCategoria: { categoria: DespesaCategoria; registos: number; totalCents: number; ivaCents: number; semIva: number; recorrentes: number }[];
}

function filtroParams(filters: ListFilters) {
  return {
    q: filters.q || undefined,
    categoria: filters.categoria ?? undefined,
    categoria_in: filters.categoriaIn?.length ? filters.categoriaIn.join(',') : undefined,
    include_supplier_invoice_imports: filters.includeSupplierInvoiceImports || undefined,
    exclude_supplier_invoice_imports: filters.excludeSupplierInvoiceImports || undefined,
    from: filters.from || undefined,
    to: filters.to || undefined,
    isRecorrente: filters.isRecorrente ?? undefined,
  };
}

export const despesasApi = {
  list(filters: ListFilters = {}) {
    return api
      .get<DespesasPage>('/despesas', { params: { ...filtroParams(filters), page: filters.page ?? 1, pageSize: filters.pageSize ?? 20 } })
      .then((r) => r.data);
  },
  resumo(filters: ListFilters = {}) {
    return api.get<DespesasResumo>('/despesas/resumo', { params: filtroParams(filters) }).then((r) => r.data);
  },
  get(id: string) {
    return api.get<Despesa>(`/despesas/${id}`).then((r) => r.data);
  },
  create(form: CreateDespesaForm) {
    return api.post<Despesa>('/despesas', form).then((r) => r.data);
  },
  update(id: string, form: UpdateDespesaForm) {
    return api.put<Despesa>(`/despesas/${id}`, form).then((r) => r.data);
  },
  remove(id: string) {
    return api.delete(`/despesas/${id}`).then(() => undefined);
  },
};
