// Sprint 422: tarefas internas (TODO list).

export const InternalTaskStatus = {
  Pendente: 0,
  Concluida: 1,
  Cancelada: 2,
} as const;

export type InternalTaskStatus = (typeof InternalTaskStatus)[keyof typeof InternalTaskStatus];

/** Sprint 617 (Doc 98 F2): Hoje (máx. 3) / Semana / Ideia (capturada para mais tarde). */
export const TarefaFoco = { Nenhum: 0, Hoje: 1, Semana: 2, Ideia: 3 } as const;
export type TarefaFoco = (typeof TarefaFoco)[keyof typeof TarefaFoco];
export const MAX_HOJE = 3;

export interface InternalTask {
  id: string;
  title: string;
  description: string | null;
  dueAt: string | null;
  status: InternalTaskStatus;
  completedAt: string | null;
  assignedToUserId: string | null;
  assignedToDisplayName: string | null;
  createdByUserId: string;
  createdAt: string;
  vendaId: string | null;
  vendaNumero: number | null;
  foco: TarefaFoco;
}

export interface CreateInternalTaskForm {
  title: string;
  description?: string | null;
  dueAt?: string | null;
  assignedToUserId?: string | null;
  vendaId?: string | null;
  foco?: TarefaFoco;
}

export type UpdateInternalTaskForm = CreateInternalTaskForm;
