import axios from 'axios';
import { api } from '../api';

// O link do cliente é anónimo — cliente HTTP sem o token da equipa (como o portal).
const httpPublic = axios.create({ baseURL: (import.meta.env.VITE_API_URL as string | undefined) ?? '/api', withCredentials: false });

/** Sprint 595: como o equipamento se desbloqueia. */
export const SENHA_TIPO = { Nenhuma: 0, Pin: 1, Alfanumerica: 2, Padrao: 3 } as const;
export type SenhaTipo = (typeof SENHA_TIPO)[keyof typeof SENHA_TIPO];
export const SENHA_TIPO_LABEL: Record<SenhaTipo, string> = { 0: 'Sem senha', 1: 'PIN', 2: 'Letras e números', 3: 'Padrão (desenho)' };

/** "ok" funciona · "falha" não funciona · "nt" não testado. */
export type ResultadoTeste = 'ok' | 'falha' | 'nt';

export interface ChecklistItem {
  chave: string;
  label: string;
  entrada: ResultadoTeste | null;
  saida: ResultadoTeste | null;
}

export interface FichaReparacao {
  imei: string | null;
  senhaTipo: SenhaTipo;
  /** Em claro, só para a equipa; null depois de entregue. */
  senha: string | null;
  permiteTestes: boolean | null;
  estadoFisico: string | null;
  acessorios: string | null;
  checklist: ChecklistItem[];
  clienteAssinouEm: string | null;
  clienteAssinouPeloLink: boolean;
  tecnicoAssinouEm: string | null;
  linkAssinatura: string | null;
}

export type FichaReparacaoWrite = Omit<FichaReparacao, 'clienteAssinouEm' | 'clienteAssinouPeloLink' | 'tecnicoAssinouEm' | 'linkAssinatura'>;

/** A ordem de reparação como o cliente a vê no link (sem a senha). */
export interface PublicFicha {
  numero: string;
  loja: string;
  lojaContacto: string | null;
  cliente: string;
  equipamento: string;
  imei: string | null;
  avaria: string;
  senhaTipo: SenhaTipo;
  permiteTestes: boolean | null;
  estadoFisico: string | null;
  acessorios: string | null;
  checklist: ChecklistItem[];
  reparacao: { descricao: string; totalCents: number }[];
  totalCents: number;
  recebidoEm: string;
  previstoPara: string | null;
  termos: string | null;
  aviso: string;
  assinadaEm: string | null;
  podeAssinar: boolean;
}

export const fichaApi = {
  get(vendaId: string) {
    return api.get<FichaReparacao>(`/vendas/${vendaId}/ficha`).then((r) => r.data);
  },
  guardar(vendaId: string, ficha: FichaReparacaoWrite) {
    return api.put<FichaReparacao>(`/vendas/${vendaId}/ficha`, ficha).then((r) => r.data);
  },
  ordemPdfPath(vendaId: string) {
    return `/vendas/${vendaId}/ordem.pdf`;
  },
  publica(slug: string) {
    return httpPublic.get<PublicFicha>(`/public/repair/${encodeURIComponent(slug)}/ficha`).then((r) => r.data);
  },
  assinar(slug: string, dataUrl: string) {
    return httpPublic.post<PublicFicha>(`/public/repair/${encodeURIComponent(slug)}/assinar`, { dataUrl, aceitaTermos: true }).then((r) => r.data);
  },
  /** URL direto (anónimo) da via do cliente em PDF. */
  publicaPdfUrl(slug: string) {
    return `${(import.meta.env.VITE_API_URL as string | undefined) ?? '/api'}/public/repair/${encodeURIComponent(slug)}/ordem.pdf`;
  },
};
