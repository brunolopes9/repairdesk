import { api } from '../api';

export type ApiKeyScope = 'read' | 'write';

export interface ServiceApiKey {
  id: string;
  name: string;
  keyPrefix: string;
  createdAt: string;
  lastUsedAt: string | null;
  revokedAt: string | null;
  revokedReason: string | null;
  /** Lista de scopes. Array vazio = wildcard (acesso total, backwards-compat com keys antigas). */
  scopes: ApiKeyScope[];
}

export interface CreateServiceApiKeyResponse {
  key: ServiceApiKey;
  plainKey: string;
}

export const serviceKeysApi = {
  list() {
    return api.get<ServiceApiKey[]>('/service-keys').then((r) => r.data);
  },
  create(name: string, scopes: ApiKeyScope[] | null) {
    return api.post<CreateServiceApiKeyResponse>('/service-keys', { name, scopes }).then((r) => r.data);
  },
  revoke(id: string, reason: string | null) {
    return api.post(`/service-keys/${id}/revoke`, { reason }).then(() => undefined);
  },
};
