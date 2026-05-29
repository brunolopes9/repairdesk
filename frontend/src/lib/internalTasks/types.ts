// Sprint 422: tarefas internas (TODO list).

export const InternalTaskStatus = {
  Pendente: 0,
  Concluida: 1,
  Cancelada: 2,
} as const;

export type InternalTaskStatus = (typeof InternalTaskStatus)[keyof typeof InternalTaskStatus];

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
  reparacaoId: string | null;
  reparacaoNumero: number | null;
}

export interface CreateInternalTaskForm {
  title: string;
  description?: string | null;
  dueAt?: string | null;
  assignedToUserId?: string | null;
  reparacaoId?: string | null;
}

export type UpdateInternalTaskForm = CreateInternalTaskForm;
