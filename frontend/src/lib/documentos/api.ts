import { api } from '../api';
import type { DocumentosListDto } from './types';

interface ListVendasFilters {
  from?: string;
  to?: string;
  q?: string;
  tipo?: string;
}

export const documentosApi = {
  listVendas(filters: ListVendasFilters = {}) {
    return api.get<DocumentosListDto>('/documentos/vendas', { params: filters }).then((r) => r.data);
  },
  /** Caminho relativo para o export CSV (usar com downloadFile, que injecta auth + base URL). */
  exportVendasPath(from?: string, to?: string) {
    const p = new URLSearchParams();
    if (from) p.set('from', from);
    if (to) p.set('to', to);
    const qs = p.toString();
    return `/documentos/vendas/export.csv${qs ? `?${qs}` : ''}`;
  },
};
