import type { PagedResult } from '../clientes/types';
import type { EquipmentFieldValue, SetEquipmentFieldValue } from '../equipmentFields/types';

export const REPAIR_STATUS = {
  Recebido: 0,
  Diagnostico: 1,
  AguardaPeca: 2,
  EmReparacao: 3,
  Pronto: 4,
  Entregue: 5,
  Cancelado: 6,
  Orcamento: 7,
} as const;

export type RepairStatus = (typeof REPAIR_STATUS)[keyof typeof REPAIR_STATUS];

export const PAYMENT_STATUS = {
  NaoPago: 0,
  PagoParcial: 1,
  Pago: 2,
  Anulado: 3,
} as const;

export type PaymentStatus = (typeof PAYMENT_STATUS)[keyof typeof PAYMENT_STATUS];

export const STATUS_LABEL: Record<RepairStatus, string> = {
  0: 'Recebido',
  1: 'Diagnóstico',
  2: 'Aguarda peça',
  3: 'Em reparação',
  4: 'Reparado',
  5: 'Entregue',
  6: 'Cancelado',
  7: 'Orçamento',
};

export const STATUS_COLOR: Record<RepairStatus, string> = {
  // Recebido: âmbar — "olha para mim, falta diagnóstico"
  0: 'bg-amber-200 text-amber-900 ring-1 ring-amber-300 dark:bg-amber-900/60 dark:text-amber-100 dark:ring-amber-700',
  // Diagnóstico: violeta — em análise pelo técnico
  1: 'bg-violet-200 text-violet-900 ring-1 ring-violet-300 dark:bg-violet-900/60 dark:text-violet-100 dark:ring-violet-700',
  // Aguarda Peça: azul claro — bloqueado externamente
  2: 'bg-sky-200 text-sky-900 ring-1 ring-sky-300 dark:bg-sky-900/60 dark:text-sky-100 dark:ring-sky-700',
  // Em Reparação: azul forte — técnico a trabalhar
  3: 'bg-blue-200 text-blue-900 ring-1 ring-blue-300 dark:bg-blue-900/60 dark:text-blue-100 dark:ring-blue-700',
  // Reparado: verde forte — "pronto para entregar"
  4: 'bg-emerald-200 text-emerald-900 ring-1 ring-emerald-300 dark:bg-emerald-900/60 dark:text-emerald-100 dark:ring-emerald-700',
  // Entregue: vermelho escuro — terminal, fora do fluxo activo
  5: 'bg-rose-700 text-white dark:bg-rose-800 dark:text-rose-100',
  // Cancelado: cinzento
  6: 'bg-zinc-300 text-zinc-800 dark:bg-zinc-700 dark:text-zinc-200',
  // Orçamento: amarelo claro (pre-loja)
  7: 'bg-yellow-100 text-yellow-800 ring-1 ring-yellow-200 dark:bg-yellow-950/40 dark:text-yellow-300',
};

// Estados principais visíveis no workflow stepper (na ordem do progresso)
export const PRIMARY_STATUSES: RepairStatus[] = [0, 1, 2, 3, 4, 5];

// Estados "em curso" — ainda precisam de acção. Excluem Entregue/Cancelado (terminais) e Orçamento (pré-loja).
export const STATES_EM_CURSO: RepairStatus[] = [0, 1, 2, 3, 4];

export const PAYMENT_LABEL: Record<PaymentStatus, string> = {
  0: 'Não pago',
  1: 'Pago parcial',
  2: 'Pago',
  3: 'Anulado',
};

export const VALID_TRANSITIONS: Record<RepairStatus, RepairStatus[]> = {
  0: [1, 6],             // Recebido → Diagnóstico / Cancelar
  1: [2, 3, 4, 6],       // Diagnóstico → Aguarda Peça / Em Reparação / Reparado (skip) / Cancelar
  2: [3, 1, 6],          // Aguarda Peça → Em Reparação / Diagnóstico (re-avaliar) / Cancelar
  3: [4, 2, 6],          // Em Reparação → Reparado / Aguarda Peça (precisa mais peça) / Cancelar
  4: [5, 1, 6],          // Reparado → Entregue / reabrir (Diagnóstico) / Cancelar
  5: [],                 // Entregue: terminal
  6: [],                 // Cancelado: terminal
  7: [0, 6],             // Orçamento → Recebido / Cancelar
};

export interface ClienteResumo {
  id: string;
  nome: string;
  telefone: string;
  /** Sprint 114: usado para banner "fatura sairá como Simplificada" quando vazio. */
  nif: string | null;
}

export interface EstadoLog {
  id: string;
  estadoFrom: RepairStatus | null;
  estadoTo: RepairStatus;
  mudouEm: string;
  notas: string | null;
}

export interface Reparacao {
  id: string;
  numero: number;
  cliente: ClienteResumo;
  equipamento: string;
  avaria: string;
  imei: string | null;
  diagnostico: string | null;
  estado: RepairStatus;
  estadoSince: string;
  recebidoEm: string;
  entregueEm: string | null;
  orcamentoCents: number | null;
  orcamentoAprovado: boolean;
  precoFinalCents: number | null;
  custoPecasCents: number;
  horasGastas: number;
  lucroCents: number;
  custoDespesasCents: number;
  notas: string | null;
  estadoPagamento: PaymentStatus;
  /** Sprint 229: slug público para portal cliente (/r/{slug}) — sempre presente. */
  publicSlug: string | null;
  invoiceProvider: 0 | 1 | 2;
  invoiceExternalId: string | null;
  invoicePdfUrl: string | null;
  invoiceNumber: string | null;
  invoiceEmittedAt: string | null;
  estimateExternalId: string | null;
  estimateNumber: string | null;
  estimatePdfUrl: string | null;
  estimateEmittedAt: string | null;
  equipmentFieldTemplateId: string | null;
  equipmentFieldTemplateNome: string | null;
  fields: EquipmentFieldValue[];
}

export interface ReparacaoDetalhada {
  reparacao: Reparacao;
  timeline: EstadoLog[];
  /** Sprint 87: venda anterior cujo IMEI bate (se aplicável) — para banner "em garantia". */
  vendaOrigem: ReparacaoVendaOrigem | null;
}

export interface ReparacaoVendaOrigem {
  vendaId: string;
  vendaNumero: number;
  vendaData: string;
  garantiaSlug: string | null;
  garantiaActiva: boolean;
  diasRestantesGarantia: number;
  diasEntreVendaEReparacao: number;
  /** Sprint 108: info do fornecedor B2B do item — para banner cobertura. */
  fornecedorNome: string | null;
  condicao: number;
  /** ISO date — até quando o fornecedor cobre garantia B2B. */
  garantiaFornecedorAteAo: string | null;
}

export interface CreateReparacaoForm {
  clienteId: string;
  equipamento: string;
  avaria: string;
  imei: string | null;
  orcamentoCents: number | null;
  notas: string | null;
  estadoInicial?: RepairStatus | null;
  equipmentFieldTemplateId?: string | null;
  fields?: SetEquipmentFieldValue[] | null;
}

export interface UpdateReparacaoForm {
  clienteId?: string | null;
  equipamento: string;
  avaria: string;
  imei: string | null;
  diagnostico: string | null;
  orcamentoCents: number | null;
  orcamentoAprovado: boolean;
  precoFinalCents: number | null;
  custoPecasCents: number;
  horasGastas: number;
  notas: string | null;
  estadoPagamento: PaymentStatus;
  equipmentFieldTemplateId?: string | null;
  fields?: SetEquipmentFieldValue[] | null;
}

export type ReparacoesPage = PagedResult<Reparacao>;
