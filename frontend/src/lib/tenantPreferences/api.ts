import { api } from '../api';
import type { TenantPreferencesRoot } from './types';

export const tenantPreferencesApi = {
  get() {
    return api.get<TenantPreferencesRoot>('/tenant-settings/me/preferences').then((r) => r.data);
  },
  update(payload: TenantPreferencesRoot) {
    return api.put<TenantPreferencesRoot>('/tenant-settings/me/preferences', payload).then((r) => r.data);
  },
  resetGroup(group: 'communication' | 'portal' | 'repairs' | 'sales') {
    return api.post<TenantPreferencesRoot>(`/tenant-settings/me/preferences/reset/${group}`).then((r) => r.data);
  },
};
