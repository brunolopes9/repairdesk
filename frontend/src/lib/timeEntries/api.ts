import { api } from '../api';

export interface TimeEntryDto {
  id: string;
  reparacaoId: string;
  userId: string;
  startedAt: string;
  endedAt: string | null;
  duracaoMinutos: number | null;
  notas: string | null;
}

export interface ActiveTimerDto {
  id: string;
  reparacaoId: string;
  reparacaoNumero: number;
  startedAt: string;
}

export interface TimeStatsRow {
  userId: string;
  /** Sprint 350: DisplayName resolvido server-side (com fallback para email). */
  displayName: string;
  totalMinutos: number;
  sessoes: number;
  reparacoes: number;
}

export const timeEntriesApi = {
  active() {
    // Sprint 358: backend devolve 204 (sem body) quando não há timer activo.
    return api.get<ActiveTimerDto | null>('/time-entries/active').then((r) =>
      r.status === 204 || !r.data ? null : r.data,
    );
  },
  byReparacao(reparacaoId: string) {
    return api.get<TimeEntryDto[]>(`/time-entries/by-reparacao/${reparacaoId}`).then((r) => r.data);
  },
  start(reparacaoId: string, notas?: string) {
    return api.post<TimeEntryDto>('/time-entries/start', { reparacaoId, notas: notas ?? null }).then((r) => r.data);
  },
  stop(id: string, notas?: string) {
    return api.post<TimeEntryDto>(`/time-entries/${id}/stop`, { notas: notas ?? null }).then((r) => r.data);
  },
  delete(id: string) {
    return api.delete<void>(`/time-entries/${id}`).then((r) => r.data);
  },
  stats(fromIso: string, toIso: string) {
    return api.get<TimeStatsRow[]>(`/time-entries/stats?from=${encodeURIComponent(fromIso)}&to=${encodeURIComponent(toIso)}`).then((r) => r.data);
  },
};
