import type { PagedResult } from '../clientes/types';

export const AUDIT_ACTION_LABEL: Record<number, string> = {
  0: 'Criação',
  1: 'Actualização',
  2: 'Apagar',
  3: 'Apagar definitivo',
  4: 'Login',
  5: 'Exportação',
};

export interface AuditEntry {
  id: string;
  tenantId: string;
  appUserId: string | null;
  action: number;
  entityType: string;
  entityId: string | null;
  changesJson: string | null;
  ipAddress: string | null;
  userAgent: string | null;
  createdAt: string;
}

export type AuditPage = PagedResult<AuditEntry>;
