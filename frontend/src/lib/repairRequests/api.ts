import { api } from '../api';

export const REPAIR_REQUEST_ESTADO = {
  Pendente: 0,
  Convertido: 1,
  Rejeitado: 2,
} as const;

export type RepairRequestEstado = (typeof REPAIR_REQUEST_ESTADO)[keyof typeof REPAIR_REQUEST_ESTADO];

// Sprint 436 (Doc 91 follow-up): prioridade para triagem da inbox de pedidos.
export const REPAIR_REQUEST_PRIORIDADE = {
  Baixa: 0,
  Normal: 1,
  Alta: 2,
  Urgente: 3,
} as const;

export type RepairRequestPrioridade =
  (typeof REPAIR_REQUEST_PRIORIDADE)[keyof typeof REPAIR_REQUEST_PRIORIDADE];

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
  notasInternas: string | null;
  prioridade: RepairRequestPrioridade;
  trabalhoId: string | null;
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
  converterEmTrabalho(id: string) {
    return api.post<RepairRequestDto>(`/repair-requests/${id}/converter-em-trabalho`, {}).then((r) => r.data);
  },
  rejeitar(id: string, motivo?: string) {
    return api.post<RepairRequestDto>(`/repair-requests/${id}/rejeitar`, { motivo: motivo ?? null }).then((r) => r.data);
  },
  updateTriagem(id: string, body: { notasInternas: string | null; prioridade: RepairRequestPrioridade }) {
    return api.put<RepairRequestDto>(`/repair-requests/${id}/triagem`, body).then((r) => r.data);
  },
};
