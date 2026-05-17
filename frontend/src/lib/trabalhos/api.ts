import { api } from '../api';
import type {
  CreateTrabalhoForm,
  JobCategory,
  Trabalho,
  TrabalhosPage,
  TrabalhoStatus,
  UpdateTrabalhoForm,
} from './types';

export const trabalhosApi = {
  list(filters: { q?: string; status?: TrabalhoStatus | null; categoria?: JobCategory | null; clienteId?: string; page?: number; pageSize?: number } = {}) {
    return api
      .get<TrabalhosPage>('/trabalhos', {
        params: {
          q: filters.q || undefined,
          status: filters.status ?? undefined,
          categoria: filters.categoria ?? undefined,
          clienteId: filters.clienteId || undefined,
          page: filters.page ?? 1,
          pageSize: filters.pageSize ?? 20,
        },
      })
      .then((r) => r.data);
  },
  get(id: string) {
    return api.get<Trabalho>(`/trabalhos/${id}`).then((r) => r.data);
  },
  create(form: CreateTrabalhoForm) {
    return api.post<Trabalho>('/trabalhos', form).then((r) => r.data);
  },
  update(id: string, form: UpdateTrabalhoForm) {
    return api.put<Trabalho>(`/trabalhos/${id}`, form).then((r) => r.data);
  },
  reabrir(id: string) {
    return api.post<Trabalho>(`/trabalhos/${id}/reabrir`).then((r) => r.data);
  },
  remove(id: string) {
    return api.delete(`/trabalhos/${id}`).then(() => undefined);
  },
};
