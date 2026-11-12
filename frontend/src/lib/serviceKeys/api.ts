import { api } from '../api';

// Sprint 598: «conteudo» = calendário das redes sociais (Doc 95); «ingest» = faturas de fornecedor.
export type ApiKeyScope = 'read' | 'write' | 'ingest' | 'conteudo' | 'loja' | 'assistente';

export const API_KEY_SCOPES: { scope: ApiKeyScope; descricao: string }[] = [
  { scope: 'read', descricao: 'GETs (orders, parts, clientes, garantias)' },
  { scope: 'write', descricao: 'POSTs (checkout, cancel)' },
  { scope: 'ingest', descricao: 'Enviar faturas de fornecedor (n8n, email)' },
  { scope: 'conteudo', descricao: 'Redes sociais: criar publicações e ler vídeos — não aprova' },
  { scope: 'loja', descricao: 'Site lopestech.pt: ler o catálogo publicado da loja (sem custos)' },
  { scope: 'assistente', descricao: 'Assistente por voz/chat: age em teu nome (tarefas, rotina, agenda, leituras). Sem vendas, faturas nem apagar.' },
];

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
