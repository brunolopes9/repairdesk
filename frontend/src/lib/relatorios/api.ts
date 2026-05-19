import { api } from '../api';

export interface RelatorioIvaDocumento {
  id: string;
  tipo: string;
  numeroInterno: number;
  numeroDocumento: string;
  data: string;
  cliente: string;
  baseCents: number;
  ivaCents: number;
  totalCents: number;
}

export interface RelatorioIvaResponse {
  ano: number;
  trimestre: number;
  periodoDe: string;
  periodoAte: string;
  totalSemIvaCents: number;
  ivaLiquidadoCents: number;
  ivaComprasCents: number;
  ivaAEntregarCents: number;
  trimestreAnteriorTotalSemIvaCents: number;
  trimestreAnteriorIvaLiquidadoCents: number;
  documentos: RelatorioIvaDocumento[];
}

export const relatoriosApi = {
  iva(ano: number, trimestre: number, ivaComprasCents = 0) {
    return api
      .get<RelatorioIvaResponse>('/relatorios/iva', { params: { ano, trimestre, ivaComprasCents } })
      .then((r) => r.data);
  },
};
