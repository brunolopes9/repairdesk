import type { PagedResult } from '../clientes/types';

export const JOB_CATEGORY = {
  Reparacao: 0,
  Website: 1,
  Software: 2,
  EquipamentoNovo: 3,
  Outro: 99,
} as const;
export type JobCategory = (typeof JOB_CATEGORY)[keyof typeof JOB_CATEGORY];

export const TRABALHO_STATUS = {
  Orcamento: 0,
  Aceite: 1,
  EmExecucao: 2,
  Concluido: 3,
  Cancelado: 4,
} as const;
export type TrabalhoStatus = (typeof TRABALHO_STATUS)[keyof typeof TRABALHO_STATUS];

export const PAYMENT_STATUS = { NaoPago: 0, PagoParcial: 1, Pago: 2, Anulado: 3 } as const;
export type PaymentStatus = (typeof PAYMENT_STATUS)[keyof typeof PAYMENT_STATUS];

export const CATEGORIA_LABEL: Record<JobCategory, string> = {
  0: 'Reparação',
  1: 'Website',
  2: 'Software',
  3: 'Equipamento novo',
  99: 'Outro',
};

export const TRABALHO_STATUS_LABEL: Record<TrabalhoStatus, string> = {
  0: 'Orçamento',
  1: 'Aceite',
  2: 'Em execução',
  3: 'Concluído',
  4: 'Cancelado',
};

export const TRABALHO_STATUS_COLOR: Record<TrabalhoStatus, string> = {
  // Orçamento — amarelo (à espera de aceitação)
  0: 'bg-yellow-100 text-yellow-800 ring-1 ring-yellow-200 dark:bg-yellow-950/40 dark:text-yellow-300',
  // Aceite — azul (cliente confirmou, espera para começar / material a chegar)
  1: 'bg-blue-200 text-blue-900 ring-1 ring-blue-300 dark:bg-blue-900/60 dark:text-blue-100',
  // Em Execução — violeta (a trabalhar)
  2: 'bg-violet-200 text-violet-900 ring-1 ring-violet-300 dark:bg-violet-900/60 dark:text-violet-100',
  // Concluído — verde (terminado, pago default)
  3: 'bg-emerald-200 text-emerald-900 ring-1 ring-emerald-300 dark:bg-emerald-900/60 dark:text-emerald-100',
  // Cancelado — vermelho escuro
  4: 'bg-rose-700 text-white dark:bg-rose-800 dark:text-rose-100',
};

// 4 estados principais visíveis no workflow
export const TRABALHO_PRIMARY_STATUSES: TrabalhoStatus[] = [0, 1, 2, 3];

// Transições válidas
export const TRABALHO_VALID_TRANSITIONS: Record<TrabalhoStatus, TrabalhoStatus[]> = {
  0: [1, 2, 4],     // Orçamento → Aceite / EmExecução / Cancelar
  1: [2, 4],        // Aceite → EmExecução / Cancelar
  2: [3, 4],        // EmExecução → Concluído / Cancelar
  3: [],            // terminal
  4: [],            // terminal
};

export const PAYMENT_LABEL: Record<PaymentStatus, string> = {
  0: 'Não pago',
  1: 'Pago parcial',
  2: 'Pago',
  3: 'Anulado',
};

export interface ClienteResumo {
  id: string;
  nome: string;
  telefone: string;
}

export interface Trabalho {
  id: string;
  numero: number;
  cliente: ClienteResumo | null;
  titulo: string;
  descricao: string | null;
  categoria: JobCategory;
  status: TrabalhoStatus;
  createdAt: string;
  dataInicio: string | null;
  dataConclusao: string | null;
  orcamentoCents: number | null;
  precoFinalCents: number | null;
  horasGastas: number;
  notas: string | null;
  estadoPagamento: PaymentStatus;
  custoDespesasCents: number;
  lucroCents: number;
}

export interface CreateTrabalhoForm {
  clienteId: string | null;
  titulo: string;
  descricao: string | null;
  categoria: JobCategory;
  orcamentoCents: number | null;
  notas: string | null;
}

export interface UpdateTrabalhoForm {
  clienteId: string | null;
  titulo: string;
  descricao: string | null;
  categoria: JobCategory;
  status: TrabalhoStatus;
  dataInicio: string | null;
  dataConclusao: string | null;
  orcamentoCents: number | null;
  precoFinalCents: number | null;
  horasGastas: number;
  notas: string | null;
  estadoPagamento: PaymentStatus;
}

export type TrabalhosPage = PagedResult<Trabalho>;
