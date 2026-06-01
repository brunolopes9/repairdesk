import { api } from '../api';
import type { ClienteCampanhaSegmento, ClienteTag } from '../clientes/types';

export const clienteTagsApi = {
  list() {
    return api.get<ClienteTag[]>('/cliente-tags').then((r) => r.data);
  },
  create(payload: { nome: string; corHex?: string }) {
    return api.post<ClienteTag>('/cliente-tags', payload).then((r) => r.data);
  },
  update(id: string, payload: { nome: string; corHex?: string }) {
    return api.put<ClienteTag>(`/cliente-tags/${id}`, payload).then((r) => r.data);
  },
  delete(id: string) {
    return api.delete<void>(`/cliente-tags/${id}`).then((r) => r.data);
  },
  segmento(tagIds: string[]) {
    return api
      .get<ClienteCampanhaSegmento>('/cliente-tags/segmento', { params: { tagIds: tagIds.join(',') } })
      .then((r) => r.data);
  },
  setForCliente(clienteId: string, tagIds: string[]) {
    return api.put<ClienteTag[]>(`/clientes/${clienteId}/tags`, { tagIds }).then((r) => r.data);
  },
};
