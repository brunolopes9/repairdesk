import { api } from '../api';

export interface Fornecedor {
  id: string;
  name: string;
  code: string | null;
  email: string | null;
  rmaEmail: string | null;
  phone: string | null;
  website: string | null;
  garantiaB2BDiasDefault: number | null;
  notas: string | null;
  active: boolean;
  createdAt: string;
  intraUe: boolean;
}

export interface FornecedorWriteRequest {
  name: string;
  email?: string | null;
  rmaEmail?: string | null;
  phone?: string | null;
  website?: string | null;
  garantiaB2BDiasDefault?: number | null;
  notas?: string | null;
  active: boolean;
  intraUe?: boolean;
}

/** Sprint 548 (Doc 93 #3): histórico consolidado de um fornecedor. */
export interface FornecedorHistorico {
  id: string;
  nome: string;
  intraUe: boolean;
  defaultImportAction: string;
  defaultDespesaCategoria: number | null;
  garantiaB2BDiasDefault: number | null;
  comprasStockCents: number;
  despesasCents: number;
  importsTotal: number;
  importsPendentes: number;
  ultimaCompraEm: string | null;
  itensVendidos12m: number;
  itensComReparacao12m: number;
  taxaDefeitoPct12m: number;
  ultimasFaturas: {
    importId: string;
    numero: string | null;
    data: string | null;
    totalCents: number | null;
    status: string;
  }[];
}

export const fornecedoresApi = {
  list(includeInactive = false) {
    return api.get<Fornecedor[]>('/fornecedores', { params: { includeInactive } }).then((r) => r.data);
  },
  historico(id: string) {
    return api.get<FornecedorHistorico>(`/fornecedores/${id}/historico`).then((r) => r.data);
  },
  create(req: FornecedorWriteRequest) {
    return api.post<Fornecedor>('/fornecedores', req).then((r) => r.data);
  },
  update(id: string, req: FornecedorWriteRequest) {
    return api.put<Fornecedor>(`/fornecedores/${id}`, req).then((r) => r.data);
  },
  remove(id: string) {
    return api.delete(`/fornecedores/${id}`).then(() => undefined);
  },
};
