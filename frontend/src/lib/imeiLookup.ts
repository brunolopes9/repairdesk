import { api } from './api';

export interface TacLookupResult {
  tac: string;
  brand: string | null;
  model: string | null;
  found: boolean;
}

// Sprint 390 (Doc 04): resolve marca+modelo a partir do IMEI (TAC local, offline).
export function imeiLookup(imei: string) {
  return api.get<TacLookupResult>('/imei/lookup', { params: { imei } }).then((r) => r.data);
}

export const tacDbApi = {
  status() {
    return api.get<{ count: number }>('/imei/tac-db/status').then((r) => r.data);
  },
  import(file: File) {
    const fd = new FormData();
    fd.append('file', file);
    return api.post<{ count: number }>('/imei/tac-db/import', fd).then((r) => r.data);
  },
};
