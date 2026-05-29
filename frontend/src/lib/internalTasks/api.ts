import { api } from '../api';
import type { CreateInternalTaskForm, InternalTask, InternalTaskStatus, UpdateInternalTaskForm } from './types';

export const internalTasksApi = {
  list: (filters: { status?: InternalTaskStatus; assignedToUserId?: string; reparacaoId?: string } = {}) =>
    api
      .get<InternalTask[]>('/internal-tasks', {
        params: {
          status: filters.status,
          assignedToUserId: filters.assignedToUserId,
          reparacaoId: filters.reparacaoId,
        },
      })
      .then((r) => r.data),

  get: (id: string) => api.get<InternalTask>(`/internal-tasks/${id}`).then((r) => r.data),

  create: (form: CreateInternalTaskForm) =>
    api.post<InternalTask>('/internal-tasks', form).then((r) => r.data),

  update: (id: string, form: UpdateInternalTaskForm) =>
    api.put<InternalTask>(`/internal-tasks/${id}`, form).then((r) => r.data),

  changeStatus: (id: string, status: InternalTaskStatus) =>
    api.post<InternalTask>(`/internal-tasks/${id}/status`, { status }).then((r) => r.data),

  remove: (id: string) => api.delete(`/internal-tasks/${id}`),
};
