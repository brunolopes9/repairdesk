import { api } from '../api';

/** Doc 94 Fase 3: regime de IVA nas compras ao fornecedor (espelha RegimeIvaFornecedor no backend). */
export const REGIME_IVA = { Nacional: 0, UeAutoliquidacao: 1, ForaUe: 2 } as const;
export type RegimeIva = (typeof REGIME_IVA)[keyof typeof REGIME_IVA];
export const REGIME_IVA_LABEL: Record<RegimeIva, string> = {
  0: 'Nacional (IVA 23%)',
  1: 'UE — autoliquidação (0%)',
  2: 'Fora da UE (importação)',
};

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
  regimeIva: RegimeIva;
  pais: string | null;
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
  regimeIva?: RegimeIva;
  pais?: string | null;
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
