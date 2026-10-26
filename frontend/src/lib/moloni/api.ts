import { api } from '../api';
import type { PaymentMethod, Venda } from '../vendas/types';

/** Sprint 567: ligação ao Moloni ON — os documentos fiscais emitem-se lá a partir do Mender. */
export interface MoloniSerie {
  id: number;
  nome: string;
  predefinida: boolean;
}

export interface MoloniEstado {
  configurado: boolean;
  companyId: number | null;
  series: MoloniSerie[];
  serieServico: number | null;
  serieReparacao: number | null;
  serieProduto: number | null;
}

export type MoloniDocumento = 'Fatura' | 'FaturaRecibo';

/** Motivos de isenção mais usados (código AT). Linhas a 0% exigem um. */
export const MOTIVOS_ISENCAO = [
  { codigo: 'M40', label: 'M40 — Autoliquidação (cliente estrangeiro, serviços)' },
  { codigo: 'M16', label: 'M16 — Art. 14.º do RITI (entrega intracomunitária)' },
  { codigo: 'M05', label: 'M05 — Art. 14.º do CIVA (exportação)' },
  { codigo: 'M07', label: 'M07 — Art. 9.º do CIVA (isenção)' },
  { codigo: 'M99', label: 'M99 — Não sujeito / não tributado' },
] as const;

export const moloniApi = {
  estado() {
    return api.get<MoloniEstado>('/moloni/estado').then((r) => r.data);
  },
  configurar(req: { apiKey: string | null; serieServico?: number | null; serieReparacao?: number | null; serieProduto?: number | null }) {
    return api.put<MoloniEstado>('/moloni', req, { timeout: 60_000 }).then((r) => r.data);
  },
  remover() {
    return api.delete('/moloni');
  },
  emitir(vendaId: string, req: { tipo: MoloniDocumento; serieId?: number | null; paymentMethod?: PaymentMethod | null; vencimentoDias?: number; motivoIsencao?: string | null }) {
    return api.post<Venda>(`/vendas/${vendaId}/moloni/emitir`, req, { timeout: 90_000 }).then((r) => r.data);
  },
  recibo(vendaId: string, paymentMethod: PaymentMethod | null) {
    return api.post<Venda>(`/vendas/${vendaId}/moloni/recibo`, { paymentMethod }, { timeout: 60_000 }).then((r) => r.data);
  },
  anular(vendaId: string, motivo: string) {
    return api.post<Venda>(`/vendas/${vendaId}/moloni/anular`, { motivo }, { timeout: 60_000 }).then((r) => r.data);
  },
  orcamento(vendaId: string, req: { serieId?: number | null; validadeDias?: number; motivoIsencao?: string | null }) {
    return api.post<Venda>(`/vendas/${vendaId}/moloni/orcamento`, req, { timeout: 90_000 }).then((r) => r.data);
  },
  /** Para openPdfInNewTab: documento = fatura | recibo | orcamento (pedido ao Moloni na hora). */
  pdfPath(vendaId: string, documento: 'fatura' | 'recibo' | 'orcamento') {
    return `/vendas/${vendaId}/moloni/${documento}.pdf`;
  },
};
