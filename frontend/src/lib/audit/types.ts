import type { PagedResult } from '../clientes/types';

export const AUDIT_ACTION_LABEL: Record<number, string> = {
  0: 'Criado',
  1: 'Atualizado',
  2: 'Apagado',
  3: 'Apagado definitivo',
  4: 'Login',
  5: 'Exportado',
};

export interface AuditEntry {
  id: string;
  tenantId: string;
  appUserId: string | null;
  appUserDisplayName: string | null;
  appUserEmail: string | null;
  action: number;
  entityType: string;
  entityId: string | null;
  changesJson: string | null;
  ipAddress: string | null;
  userAgent: string | null;
  createdAt: string;
  /** Sprint 99: integração externa — identifica a API key autora. */
  serviceApiKeyId: string | null;
  serviceApiKeyName: string | null;
  serviceApiKeyPrefix: string | null;
}

export type AuditPage = PagedResult<AuditEntry>;

export interface AuditUserOption {
  id: string;
  displayName: string;
  email: string | null;
}

export interface AuditApiKeyOption {
  id: string;
  name: string;
  keyPrefix: string;
  revoked: boolean;
}

export interface AuditFilterOptions {
  entityTypes: string[];
  users: AuditUserOption[];
  actions: number[];
  apiKeys: AuditApiKeyOption[];
}

export interface AuditFilters {
  entityTypes?: string[];
  entityId?: string;
  userIds?: string[];
  actions?: number[];
  search?: string;
  from?: string;
  to?: string;
  page?: number;
  pageSize?: number;
  serviceApiKeyIds?: string[];
}
