import { api } from '../api';

export interface DashboardKpis {
  receitaCentsMes: number;
  despesasCentsMes: number;
  lucroCentsMes: number;
  vendasHojeCents: number;
  vendasMesCents: number;
  reparacoesAbertas: number;
  trabalhosAbertos: number;
  reparacoesEntreguesMes: number;
  trabalhosConcluidosMes: number;
}

export interface CategoriaBreakdown {
  label: string;
  count: number;
  totalCents: number;
}

export interface TopCliente {
  id: string;
  nome: string;
  totalCents: number;
  trabalhos: number;
}

export interface TopProdutoVendido {
  partId: string | null;
  descricao: string;
  quantidade: number;
  totalCents: number;
}

export interface DashboardResponse {
  kpis: DashboardKpis;
  receitaPorCategoria: CategoriaBreakdown[];
  despesaPorCategoria: CategoriaBreakdown[];
  topClientes: TopCliente[];
  topProdutosVendidos: TopProdutoVendido[];
}

export interface CategoriaFinanceira {
  label: string;
  count: number;
  receitaCents: number;
  custoCents: number;
  lucroCents: number;
}

export interface FinanceiroResponse {
  receitaRealizadaCents: number;
  custoImputadoCents: number;
  lucroRealizadoCents: number;
  receitaPendenteCents: number;
  investimentoStockCents: number;
  porCategoria: CategoriaFinanceira[];
  periodoDe: string;
  periodoAte: string;
}

export const dashboardApi = {
  current() {
    return api.get<DashboardResponse>('/dashboard').then((r) => r.data);
  },
  range(fromIso: string, toIso: string) {
    return api
      .get<DashboardResponse>('/dashboard', { params: { from: fromIso, to: toIso } })
      .then((r) => r.data);
  },
  financeiroCurrent() {
    return api.get<FinanceiroResponse>('/dashboard/financeiro').then((r) => r.data);
  },
  financeiroRange(fromIso: string, toIso: string) {
    return api
      .get<FinanceiroResponse>('/dashboard/financeiro', { params: { from: fromIso, to: toIso } })
      .then((r) => r.data);
  },
  alertas() {
    return api.get<AlertasResponse>('/dashboard/alertas').then((r) => r.data);
  },
  tendencia(meses = 6) {
    return api.get<TendenciaResponse>('/dashboard/tendencia', { params: { meses } }).then((r) => r.data);
  },
  topReparacoesCurrent(limit = 5) {
    return api.get<TopReparacoesResponse>('/dashboard/top-reparacoes', { params: { limit } }).then((r) => r.data);
  },
  topReparacoesRange(fromIso: string, toIso: string, limit = 5) {
    return api
      .get<TopReparacoesResponse>('/dashboard/top-reparacoes', { params: { from: fromIso, to: toIso, limit } })
      .then((r) => r.data);
  },
  avaliacoes() {
    return api.get<AvaliacoesDashboardResponse>('/dashboard/avaliacoes').then((r) => r.data);
  },
};

export interface AvaliacaoRecente {
  id: string;
  reparacaoId: string;
  reparacaoNumero: number;
  clienteNome: string;
  equipamento: string;
  score: number;
  comentario: string | null;
  criadaEm: string;
}

export interface AvaliacoesDashboardResponse {
  mediaScore: number | null;
  total: number;
  distribuicao: Record<string, number>; // {"1": 0, "2": 1, ...}
  promoters: number;
  detractors: number;
  nps: number;
  recentes: AvaliacaoRecente[];
}

export interface ItemPorCobrar {
  id: string;
  numero: number;
  titulo: string;
  clienteNome: string | null;
  valorCents: number;
  concluidoEm: string | null;
}

export interface DespesaOrfa {
  id: string;
  descricao: string;
  categoria: number;
  valorCents: number;
  data: string;
  fornecedor: string | null;
}

export interface AlertasResponse {
  trabalhosNaoPagos: ItemPorCobrar[];
  reparacoesNaoPagas: ItemPorCobrar[];
  despesasOrfas: DespesaOrfa[];
  totalPorCobrarCents: number;
  totalDespesasOrfasCents: number;
}

export interface MesFinanceiro {
  ano: number;
  mes: number;
  receitaCents: number;
  custoCents: number;
  lucroCents: number;
}

export interface TendenciaResponse {
  meses: MesFinanceiro[];
}

export interface ReparacaoTop {
  id: string;
  numero: number;
  equipamento: string;
  clienteNome: string | null;
  receitaCents: number;
  custoCents: number;
  lucroCents: number;
}

export interface TopReparacoesResponse {
  items: ReparacaoTop[];
}

export type Period = 'this-month' | 'last-month' | 'last-90' | 'this-year';

export function periodRange(period: Period): { from: Date; to: Date; label: string } {
  const now = new Date();
  const startOfThisMonth = new Date(now.getFullYear(), now.getMonth(), 1);
  const startOfNextMonth = new Date(now.getFullYear(), now.getMonth() + 1, 1);
  const startOfLastMonth = new Date(now.getFullYear(), now.getMonth() - 1, 1);
  const ninetyDaysAgo = new Date(now.getTime() - 90 * 24 * 60 * 60 * 1000);
  const startOfYear = new Date(now.getFullYear(), 0, 1);
  const startOfNextYear = new Date(now.getFullYear() + 1, 0, 1);

  switch (period) {
    case 'this-month':
      return { from: startOfThisMonth, to: startOfNextMonth, label: 'Este mês' };
    case 'last-month':
      return { from: startOfLastMonth, to: startOfThisMonth, label: 'Mês anterior' };
    case 'last-90':
      return { from: ninetyDaysAgo, to: now, label: '90 dias' };
    case 'this-year':
      return { from: startOfYear, to: startOfNextYear, label: 'Este ano' };
  }
}
