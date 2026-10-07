import { api } from '../api';

/** Sprint 452 (Doc 91 ponto 1): canal/tipo da comunicação. */
export const ComunicacaoTipo = {
  Nota: 0,
  Telefone: 1,
  WhatsApp: 2,
  Email: 3,
  Sms: 4,
  Visita: 5,
  PortalCliente: 6,
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
  vendaId: string;
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
  list: (vendaId: string) =>
    api.get<ReparacaoComunicacao[]>(`/vendas/${vendaId}/comunicacoes`).then((r) => r.data),
  create: (vendaId: string, form: CreateComunicacaoForm) =>
    api.post<ReparacaoComunicacao>(`/vendas/${vendaId}/comunicacoes`, form).then((r) => r.data),
  remove: (vendaId: string, id: string) =>
    api.delete(`/vendas/${vendaId}/comunicacoes/${id}`),
};

export const COMUNICACAO_TIPO_LABEL: Record<ComunicacaoTipo, string> = {
  [ComunicacaoTipo.Nota]: 'Nota',
  [ComunicacaoTipo.Telefone]: 'Telefone',
  [ComunicacaoTipo.WhatsApp]: 'WhatsApp',
  [ComunicacaoTipo.Email]: 'Email',
  [ComunicacaoTipo.Sms]: 'SMS',
  [ComunicacaoTipo.Visita]: 'Visita',
  [ComunicacaoTipo.PortalCliente]: 'Portal',
};

export const COMUNICACAO_DIRECAO_LABEL: Record<ComunicacaoDirecao, string> = {
  [ComunicacaoDirecao.Inbound]: 'Recebida',
  [ComunicacaoDirecao.Outbound]: 'Enviada',
  [ComunicacaoDirecao.Interna]: 'Interna',
};
