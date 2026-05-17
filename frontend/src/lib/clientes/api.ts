import { api } from '../api';
import type { Cliente, ClienteForm, PagedResult } from './types';

export const clientesApi = {
  list(query: string, page = 1, pageSize = 20) {
    return api
      .get<PagedResult<Cliente>>('/clientes', { params: { q: query || undefined, page, pageSize } })
      .then((r) => r.data);
  },
  get(id: string) {
    return api.get<Cliente>(`/clientes/${id}`).then((r) => r.data);
  },
  create(payload: ClienteForm) {
    return api.post<Cliente>('/clientes', payload).then((r) => r.data);
  },
  update(id: string, payload: ClienteForm) {
    return api.put<Cliente>(`/clientes/${id}`, payload).then((r) => r.data);
  },
  remove(id: string) {
    return api.delete(`/clientes/${id}`).then(() => undefined);
  },
  importCsv(csv: string) {
    return api.post<ImportClientesResponse>('/clientes/import', { csv }).then((r) => r.data);
  },
};

export interface ImportError {
  linha: number;
  campo: string;
  mensagem: string;
  valorOriginal: string | null;
}

export interface ImportClientesResponse {
  totalLinhas: number;
  criados: number;
  ignorados: number;
  comErro: number;
  clientesCriados: Cliente[];
  erros: ImportError[];
}
