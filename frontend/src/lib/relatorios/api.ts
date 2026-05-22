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
  // === Vendas ===
  totalSemIvaCents: number;
  ivaLiquidadoCents: number;
  // === Compras dedutíveis (Sprint 159) ===
  /** Input manual Bruno (compras não registadas). */
  ivaComprasCents: number;
  /** Sprint 178: auto — IVA pago nas peças que ENTRARAM em stock no período (compras a fornecedor). */
  ivaDedutivelPecasCents: number;
  /** Sprint 176: auto — IVA das Despesas OpEx (IsCogs=false) no período. */
  ivaDedutivelDespesasCents: number;
  /** Soma das 3 fontes. */
  ivaDedutivelTotalCents: number;
  // === A entregar ===
  ivaAEntregarCents: number;
  // === Comparação ===
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
