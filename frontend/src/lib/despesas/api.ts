import { api } from '../api';
import type { CreateDespesaForm, Despesa, DespesaCategoria, DespesasPage } from './types';

export interface UpdateDespesaForm extends CreateDespesaForm {}

interface ListFilters {
  q?: string;
  categoria?: DespesaCategoria | null;
  from?: string;
  to?: string;
  trabalhoId?: string;
  reparacaoId?: string;
  page?: number;
  pageSize?: number;
}

export const despesasApi = {
  list(filters: ListFilters = {}) {
    return api
      .get<DespesasPage>('/despesas', {
        params: {
          q: filters.q || undefined,
          categoria: filters.categoria ?? undefined,
          from: filters.from || undefined,
          to: filters.to || undefined,
          trabalhoId: filters.trabalhoId || undefined,
          reparacaoId: filters.reparacaoId || undefined,
          page: filters.page ?? 1,
          pageSize: filters.pageSize ?? 20,
        },
      })
      .then((r) => r.data);
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
