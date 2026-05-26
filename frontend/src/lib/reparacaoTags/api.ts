import { api } from '../api';

export interface ReparacaoTagDto {
  id: string;
  nome: string;
  corHex: string;
}

export const reparacaoTagsApi = {
  list() {
    return api.get<ReparacaoTagDto[]>('/reparacao-tags').then((r) => r.data);
  },
  create(payload: { nome: string; corHex?: string }) {
    return api.post<ReparacaoTagDto>('/reparacao-tags', payload).then((r) => r.data);
  },
  update(id: string, payload: { nome: string; corHex?: string }) {
    return api.put<ReparacaoTagDto>(`/reparacao-tags/${id}`, payload).then((r) => r.data);
  },
  delete(id: string) {
    return api.delete<void>(`/reparacao-tags/${id}`).then((r) => r.data);
  },
  setForReparacao(reparacaoId: string, tagIds: string[]) {
    return api.put<ReparacaoTagDto[]>(`/reparacoes/${reparacaoId}/tags`, { tagIds }).then((r) => r.data);
  },
};
