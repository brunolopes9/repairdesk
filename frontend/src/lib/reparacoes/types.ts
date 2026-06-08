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
  /** Sprint 348: usado pelo EmailMenu (Send 1-click). */
  email?: string | null;
  /** Sprint 355: alerta curto destacado (banner). */
  notaImportante?: string | null;
  /** Sprint 488: consentimento RGPD (S479/Codex) — superfícies de comunicação respeitam. */
  naoContactar?: boolean;
  contactoPreferido?: string | null;
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
  reciboNumero: string | null;
  reciboEmitidoEm: string | null;
  estimateExternalId: string | null;
  estimateNumber: string | null;
  estimatePdfUrl: string | null;
  estimateEmittedAt: string | null;
  equipmentFieldTemplateId: string | null;
  equipmentFieldTemplateNome: string | null;
  fields: EquipmentFieldValue[];
  precisaConfirmacaoPagamento: boolean;
  precisaConfirmacaoGarantia: boolean;
  /** Sprint 343: técnico atribuído (null = não atribuída ainda). */
  assignedToUserId: string | null;
  assignedToDisplayName: string | null;
  /** Sprint 346: tags categóricas atribuídas. */
  tags: Array<{ id: string; nome: string; corHex: string }>;
  /** Sprint 419: ETA de entrega (calendário). Null = sem ETA marcada. */
  previstoEntregueEm: string | null;
  /** Sprint 474: estado físico ao receber (rachado/riscado/sem acessórios). Distinto de diagnostico. */
  estadoFisicoInicial: string | null;
  /** Sprint 475: categoria estruturada (DeviceCategory enum 0-5/99). Null = não classificado. */
  categoria: number | null;
  /** Sprint 499: sinal/depósito recebido (cêntimos). Falta = (precoFinal ?? orcamento) − sinal. */
  sinalCents: number;
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
  /** Sprint 474: estado físico observado na recepção. */
  estadoFisicoInicial?: string | null;
  /** Sprint 475: categoria estruturada (DeviceCategory). */
  categoria?: number | null;
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
  /** Sprint 419: ETA de entrega (ISO). null = limpar. */
  previstoEntregueEm?: string | null;
  /** Sprint 474: estado físico inicial observado na recepção. */
  estadoFisicoInicial?: string | null;
  /** Sprint 475: categoria estruturada (DeviceCategory). */
  categoria?: number | null;
}

/** Sprint 475: DeviceCategory enum (alinhado com backend). */
export const DEVICE_CATEGORY = {
  Smartphone: 0,
  Tablet: 1,
  Laptop: 2,
  Desktop: 3,
  Smartwatch: 4,
  Consola: 5,
  Outro: 99,
} as const;

export const DEVICE_CATEGORY_LABEL: Record<number, string> = {
  0: 'Telemóvel',
  1: 'Tablet',
  2: 'Portátil',
  3: 'Desktop',
  4: 'Smartwatch',
  5: 'Consola',
  99: 'Outro',
};

export type ReparacoesPage = PagedResult<Reparacao>;
