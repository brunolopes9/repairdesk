import { api } from '../api';

// Sprint 619 (Doc 98 F4): calendários de fora (Google/Outlook/iCloud) vistos no Mender — só leitura.

export interface CalendarioExterno {
  id: string;
  nome: string;
  dominio: string;
  ativo: boolean;
  ultimaLeitura: string | null;
  ultimoErro: string | null;
}

export interface EventoExterno {
  calendarioId: string;
  calendario: string;
  titulo: string;
  inicioUtc: string;
  fimUtc: string;
  diaInteiro: boolean;
  local: string | null;
}

export interface EventosExternos {
  eventos: EventoExterno[];
  calendarios: CalendarioExterno[];
}

export const calendarioApi = {
  list: () => api.get<CalendarioExterno[]>('/calendarios-externos').then((r) => r.data),
  ligar: (nome: string, url: string) => api.post<CalendarioExterno>('/calendarios-externos', { nome, url }).then((r) => r.data),
  remover: (id: string) => api.delete(`/calendarios-externos/${id}`),
  eventos: (de: string, ate: string) =>
    api.get<EventosExternos>('/calendarios-externos/eventos', { params: { de, ate } }).then((r) => r.data),
};
