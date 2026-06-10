import { api } from '../api';

/** Sprint 180: linha individual para drill-down (PartMovimento Entrada ou Despesa). */
export interface IvaDeducaoLinha {
  data: string;
  descricao: string;
  fornecedor: string | null;
  origem: 'stock-entrada' | 'despesa-pecas' | 'despesa-opex';
  valorComIvaCents: number;
  ivaCents: number;
}

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
  /** Sprint 541: true = IVA exato (totais do Moloni ou taxa por linha); false = estimado a 23%. */
  ivaExato: boolean;
}

export interface RelatorioIvaResponse {
  ano: number;
  trimestre: number;
  periodoDe: string;
  periodoAte: string;
  // === Vendas ===
  totalSemIvaCents: number;
  ivaLiquidadoCents: number;
  /** Sprint 535: IVA do regime da margem (bens em segunda mão) — JÁ incluído em ivaLiquidadoCents. */
  ivaRegimeMargemCents: number;
  /** Sprint 535: nº de linhas de 2ª mão sem custo registado (não entraram na base da margem). */
  regimeMargemSemCustoCount: number;
  // === Compras dedutíveis (Sprint 159) ===
  /** Input manual Bruno (compras não registadas). */
  ivaComprasCents: number;
  /** Sprint 178: auto — IVA pago nas peças que ENTRARAM em stock no período (compras a fornecedor). */
  ivaDedutivelPecasCents: number;
  /** Sprint 176: auto — IVA das Despesas OpEx (IsCogs=false) no período. */
  ivaDedutivelDespesasCents: number;
  /** Sprint 180: drill-down — linhas individuais que somam para 'Compras stock'. */
  comprasStockDetalhe: IvaDeducaoLinha[];
  /** Sprint 180: drill-down — linhas individuais 'Despesas operacionais'. */
  despesasOpExDetalhe: IvaDeducaoLinha[];
  /** Soma das 3 fontes. */
  ivaDedutivelTotalCents: number;
  // === A entregar ===
  ivaAEntregarCents: number;
  // === Comparação ===
  trimestreAnteriorTotalSemIvaCents: number;
  trimestreAnteriorIvaLiquidadoCents: number;
  documentos: RelatorioIvaDocumento[];
}

export interface TopReparacaoLucrativa {
  id: string;
  numero: number;
  equipamento: string;
  clienteNome: string | null;
  receitaCents: number;
  custoPecasCents: number;
  lucroCents: number;
}

export interface TopPecaUsada {
  partId: string;
  nome: string;
  sku: string | null;
  quantidade: number;
}

export interface TopFornecedor {
  nome: string;
  totalCompradoCents: number;
}

export interface RelatorioNegocioResponse {
  ano: number;
  trimestre: number;
  periodoDe: string;
  periodoAte: string;
  receitaTotalCents: number;
  receitaReparacoesCents: number;
  receitaTrabalhosCents: number;
  receitaVendasCents: number;
  custoPecasCents: number;
  opexCents: number;
  lucroBrutoCents: number;     // Sprint 550: receita LÍQUIDA − peças − custo artigos vendidos
  margemMedia: number;         // margem bruta (sobre receita líquida)
  lucroOperacionalCents: number; // Sprint 536: lucro bruto − OpEx
  margemOperacionalPct: number;
  // Sprint 550 (mão de contabilista): receita sem IVA liquidado + IVA embutido + COGS das vendas.
  receitaLiquidaCents: number;
  ivaEmbutidoCents: number;
  custoVendasCents: number;
  ticketMedioCents: number;
  reparacoesPagasCount: number;
  topReparacoesLucrativas: TopReparacaoLucrativa[];
  topPecasUsadas: TopPecaUsada[];
  topFornecedores: TopFornecedor[];
}

/** Sprint 187: linha por fornecedor com taxa de devolução para reparação. */
export interface FornecedorDefeito {
  nome: string;
  itemsVendidos: number;
  itemsComReparacao: number;
  taxaDefeitoPct: number;
}

export interface TaxaDefeitoFornecedorResponse {
  meses: number;
  desdeUtc: string;
  fornecedores: FornecedorDefeito[];
}

/** Sprint 547 (Doc 93 #2): Análise de Vendas — top artigos + top clientes do trimestre. */
export interface AnaliseVendasTopArtigo {
  descricao: string;
  quantidade: number;
  receitaCents: number;
  /** null = artigo sem custo registado (margem incalculável). */
  margemCents: number | null;
}

export interface AnaliseVendasTopCliente {
  clienteId: string;
  nome: string;
  receitaCents: number;
  documentos: number;
}

export interface AnaliseVendasResponse {
  ano: number;
  trimestre: number;
  periodoDe: string;
  periodoAte: string;
  topArtigos: AnaliseVendasTopArtigo[];
  topClientes: AnaliseVendasTopCliente[];
}

export const relatoriosApi = {
  iva(ano: number, trimestre: number, ivaComprasCents = 0) {
    return api
      .get<RelatorioIvaResponse>('/relatorios/iva', { params: { ano, trimestre, ivaComprasCents } })
      .then((r) => r.data);
  },
  negocio(ano: number, trimestre: number) {
    return api
      .get<RelatorioNegocioResponse>('/relatorios/negocio', { params: { ano, trimestre } })
      .then((r) => r.data);
  },
  taxaDefeitoFornecedor(meses = 12) {
    return api
      .get<TaxaDefeitoFornecedorResponse>('/relatorios/taxa-defeito-fornecedor', { params: { meses } })
      .then((r) => r.data);
  },
  analiseVendas(ano: number, trimestre: number) {
    return api
      .get<AnaliseVendasResponse>('/relatorios/analise-vendas', { params: { ano, trimestre } })
      .then((r) => r.data);
  },
};
