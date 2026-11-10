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
  tikTokClientKey: string | null;
  tikTokClientSecretDefinido: boolean;
  tikTokPrivacidade: TikTokPrivacidade | null;
  redirectTikTok: string;
}

// Sprint 601: privacidade dos vídeos no TikTok (o dono escolhe — o TikTok não deixa haver valor por defeito).
export type TikTokPrivacidade = 'PUBLIC_TO_EVERYONE' | 'MUTUAL_FOLLOW_FRIENDS' | 'FOLLOWER_OF_CREATOR' | 'SELF_ONLY';
export const TIKTOK_PRIVACIDADE_LABEL: Record<TikTokPrivacidade, string> = {
  PUBLIC_TO_EVERYONE: 'Público (todos)',
  MUTUAL_FOLLOW_FRIENDS: 'Amigos',
  FOLLOWER_OF_CREATOR: 'Seguidores',
  SELF_ONLY: 'Só eu (privado)',
};

export interface GuardarAppsForm {
  metaAppId: string | null;
  metaAppSecret: string | null;
  threadsAppId: string | null;
  threadsAppSecret: string | null;
  metaLoginConfigId: string | null;
  tikTokClientKey: string | null;
  tikTokClientSecret: string | null;
  tikTokPrivacidade: TikTokPrivacidade | null;
}

export const redesSociaisApi = {
  estado: () => api.get<RedesSociaisEstado>('/redes').then((r) => r.data),
  guardarApps: (f: GuardarAppsForm) => api.put<RedesSociaisEstado>('/redes/apps', f).then((r) => r.data),
  autoPublicar: (ligado: boolean) => api.put<RedesSociaisEstado>('/redes/auto-publicar', { ligado }).then((r) => r.data),
  urlLigar: (plataforma: 'meta' | 'threads' | 'tiktok') =>
    api.get<{ url: string }>(`/redes/${plataforma}/url-ligar`).then((r) => r.data.url),
  ligarThreadsComToken: (token: string) =>
    api.put<RedesSociaisEstado>('/redes/threads/token', { token }).then((r) => r.data),
  desligar: (rede: RedeSocial) => api.delete<RedesSociaisEstado>(`/redes/ligacoes/${rede}`).then((r) => r.data),
};
