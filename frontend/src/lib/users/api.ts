import { api } from '../api';
import type { UserListItem } from './types';

export const usersApi = {
  list() {
    return api.get<UserListItem[]>('/users').then((r) => r.data);
  },
  getRoles(id: string) {
    return api.get<{ userId: string; roles: string[] }>(`/users/${id}/roles`).then((r) => r.data);
  },
  setRoles(id: string, roles: string[]) {
    return api.put<{ userId: string; roles: string[] }>(`/users/${id}/roles`, { roles }).then((r) => r.data);
  },
  deactivate(id: string, reason?: string) {
    return api.post<{ userId: string; revokedCount: number }>(`/users/${id}/deactivate`, { reason }).then((r) => r.data);
  },
  revokeSessions(id: string) {
    return api.post<{ revokedCount: number }>(`/users/${id}/revoke-sessions`).then((r) => r.data);
  },
};
