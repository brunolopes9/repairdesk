import { api } from '../api';

// 23/11: Loja online → Visitas — medição própria do lopestech.pt (js/visitas.js).

export interface Contagem {
  nome: string;
  total: number;
  extra: number | null;
  media: number | null;
}

export interface DiaVisitas {
  dia: string;
  visitas: number;
  visitantes: number;
  paginas: number;
  compras: number;
}

export interface Funil {
  visitas: number;
  viramProduto: number;
  carrinho: number;
  checkout: number;
  pagamento: number;
  compras: number;
  valorComprado: number;
}

export interface VisitasResumo {
  visitantes: number;
  visitas: number;
  paginas: number;
  tempoMedioSeg: number;
  visitasDeUmaPagina: number;
  regressaram: number;
  funil: Funil;
  porDia: DiaVisitas[];
  topPaginas: Contagem[];
  entradas: Contagem[];
  saidas: Contagem[];
  pesquisas: Contagem[];
  pesquisasSemResultados: Contagem[];
  produtos: Contagem[];
  carrinhos: Contagem[];
  origens: Contagem[];
  campanhas: Contagem[];
  dispositivos: Contagem[];
  contactos: Contagem[];
  truncado: boolean;
}

export interface SessaoVisita {
  sessao: string;
  visitante: string;
  inicio: string;
  fim: string;
  duracaoSeg: number;
  paginas: number;
  origem: string | null;
  campanha: string | null;
  dispositivo: string | null;
  sistema: string | null;
  cliente: string | null;
  entrada: string | null;
  saida: string | null;
  pesquisas: string[];
  produtos: string[];
  carrinho: boolean;
  checkout: boolean;
  pagamento: boolean;
  comprou: boolean;
  valor: number | null;
  regressou: boolean;
}

export interface EventoVisita {
  em: string;
  tipo: string;
  caminho: string | null;
  titulo: string | null;
  termo: string | null;
  resultados: number | null;
  produto: string | null;
  valor: number | null;
  duracao: number | null;
  scroll: number | null;
  encomenda: string | null;
  origem: string | null;
  referrer: string | null;
}

const qs = (de: string, ate: string) => `de=${encodeURIComponent(de)}&ate=${encodeURIComponent(ate)}`;

export const visitasApi = {
  resumo: (de: string, ate: string) => api.get<VisitasResumo>(`/loja/visitas/resumo?${qs(de, ate)}`).then((r) => r.data),
  sessoes: (de: string, ate: string) => api.get<SessaoVisita[]>(`/loja/visitas/sessoes?${qs(de, ate)}`).then((r) => r.data),
  /** «Fui eu»: apaga esta visita e todas as do mesmo visitante. */
  naoContar: (sessao: string) => api.delete<{ apagados: number }>(`/loja/visitas/sessoes/${encodeURIComponent(sessao)}`).then((r) => r.data),
  sessao: (sessao: string) => api.get<EventoVisita[]>(`/loja/visitas/sessoes/${encodeURIComponent(sessao)}`).then((r) => r.data),
};
