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
  compras: { documentos: number; baseNacional: number; ivaNacional: number; ivaPortes: number; baseUe: number; ivaAutoliquidadoUe: number; baseForaUe: number };
  despesas: { despesas: number; base: number; ivaDedutivel: number; ivaNaoDedutivel: number; baseAutoliquidacao: number; ivaAutoliquidado: number };
  ivaDedutivel: number;
  ivaAutoliquidado: number;
  /** Positivo = a entregar; negativo = a recuperar. */
  saldo: number;
  resultado: { vendas: number; custoPecasVendidas: number; gastos: number; resultado: number; comprasDeStock: number; trading: number };
  estimativas: ApuramentoEstimativas;
  campos: ApuramentoCampo[];
  pendentes: ApuramentoPendente[];
}

/** Sprint 575: IRS (regime simplificado) e Segurança Social — estimativas do ano até ao fim do trimestre. */
export interface ApuramentoEstimativas {
  inicioAtividade: string;
  inicioDefinido: boolean;
  receitasBens: number;
  receitasServicos075: number;
  receitasServicos035: number;
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
  ssIsentoAte: string;
}

export const apuramentoApi = {
  trimestre(ano: number, trimestre: number) {
    return api.get<Apuramento>('/apuramento', { params: { ano, trimestre } }).then((r) => r.data);
  },
  definirInicioAtividade(data: string | null) {
    return api.put('/apuramento/inicio-atividade', { data }).then(() => undefined);
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
