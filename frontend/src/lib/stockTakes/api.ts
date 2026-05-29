import { api } from '../api';
import type { StockTake, StockTakeItem } from './types';

export const stockTakesApi = {
  current: () =>
    api
      .get<StockTake>('/stock-takes/current', { validateStatus: (s) => (s >= 200 && s < 300) || s === 204 })
      .then((r) => (r.status === 204 ? null : r.data)),

  list: (take = 20) => api.get<StockTake[]>('/stock-takes', { params: { take } }).then((r) => r.data),

  get: (id: string) => api.get<StockTake>(`/stock-takes/${id}`).then((r) => r.data),

  open: () => api.post<StockTake>('/stock-takes').then((r) => r.data),

  count: (id: string, partId: string, qtdContada: number) =>
    api.put<StockTakeItem>(`/stock-takes/${id}/items/${partId}`, { qtdContada }).then((r) => r.data),

  close: (id: string, notas?: string | null) =>
    api.post<StockTake>(`/stock-takes/${id}/close`, { notas: notas ?? null }).then((r) => r.data),

  cancel: (id: string) => api.post<StockTake>(`/stock-takes/${id}/cancel`).then((r) => r.data),

  // Sprint 434: caminho relativo para download CSV via downloadFile helper.
  exportCsvPath: (id: string) => `/stock-takes/${id}/export.csv`,
};
