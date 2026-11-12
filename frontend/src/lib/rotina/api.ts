import { api } from '../api';

// Sprint 615 (Doc 98): rotina pessoal — blocos, hábitos e limites com avisos no telemóvel.

export const RotinaTipo = {
  Refeicao: 1, Suplemento: 2, Meditacao: 3, Exercicio: 4, Trading: 5, Trabalho: 6, Limite: 7, Pessoal: 8, Peso: 9,
} as const;
export type RotinaTipo = (typeof RotinaTipo)[keyof typeof RotinaTipo];

export const RotinaEstado = { Feito: 1, Saltado: 2, Adiado: 3 } as const;
export type RotinaEstado = (typeof RotinaEstado)[keyof typeof RotinaEstado];

export const TIPO_INFO: Record<RotinaTipo, { label: string; icone: string; cor: string }> = {
  1: { label: 'Refeição', icone: '🍽️', cor: 'bg-amber-100 text-amber-800 dark:bg-amber-900/40 dark:text-amber-200' },
  2: { label: 'Suplemento', icone: '💊', cor: 'bg-lime-100 text-lime-800 dark:bg-lime-900/40 dark:text-lime-200' },
  3: { label: 'Meditação', icone: '🧘', cor: 'bg-violet-100 text-violet-800 dark:bg-violet-900/40 dark:text-violet-200' },
  4: { label: 'Exercício', icone: '🏋️', cor: 'bg-emerald-100 text-emerald-800 dark:bg-emerald-900/40 dark:text-emerald-200' },
  5: { label: 'Trading', icone: '📈', cor: 'bg-sky-100 text-sky-800 dark:bg-sky-900/40 dark:text-sky-200' },
  6: { label: 'Trabalho', icone: '💻', cor: 'bg-indigo-100 text-indigo-800 dark:bg-indigo-900/40 dark:text-indigo-200' },
  7: { label: 'Limite', icone: '🔴', cor: 'bg-rose-100 text-rose-800 dark:bg-rose-900/40 dark:text-rose-200' },
  8: { label: 'Pessoal', icone: '❤️', cor: 'bg-pink-100 text-pink-800 dark:bg-pink-900/40 dark:text-pink-200' },
  9: { label: 'Peso', icone: '⚖️', cor: 'bg-teal-100 text-teal-800 dark:bg-teal-900/40 dark:text-teal-200' },
};

/** Bit de cada dia = 1 << getDay() (domingo = 0), igual ao servidor. */
export const DIAS = [
  { bit: 2, curto: 'Seg' }, { bit: 4, curto: 'Ter' }, { bit: 8, curto: 'Qua' }, { bit: 16, curto: 'Qui' },
  { bit: 32, curto: 'Sex' }, { bit: 64, curto: 'Sáb' }, { bit: 1, curto: 'Dom' },
] as const;
export const TODOS_OS_DIAS = 127;

/** Sprint 635: alimento de uma refeição (macros da quantidade indicada). */
export interface RotinaAlimento {
  nome: string;
  quantidade: string | null;
  kcal: number;
  proteinaG: number;
  hidratosG: number;
  gorduraG: number;
}
export interface Macros { kcal: number; proteinaG: number; hidratosG: number; gorduraG: number }

export interface RotinaItem {
  id: string;
  titulo: string;
  detalhe: string | null;
  tipo: RotinaTipo;
  hora: string;
  duracaoMin: number;
  dias: number;
  intervaloDias: number | null;
  dataInicio: string | null;
  avisosAntes: string | null;
  insistir: boolean;
  avisarFim: boolean;
  ativo: boolean;
  alimentos?: RotinaAlimento[] | null;
  macros?: Macros | null;
}

export type GuardarRotinaItem = Omit<RotinaItem, 'id' | 'alimentos' | 'macros'>;

export interface RotinaDiaItem {
  item: RotinaItem;
  inicioUtc: string;
  fimUtc: string;
  estado: RotinaEstado | null;
  adiadoAte: string | null;
  motivo: string | null;
  valor: number | null;
  atrasado: boolean;
}

export interface RotinaDia {
  dia: string;
  agoraUtc: string;
  atualId: string | null;
  proximoId: string | null;
  itens: RotinaDiaItem[];
  feitos: number;
  devidos: number;
  meditacoesFeitas: number;
  meditacoesTotal: number;
  macrosPlaneados?: Macros | null;
  macrosComidos?: Macros | null;
}

export interface RegistarRotina {
  estado: RotinaEstado | null;
  motivo?: string | null;
  valor?: number | null;
  adiarMin?: number | null;
}

export interface RotinaDiaResumo {
  dia: string;
  total: number;
  feitos: number;
  saltados: number;
  percent: number;
  cumprido: boolean;
}

export interface RotinaHabito {
  itemId: string;
  titulo: string;
  tipo: RotinaTipo;
  sequenciaAtual: number;
  melhorSequencia: number;
  feitos30: number;
  previstos30: number;
}

export interface RotinaHistorico {
  dias: RotinaDiaResumo[];
  habitos: RotinaHabito[];
  diasCumpridosSeguidos: number;
  melhorSequenciaDias: number;
  percentSemana: number;
  percentMes: number;
}

export interface RotinaValor {
  dia: string;
  valor: number;
}

export const rotinaApi = {
  itens: () => api.get<RotinaItem[]>('/rotina/itens').then((r) => r.data),
  criar: (req: GuardarRotinaItem) => api.post<RotinaItem>('/rotina/itens', req).then((r) => r.data),
  atualizar: (id: string, req: GuardarRotinaItem) => api.put<RotinaItem>(`/rotina/itens/${id}`, req).then((r) => r.data),
  apagar: (id: string) => api.delete(`/rotina/itens/${id}`),
  valores: (id: string) => api.get<RotinaValor[]>(`/rotina/itens/${id}/valores`).then((r) => r.data),
  importar: (itens: GuardarRotinaItem[]) => api.post<RotinaItem[]>('/rotina/importar', itens).then((r) => r.data),
  alimentos: (id: string, alimentos: RotinaAlimento[]) => api.put<RotinaItem>(`/rotina/itens/${id}/alimentos`, alimentos).then((r) => r.data),
  importarAlimentos: (planos: { titulo: string; alimentos: RotinaAlimento[] }[]) =>
    api.post<{ refeicoes: number }>('/rotina/alimentos/importar', planos).then((r) => r.data),
  historico: (dias = 91) => api.get<RotinaHistorico>('/rotina/historico', { params: { dias } }).then((r) => r.data),
  dia: (dia?: string) => api.get<RotinaDia>('/rotina/dia', { params: dia ? { dia } : undefined }).then((r) => r.data),
  registar: (itemId: string, dia: string, req: RegistarRotina) =>
    api.post<RotinaDiaItem>(`/rotina/itens/${itemId}/dia/${dia}`, req).then((r) => r.data),
};

export function horaLocal(utc: string) {
  return new Date(utc).toLocaleTimeString('pt-PT', { hour: '2-digit', minute: '2-digit', timeZone: 'Europe/Lisbon' });
}

export function linhasDetalhe(detalhe: string | null) {
  return (detalhe ?? '').split('\n').map((l) => l.replace(/^[-•*]\s*/, '').trim()).filter(Boolean);
}
