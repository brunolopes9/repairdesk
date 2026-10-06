import { api } from '../api';
import type {
  CreateReparacaoForm,
  Reparacao,
  ReparacaoDetalhada,
  ReparacoesPage,
  RepairStatus,
  UpdateReparacaoForm,
} from './types';
import type { EquipmentFieldValue, SetEquipmentFieldValue } from '../equipmentFields/types';

export const reparacoesApi = {
  list(filters: { q?: string; estado?: RepairStatus | null; clienteId?: string; categoria?: number | null; page?: number; pageSize?: number } = {}) {
    return api
      .get<ReparacoesPage>('/reparacoes', {
        params: {
          q: filters.q || undefined,
          estado: filters.estado ?? undefined,
          clienteId: filters.clienteId || undefined,
          categoria: filters.categoria ?? undefined, // Sprint 477: DeviceCategory filter
          page: filters.page ?? 1,
          pageSize: filters.pageSize ?? 20,
        },
      })
      .then((r) => r.data);
  },
  listPagasSemFatura(limit: number = 100) {
    return api.get<Reparacao[]>('/reparacoes/pagas-sem-fatura', { params: { limit } }).then((r) => r.data);
  },
  get(id: string) {
    return api.get<ReparacaoDetalhada>(`/reparacoes/${id}`).then((r) => r.data);
  },
  create(form: CreateReparacaoForm) {
    return api.post<Reparacao>('/reparacoes', form).then((r) => r.data);
  },
  update(id: string, form: UpdateReparacaoForm) {
    return api.put<Reparacao>(`/reparacoes/${id}`, form).then((r) => r.data);
  },
  changeEstado(id: string, estado: RepairStatus, notas?: string) {
    return api.post<Reparacao>(`/reparacoes/${id}/estado`, { estado, notas: notas ?? null }).then((r) => r.data);
  },
  /** Sprint 343: atribui (userId) ou desatribui (null) técnico responsável. Admin only. */
  assign(id: string, userId: string | null) {
    return api.put<Reparacao>(`/reparacoes/${id}/assign`, { userId }).then((r) => r.data);
  },
  setFields(id: string, templateId: string | null, values: SetEquipmentFieldValue[]) {
    return api.post<EquipmentFieldValue[]>(`/reparacoes/${id}/fields`, { templateId, values }).then((r) => r.data);
  },
  reabrir(id: string, notas?: string) {
    return api.post<Reparacao>(`/reparacoes/${id}/reabrir`, { notas: notas ?? null }).then((r) => r.data);
  },
  historicoImei(imei: string, excludeId?: string) {
    return api
      .get<HistoricoImeiResponse>('/reparacoes/historico-imei', { params: { imei, excludeId: excludeId || undefined } })
      .then((r) => r.data);
  },
  importCsv(csv: string) {
    return api.post<ImportReparacoesResponse>('/reparacoes/import', { csv }).then((r) => r.data);
  },
  remove(id: string) {
    return api.delete(`/reparacoes/${id}`).then(() => undefined);
  },
  // Sprint 551: assinatura do cliente (canvas) na entrada/entrega — fica estampada nos PDFs.
  listAssinaturas(id: string) {
    return api.get<AssinaturaInfo[]>(`/reparacoes/${id}/assinaturas`).then((r) => r.data);
  },
  saveAssinatura(id: string, tipo: 'entrada' | 'entrega', dataUrl: string) {
    return api.post<AssinaturaInfo>(`/reparacoes/${id}/assinaturas`, { tipo, dataUrl }).then((r) => r.data);
  },
};

/** Sprint 551: estado de uma assinatura recolhida (sem os bytes). */
export interface AssinaturaInfo {
  tipo: string; // "entrada" | "entrega"
  assinadaEm: string;
}

export interface HistoricoImeiItem {
  id: string;
  numero: number;
  equipamento: string;
  imei: string | null;
  cliente: { id: string; nome: string; telefone: string };
  estado: RepairStatus;
  recebidoEm: string;
  entregueEm: string | null;
  precoFinalCents: number | null;
  diagnostico: string | null;
}

export interface HistoricoImeiResponse {
  imei: string;
  luhnValido: boolean;
  total: number;
  items: HistoricoImeiItem[];
}

export interface ImportReparacaoError {
  linha: number;
  campo: string;
  mensagem: string;
  valorOriginal: string | null;
}

export interface ImportReparacoesResponse {
  totalLinhas: number;
  criadas: number;
  clientesCriados: number;
  clientesReutilizados: number;
  comErro: number;
  erros: ImportReparacaoError[];
}
