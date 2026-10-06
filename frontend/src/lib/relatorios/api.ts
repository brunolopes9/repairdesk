import { api } from '../api';

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
