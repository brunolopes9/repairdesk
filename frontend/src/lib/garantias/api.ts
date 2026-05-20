import { api } from '../api';

export const GARANTIA_SOURCE = {
  Reparacao: 0,
  Venda: 1,
} as const;
export type GarantiaSourceType = (typeof GARANTIA_SOURCE)[keyof typeof GARANTIA_SOURCE];

export interface GarantiaAdminDto {
  id: string;
  slug: string;
  sourceType: GarantiaSourceType;
  reparacaoId: string | null;
  vendaId: string | null;
  dataInicio: string;
  dataFim: string;
  diasGarantia: number;
  diasRestantes: number;
  activa: boolean;
  anulada: boolean;
  motivoAnulacao: string | null;
  cobertura: string | null;
  exclusoes: string | null;
}

export const garantiasApi = {
  byReparacao(reparacaoId: string) {
    return api
      .get<GarantiaAdminDto>(`/garantias/by-reparacao/${reparacaoId}`)
      .then((r) => r.data)
      .catch((err) => {
        if (err?.response?.status === 404) return null;
        throw err;
      });
  },
  byVenda(vendaId: string) {
    return api
      .get<GarantiaAdminDto>(`/garantias/by-venda/${vendaId}`)
      .then((r) => r.data)
      .catch((err) => {
        if (err?.response?.status === 404) return null;
        throw err;
      });
  },
  anular(id: string, motivo: string) {
    return api.post<GarantiaAdminDto>(`/garantias/${id}/anular`, { motivo }).then((r) => r.data);
  },
  pdfUrl(id: string) {
    // Sprint 145: devolve path relativo (sem baseURL) — openPdfInNewTab chama api.get(path)
    // que já adiciona o baseURL='/api'. Prefixar aqui causava /api/api/... → 404.
    return `/garantias/${id}/pdf`;
  },
};
