import { api } from '../api';

/** Sprint 573: apuramento trimestral do IVA e resultado (espelha ApuramentoDto). Valores em euros. */
export interface ApuramentoTaxa { taxa: number; base: number; iva: number; linhas: number }
export interface ApuramentoCampo { campo: string; descricao: string; valor: number }
export type TipoPendente = 'venda_sem_fatura' | 'compra_sem_fatura' | 'despesa_sem_iva' | 'despesa_pecas';
export interface ApuramentoPendente { tipo: TipoPendente; id: string; descricao: string; valor: number; data: string }

export interface Apuramento {
  ano: number;
  trimestre: number;
  de: string;
  ate: string;
  vendasPorTaxa: ApuramentoTaxa[];
  faturas: number;
  ivaLiquidado: number;
  compras: { documentos: number; baseNacional: number; ivaNacional: number; ivaPortes: number; baseUe: number; ivaAutoliquidadoUe: number; baseForaUe: number; baseComIvaEstrangeiro: number };
  despesas: { despesas: number; base: number; ivaDedutivel: number; ivaNaoDedutivel: number; baseAutoliquidacao: number; ivaAutoliquidado: number };
  ivaDedutivel: number;
  ivaAutoliquidado: number;
  /** Positivo = a entregar; negativo = a recuperar. */
  saldo: number;
  resultado: { vendas: number; custoPecasVendidas: number; gastos: number; resultado: number; comprasDeStock: number; trading: number };
  estimativas: ApuramentoEstimativas;
  campos: ApuramentoCampo[];
  pendentes: ApuramentoPendente[];
  /** Sprint 576: vendas a 0% a clientes UE (vão também à recapitulativa) e de fora da UE. */
  vendasIsentasUe: number;
  vendasIsentasForaUe: number;
  /** Trimestre declarado: valores que foram à declaração + diferença para o recalculado agora. */
  fecho: { ivaLiquidado: number; ivaDedutivel: number; saldo: number; pago: boolean; fechadoEm: string; diferenca: number } | null;
  /** Sprint 578: crédito do trimestre anterior que abate a este, e o valor final a pagar / a reportar. */
  reporte: number;
  saldoFinal: number;
}


/** Sprint 575: IRS (regime simplificado) e Segurança Social — estimativas do ano até ao fim do trimestre. */
export interface ApuramentoEstimativas {
  /** Null = por indicar (sem redução de IRS e sem isenção de Segurança Social). */
  inicioAtividade: string | null;
  inicioDefinido: boolean;
  receitasBens: number;
  /** Sprint 580: informática/software e reparações (sem trading), ao coeficiente coefServicos. */
  receitasInformatica: number;
  receitasReparacoes: number;
  receitasTrading: number;
  fatorInicio: number;
  despesasJustificadas: number;
  acrescimoRegra15: number;
  rendimentoTributavel: number;
  irsEstimado: number;
  taxaEfetiva: number;
  anoTabelaIrs: number;
  ssRendimentoRelevante: number;
  ssContribuicaoMensal: number;
  ssIsento: boolean;
  ssIsentoAte: string | null;
  ssContribuicaoAtual: number | null;
  /** Sprint 580: coeficientes por código (0,75 na tabela do art. 151.º, senão 0,35). */
  coefServicos: number;
  coefTrading: number;
  servicosNaTabela151: boolean;
  tradingNaTabela151: boolean;
  irsJovemAno: number | null;
  irsJovemPercentagem: number;
  irsJovemIsencao: number;
  coletaAntesDeducoes: number;
  deducoesColeta: number | null;
}

export interface PressupostosRequest {
  inicioAtividade: string | null;
  ssContribuicaoAtual: number | null;
  irsJovemAno: number | null;
  semIrsJovem: boolean;
  deducoesColeta: number | null;
  servicosNaTabela151: boolean;
  tradingNaTabela151: boolean;
}

/** Sprint 580: lembrete fiscal com prazo (ex.: rever o código de atividade do trading em janeiro). */
export interface LembreteFiscal {
  codigo: string;
  titulo: string;
  texto: string;
  urgente: boolean;
}

export const apuramentoApi = {
  trimestre(ano: number, trimestre: number) {
    return api.get<Apuramento>('/apuramento', { params: { ano, trimestre } }).then((r) => r.data);
  },
  definirPressupostos(req: PressupostosRequest) {
    return api.put('/apuramento/pressupostos', req).then(() => undefined);
  },
  lembretes() {
    return api.get<LembreteFiscal[]>('/apuramento/lembretes').then((r) => r.data);
  },
  marcarCodigoTradingRevisto(revisto: boolean) {
    return api.put('/apuramento/lembretes/codigo-trading', { revisto }).then(() => undefined);
  },
  fechar(ano: number, trimestre: number) {
    return api.post('/apuramento/fecho', { ano, trimestre, pago: true }).then(() => undefined);
  },
  reabrir(ano: number, trimestre: number) {
    return api.delete('/apuramento/fecho', { params: { ano, trimestre } }).then(() => undefined);
  },
};

/** Último trimestre já fechado (é esse que se declara). */
export function trimestreAnterior(hoje = new Date()): { ano: number; trimestre: number } {
  const t = Math.floor(hoje.getMonth() / 3) + 1;
  return t === 1 ? { ano: hoje.getFullYear() - 1, trimestre: 4 } : { ano: hoje.getFullYear(), trimestre: t - 1 };
}

/** Regime trimestral: declaração até dia 20 do 2.º mês seguinte ao trimestre; pagamento até dia 25. */
export function prazos(ano: number, trimestre: number): { entrega: Date; pagamento: Date } {
  const mes = trimestre * 3 + 1; // 2.º mês depois do fim do trimestre (0-based: fim = trimestre*3-1)
  return { entrega: new Date(ano, mes, 20), pagamento: new Date(ano, mes, 25) };
}
