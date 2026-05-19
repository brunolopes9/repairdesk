import { api } from '../api';
import type { AuditFilterOptions, AuditFilters, AuditPage } from './types';

export const auditApi = {
  list(filters: AuditFilters = {}) {
    return api.get<AuditPage>(`/audit/search?${buildAuditQuery(filters)}`).then((r) => r.data);
  },
  filters() {
    return api.get<AuditFilterOptions>('/audit/filters').then((r) => r.data);
  },
  exportCsvPath(filters: AuditFilters) {
    return `/audit/export.csv?${buildAuditQuery(filters)}`;
  },
  exportPdfPath(filters: AuditFilters) {
    return `/audit/export.pdf?${buildAuditQuery(filters)}`;
  },
};

export function buildAuditQuery(filters: AuditFilters): string {
  const params = new URLSearchParams();
  filters.entityTypes?.filter(Boolean).forEach((v) => params.append('entityTypes', v));
  filters.userIds?.filter(Boolean).forEach((v) => params.append('userIds', v));
  filters.serviceApiKeyIds?.filter(Boolean).forEach((v) => params.append('serviceApiKeyIds', v));
  filters.actions?.forEach((v) => params.append('actions', String(v)));
  if (filters.entityId) params.set('entityId', filters.entityId);
  if (filters.search) params.set('search', filters.search);
  if (filters.from) params.set('from', filters.from);
  if (filters.to) params.set('to', filters.to);
  params.set('page', String(filters.page ?? 1));
  params.set('pageSize', String(filters.pageSize ?? 50));
  return params.toString();
}
