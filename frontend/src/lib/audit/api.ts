import { api } from '../api';
import type { AuditPage } from './types';

export const auditApi = {
  list(filters: { entityType?: string; entityId?: string; from?: string; to?: string; page?: number; pageSize?: number } = {}) {
    return api
      .get<AuditPage>('/audit', {
        params: {
          entityType: filters.entityType || undefined,
          entityId: filters.entityId || undefined,
          from: filters.from || undefined,
          to: filters.to || undefined,
          page: filters.page ?? 1,
          pageSize: filters.pageSize ?? 50,
        },
      })
      .then((r) => r.data);
  },
};
