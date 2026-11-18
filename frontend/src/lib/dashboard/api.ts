import { api } from '../api';
import type { VendaEstado, VendaTipo } from '../vendas/types';

/** Doc 94: Dashboard do modelo novo. Valores em euros (decimais), calculados pelo motor de IVA. */
export interface PainelMes {
  de: string;
  ate: string;
  vendas: number;
  faturado: number;
  ivaNasVendas: number;
  ivaAEntregar: number;
  lucroVendas: number;
  despesas: number;
}

export interface PainelEmCurso {
  orcamentos: number;
  emCurso: number;
  aEsperaPeca: number;
  prontas: number;
}

export interface PainelAlertas {
  faturasPorRegistar: number;
  comprasSemFatura: number;
  comprasComDiferenca: number;
  faturasRecebidasPorAprovar: number;
}

export interface PainelStock {
  unidades: number;
  valorPago: number;
  lucroSeVenderTudo: number;
}

export interface PainelVendaResumo {
  id: string;
  numero: number;
  tipo: VendaTipo;
  estado: VendaEstado;
  cliente: string | null;
  descricao: string | null;
  previstoPara: string | null;
  totalCents: number;
}

/** Vendas entregues num dia (últimos 30 dias). */
export interface PainelDia {
  dia: string;
  vendas: number;
  faturado: number;
}

/** Dashboard novo (18/11/2026): hoje, mês anterior até ao mesmo dia, 30 dias e vendas paradas. */
export interface PainelRitmo {
  vendasHoje: number;
  faturadoHoje: number;
  faturadoMesAnteriorAteHoje: number;
  paradas: number;
  ultimos30Dias: PainelDia[];
}

export interface Painel {
  mes: PainelMes;
  emCurso: PainelEmCurso;
  alertas: PainelAlertas;
  stock: PainelStock;
  proximasEntregas: PainelVendaResumo[];
  ritmo?: PainelRitmo | null;
}

export const dashboardApi = {
  get() {
    return api.get<Painel>('/dashboard').then((r) => r.data);
  },
};
