import { api } from '../api';

export interface KitItemDto {
  partId: string;
  partNome: string;
  partSku: string | null;
  quantidade: number;
  custoUnitarioCents: number;
}

export interface KitDto {
  id: string;
  nome: string;
  descricao: string | null;
  items: KitItemDto[];
  custoTotalCents: number;
}

export interface KitItemInput {
  partId: string;
  quantidade: number;
}

export interface AppliedItemDto {
  partId: string;
  partNome: string;
  quantidade: number;
}

export interface ApplyKitResult {
  applied: AppliedItemDto[];
  failedAt: string | null;
}

export const partKitsApi = {
  list() {
    return api.get<KitDto[]>('/part-kits').then((r) => r.data);
  },
  get(id: string) {
    return api.get<KitDto>(`/part-kits/${id}`).then((r) => r.data);
  },
  create(payload: { nome: string; descricao?: string | null; items: KitItemInput[] }) {
    return api.post<KitDto>('/part-kits', payload).then((r) => r.data);
  },
  update(id: string, payload: { nome: string; descricao?: string | null; items: KitItemInput[] }) {
    return api.put<KitDto>(`/part-kits/${id}`, payload).then((r) => r.data);
  },
  delete(id: string) {
    return api.delete<void>(`/part-kits/${id}`).then((r) => r.data);
  },
  apply(kitId: string, reparacaoId: string) {
    return api.post<ApplyKitResult>(`/part-kits/${kitId}/apply`, { reparacaoId }).then((r) => r.data);
  },
};
