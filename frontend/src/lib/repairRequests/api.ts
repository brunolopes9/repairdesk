import { api } from '../api';

export const REPAIR_REQUEST_ESTADO = {
  Pendente: 0,
  Convertido: 1,
  Rejeitado: 2,
} as const;

export type RepairRequestEstado = (typeof REPAIR_REQUEST_ESTADO)[keyof typeof REPAIR_REQUEST_ESTADO];

export interface RepairRequestDto {
  id: string;
  nome: string;
  email: string | null;
  telefone: string | null;
  equipamento: string;
  descricao: string;
  estado: RepairRequestEstado;
  reparacaoId: string | null;
  motivoRejeicao: string | null;
  createdAt: string;
}

export const repairRequestsApi = {
  list(estado?: RepairRequestEstado) {
    const q = estado != null ? `?estado=${estado}` : '';
    return api.get<RepairRequestDto[]>(`/repair-requests${q}`).then((r) => r.data);
  },
  countPendentes() {
    return api.get<number>('/repair-requests/count-pendentes').then((r) => r.data);
  },
  converter(id: string) {
    return api.post<RepairRequestDto>(`/repair-requests/${id}/converter`, {}).then((r) => r.data);
  },
  rejeitar(id: string, motivo?: string) {
    return api.post<RepairRequestDto>(`/repair-requests/${id}/rejeitar`, { motivo: motivo ?? null }).then((r) => r.data);
  },
};
