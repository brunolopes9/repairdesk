import { api } from '../api';

/** Sprint 452 (Doc 91 ponto 1): canal/tipo da comunicação. */
export const ComunicacaoTipo = {
  Nota: 0,
  Telefone: 1,
  WhatsApp: 2,
  Email: 3,
  Sms: 4,
  Visita: 5,
} as const;
export type ComunicacaoTipo = (typeof ComunicacaoTipo)[keyof typeof ComunicacaoTipo];

export const ComunicacaoDirecao = {
  Inbound: 0,
  Outbound: 1,
  Interna: 2,
} as const;
export type ComunicacaoDirecao = (typeof ComunicacaoDirecao)[keyof typeof ComunicacaoDirecao];

export interface ReparacaoComunicacao {
  id: string;
  reparacaoId: string;
  clienteId: string;
  tipo: ComunicacaoTipo;
  direcao: ComunicacaoDirecao;
  texto: string;
  createdByUserId: string;
  createdAt: string;
}

export interface CreateComunicacaoForm {
  tipo: ComunicacaoTipo;
  direcao: ComunicacaoDirecao;
  texto: string;
}

export const comunicacoesApi = {
  list: (reparacaoId: string) =>
    api.get<ReparacaoComunicacao[]>(`/reparacoes/${reparacaoId}/comunicacoes`).then((r) => r.data),
  create: (reparacaoId: string, form: CreateComunicacaoForm) =>
    api.post<ReparacaoComunicacao>(`/reparacoes/${reparacaoId}/comunicacoes`, form).then((r) => r.data),
  remove: (reparacaoId: string, id: string) =>
    api.delete(`/reparacoes/${reparacaoId}/comunicacoes/${id}`),
};

export const COMUNICACAO_TIPO_LABEL: Record<ComunicacaoTipo, string> = {
  [ComunicacaoTipo.Nota]: 'Nota',
  [ComunicacaoTipo.Telefone]: 'Telefone',
  [ComunicacaoTipo.WhatsApp]: 'WhatsApp',
  [ComunicacaoTipo.Email]: 'Email',
  [ComunicacaoTipo.Sms]: 'SMS',
  [ComunicacaoTipo.Visita]: 'Visita',
};

export const COMUNICACAO_DIRECAO_LABEL: Record<ComunicacaoDirecao, string> = {
  [ComunicacaoDirecao.Inbound]: 'Recebida',
  [ComunicacaoDirecao.Outbound]: 'Enviada',
  [ComunicacaoDirecao.Interna]: 'Interna',
};
