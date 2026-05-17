import axios from 'axios';

// Cliente HTTP separado do `api` autenticado — sem interceptor de token JWT
// (endpoint é anonymous). Aponta para o mesmo backend.
const baseURL = (import.meta.env.VITE_API_URL as string | undefined) ?? '/api';
const httpPublic = axios.create({ baseURL, withCredentials: false });

export const PUBLIC_ESTADO = {
  Orcamento: 0,
  Recebido: 1,
  EmAnalise: 2,
  EmReparacao: 3,
  AguardaPeca: 4,
  Pronto: 5,
  Entregue: 6,
  Cancelado: 7,
} as const;

export type PublicEstado = (typeof PUBLIC_ESTADO)[keyof typeof PUBLIC_ESTADO];

export const ESTADO_LABEL: Record<PublicEstado, string> = {
  0: 'Aguarda aprovação',
  1: 'Recebido na loja',
  2: 'Em análise',
  3: 'Em reparação',
  4: 'À espera de peça',
  5: 'Pronto para levantar',
  6: 'Entregue',
  7: 'Cancelado',
};

export const ESTADO_DESC: Record<PublicEstado, string> = {
  0: 'A loja está a aguardar a tua aprovação do orçamento.',
  1: 'O teu equipamento entrou na oficina. Em breve será analisado.',
  2: 'O técnico está a diagnosticar o problema.',
  3: 'A reparação está em curso.',
  4: 'A loja encomendou uma peça. Aguarda chegada.',
  5: '🎉 O teu equipamento está pronto. Podes passar para o levantar.',
  6: 'Reparação concluída. Obrigado pela confiança!',
  7: 'Esta reparação foi cancelada.',
};

// Ordem dos estados na timeline visual
export const STEPS: PublicEstado[] = [
  PUBLIC_ESTADO.Recebido,
  PUBLIC_ESTADO.EmAnalise,
  PUBLIC_ESTADO.EmReparacao,
  PUBLIC_ESTADO.Pronto,
  PUBLIC_ESTADO.Entregue,
];

export interface PublicLoja {
  nome: string;
  telefone: string | null;
  email: string | null;
  website: string | null;
  logoUrl: string | null;
}

export interface PublicTimelineEntry {
  estado: PublicEstado;
  mudouEm: string;
}

export interface PublicRepairDto {
  slug: string;
  equipamentoPublico: string;
  avariaPublica: string;
  diagnostico: string | null;
  estado: PublicEstado;
  estadoSince: string;
  recebidoEm: string;
  entregueEm: string | null;
  orcamentoCents: number | null;
  orcamentoAprovado: boolean;
  temPrecoFinal: boolean;
  precoFinalCents: number | null;
  loja: PublicLoja;
  clientePrimeiroNome: string;
  timeline: PublicTimelineEntry[];
  healthScore: number | null;
  diagnosticoDestaques: string[];
  garantiaSlug: string | null;
  jaAvaliado: boolean;
  fotos: PublicFotoDto[];
}

export interface PublicFotoDto {
  id: string;
  tipo: number; // 0=Antes, 1=Durante, 2=Depois
  legenda: string | null;
  criadaEm: string;
}

export interface PublicGarantiaDto {
  slug: string;
  equipamentoPublico: string;
  loja: string;
  logoUrl: string | null;
  dataInicio: string;
  dataFim: string;
  diasGarantia: number;
  activa: boolean;
  anulada: boolean;
  diasRestantes: number;
  cobertura: string | null;
  exclusoes: string | null;
}

export interface AvaliacaoSubmittedDto {
  score: number;
  comentario: string | null;
  googleReviewUrl: string | null;
}

export const publicPortalApi = {
  get(slug: string) {
    return httpPublic.get<PublicRepairDto>(`/public/repair/${slug}`).then((r) => r.data);
  },
  aprovarOrcamento(slug: string, aceitar: boolean) {
    return httpPublic
      .post<PublicRepairDto>(`/public/repair/${slug}/orcamento`, { aceitar })
      .then((r) => r.data);
  },
  submeterAvaliacao(slug: string, score: number, comentario: string | null, publicarTestemunho: boolean) {
    return httpPublic
      .post<AvaliacaoSubmittedDto>(`/public/repair/${slug}/avaliacao`, { score, comentario, publicarTestemunho })
      .then((r) => r.data);
  },
  getGarantia(slug: string) {
    return httpPublic.get<PublicGarantiaDto>(`/public/warranty/${slug}`).then((r) => r.data);
  },
};
