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
  list(filters: { q?: string; estado?: RepairStatus | null; clienteId?: string; page?: number; pageSize?: number } = {}) {
    return api
      .get<ReparacoesPage>('/reparacoes', {
        params: {
          q: filters.q || undefined,
          estado: filters.estado ?? undefined,
          clienteId: filters.clienteId || undefined,
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
  emitirFatura(id: string, payload: { vatPercent?: number | null; paymentMethod?: string | null } = {}) {
    return api.post<InvoiceDto>(`/reparacoes/${id}/emitir-fatura`, payload).then((r) => r.data);
  },
  anularFatura(id: string) {
    return api.post<Reparacao>(`/reparacoes/${id}/anular-fatura`).then((r) => r.data);
  },
  emitirOrcamentoMoloni(id: string) {
    return api.post<Reparacao>(`/reparacoes/${id}/emitir-orcamento-moloni`).then((r) => r.data);
  },
  // Sprint 143: re-emite orçamento Moloni quando preço/items mudaram (best-effort cancel velho).
  reemitirOrcamentoMoloni(id: string) {
    return api.post<Reparacao>(`/reparacoes/${id}/reemitir-orcamento-moloni`).then((r) => r.data);
  },
  converterOrcamentoEmFatura(id: string) {
    return api.post<Reparacao>(`/reparacoes/${id}/converter-orcamento-fatura`).then((r) => r.data);
  },
  bulkEmitFaturas(ids: string[]) {
    return api
      .post<Array<{ id: string; success: boolean; invoiceNumber: string | null; errorMessage: string | null }>>(
        '/reparacoes/bulk-emit-faturas',
        { ids },
      )
      .then((r) => r.data);
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
};

export interface InvoiceDto {
  number: string;
  pdfUrl: string | null;
  emittedAt: string;
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
