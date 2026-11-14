import { api } from '../api';

// Sprint 661: subscritores da newsletter da loja (lopestech.pt). Só contam os que confirmaram pelo email.

export type EstadoSubscritor = 'ativo' | 'por_confirmar' | 'cancelado';

export interface SubscritorNewsletter {
  email: string;
  estado: EstadoSubscritor;
  origem: string | null;
  criadoEm: string;
  confirmadoEm: string | null;
  canceladoEm: string | null;
}

export interface NewsletterResumo {
  ativos: number;
  porConfirmar: number;
  cancelados: number;
  subscritores: SubscritorNewsletter[];
}

export const ESTADO_SUBSCRITOR_LABEL: Record<EstadoSubscritor, string> = {
  ativo: 'Ativo',
  por_confirmar: 'Por confirmar',
  cancelado: 'Cancelou',
};

export const newsletterApi = {
  resumo: () => api.get<NewsletterResumo>('/loja/newsletter').then((r) => r.data),
};
