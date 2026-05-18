import { api } from '../api';
import type { AtNifLookup, Cliente, ClienteForm, PagedResult } from './types';

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
  exportRgpd(id: string) {
    return api.get<Blob>(`/clientes/${id}/exportar`, { responseType: 'blob' }).then((r) => r.data);
  },
  hardDelete(id: string, confirm: string, motivo?: string | null) {
    return api.delete<HardDeleteClienteResponse>(`/clientes/${id}/hard-delete`, {
      data: { confirm, motivo: motivo || null },
    }).then((r) => r.data);
  },
  lookupAtNif(nif: string, signal?: AbortSignal) {
    return api.get<AtNifLookup>(`/at/nif-lookup/${nif}`, { signal }).then((r) => r.data);
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

export interface HardDeleteClienteResponse {
  clienteId: string;
  nome: string;
  deletedAt: string;
  reparacoes: number;
  trabalhos: number;
  despesas: number;
  fotos: number;
}
