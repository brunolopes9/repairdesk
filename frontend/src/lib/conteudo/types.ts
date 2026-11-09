// Sprint 598 (Doc 95): calendário de conteúdo das redes sociais.

export const ConteudoEstado = {
  Rascunho: 0,
  PorAprovar: 1,
  Aprovada: 2,
  APublicar: 3,
  Publicada: 4,
  ComFalhas: 5,
  Rejeitada: 6,
  Cancelada: 7,
} as const;
export type ConteudoEstado = (typeof ConteudoEstado)[keyof typeof ConteudoEstado];

export const ConteudoLinha = { Reparacoes: 0, Web: 1 } as const;
export type ConteudoLinha = (typeof ConteudoLinha)[keyof typeof ConteudoLinha];

export const ConteudoFormato = { Post: 0, Carrossel: 1, Reel: 2, Story: 3, Texto: 4 } as const;
export type ConteudoFormato = (typeof ConteudoFormato)[keyof typeof ConteudoFormato];

export const ConteudoOrigem = { Manual: 0, Claude: 1, Automatico: 2 } as const;
export type ConteudoOrigem = (typeof ConteudoOrigem)[keyof typeof ConteudoOrigem];

export const RedeSocial = { Facebook: 0, Instagram: 1, Threads: 2, TikTok: 3 } as const;
export type RedeSocial = (typeof RedeSocial)[keyof typeof RedeSocial];

export const ConteudoDestinoEstado = { Pendente: 0, AgendadoNaRede: 1, Publicado: 2, Falhou: 3 } as const;
export type ConteudoDestinoEstado = (typeof ConteudoDestinoEstado)[keyof typeof ConteudoDestinoEstado];

export const ConteudoFicheiroTipo = { Imagem: 0, Video: 1 } as const;
export type ConteudoFicheiroTipo = (typeof ConteudoFicheiroTipo)[keyof typeof ConteudoFicheiroTipo];

export const ConteudoFicheiroEstado = { AEnviar: 0, Pronto: 1 } as const;
export type ConteudoFicheiroEstado = (typeof ConteudoFicheiroEstado)[keyof typeof ConteudoFicheiroEstado];

export interface ConteudoFicheiro {
  id: string;
  publicacaoId: string | null;
  ordem: number;
  tipo: ConteudoFicheiroTipo;
  estado: ConteudoFicheiroEstado;
  nomeOriginal: string;
  contentType: string;
  bytes: number;
  largura: number | null;
  altura: number | null;
  duracaoSeg: number | null;
  notas: string | null;
  processadoEm: string | null;
  createdAt: string;
  url: string | null;
}

export interface ConteudoDestino {
  id: string;
  rede: RedeSocial;
  legendaPropria: string | null;
  estado: ConteudoDestinoEstado;
  externalId: string | null;
  permalink: string | null;
  tentativas: number;
  ultimoErro: string | null;
  publicadoEm: string | null;
}

export interface ConteudoPublicacao {
  id: string;
  numero: number;
  linha: ConteudoLinha;
  formato: ConteudoFormato;
  titulo: string;
  legenda: string | null;
  agendadoPara: string | null;
  estado: ConteudoEstado;
  aprovadoEm: string | null;
  motivoRejeicao: string | null;
  autoAprovada: boolean;
  origem: ConteudoOrigem;
  artigoId: string | null;
  notas: string | null;
  publicadaEm: string | null;
  createdAt: string;
  updatedAt: string | null;
  ficheiros: ConteudoFicheiro[];
  destinos: ConteudoDestino[];
}

export interface ConteudoResumo {
  porAprovar: number;
  bibliotecaPorProcessar: number;
}

export interface UploadTicket {
  ficheiroId: string;
  uploadUrl: string;
  metodo: 'PUT';
  contentType: string;
  expiraEm: string;
}

export const REDE_LABEL: Record<RedeSocial, string> = {
  [RedeSocial.Facebook]: 'Facebook',
  [RedeSocial.Instagram]: 'Instagram',
  [RedeSocial.Threads]: 'Threads',
  [RedeSocial.TikTok]: 'TikTok',
};

export const FORMATO_LABEL: Record<ConteudoFormato, string> = {
  [ConteudoFormato.Post]: 'Post',
  [ConteudoFormato.Carrossel]: 'Carrossel',
  [ConteudoFormato.Reel]: 'Reel',
  [ConteudoFormato.Story]: 'Story',
  [ConteudoFormato.Texto]: 'Texto',
};

export const LINHA_LABEL: Record<ConteudoLinha, string> = {
  [ConteudoLinha.Reparacoes]: 'Reparações e loja',
  [ConteudoLinha.Web]: 'Sites e software',
};
