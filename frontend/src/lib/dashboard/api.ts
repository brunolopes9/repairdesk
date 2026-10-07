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

export interface Painel {
  mes: PainelMes;
  emCurso: PainelEmCurso;
  alertas: PainelAlertas;
  stock: PainelStock;
  proximasEntregas: PainelVendaResumo[];
}

export const dashboardApi = {
  get() {
    return api.get<Painel>('/dashboard').then((r) => r.data);
  },
};
