import { api } from '../api';
import type { RedeSocial } from '../conteudo/types';

// Sprint 599 (Doc 95 Fase B): ligação das contas e publicação automática.

export interface RedeLigada {
  rede: RedeSocial;
  contaNome: string;
  contaId: string;
  ligadaEm: string;
  tokenExpiraEm: string | null;
  valida: boolean;
  ultimoErro: string | null;
}

export interface RedesSociaisEstado {
  metaAppId: string | null;
  metaAppSecretDefinido: boolean;
  metaLoginConfigId: string | null;
  threadsAppId: string | null;
  threadsAppSecretDefinido: boolean;
  autoPublicar: boolean;
  redirectMeta: string;
  redirectThreads: string;
  ligacoes: RedeLigada[];
}

export interface GuardarAppsForm {
  metaAppId: string | null;
  metaAppSecret: string | null;
  threadsAppId: string | null;
  threadsAppSecret: string | null;
  metaLoginConfigId: string | null;
}

export const redesSociaisApi = {
  estado: () => api.get<RedesSociaisEstado>('/redes').then((r) => r.data),
  guardarApps: (f: GuardarAppsForm) => api.put<RedesSociaisEstado>('/redes/apps', f).then((r) => r.data),
  autoPublicar: (ligado: boolean) => api.put<RedesSociaisEstado>('/redes/auto-publicar', { ligado }).then((r) => r.data),
  urlLigar: (plataforma: 'meta' | 'threads') =>
    api.get<{ url: string }>(`/redes/${plataforma}/url-ligar`).then((r) => r.data.url),
  ligarThreadsComToken: (token: string) =>
    api.put<RedesSociaisEstado>('/redes/threads/token', { token }).then((r) => r.data),
  desligar: (rede: RedeSocial) => api.delete<RedesSociaisEstado>(`/redes/ligacoes/${rede}`).then((r) => r.data),
};
