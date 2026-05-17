import { api } from '../api';
import { DEVICE_CATEGORY, type DeviceCategory } from '../diagnostico/api';

export { DEVICE_CATEGORY, DEVICE_CATEGORY_LABEL, type DeviceCategory } from '../diagnostico/api';

export interface PriceTableEntry {
  id: string;
  categoria: DeviceCategory;
  marca: string;
  modelo: string;
  servico: string;
  custoPecaCents: number | null;
  pvpCents: number;
  tempoEstimadoMin: number | null;
  notas: string | null;
  activo: boolean;
  margemPct: number | null;
}

export interface PriceTablePage {
  items: PriceTableEntry[];
  page: number;
  pageSize: number;
  total: number;
}

export interface CreatePriceEntryForm {
  categoria: DeviceCategory;
  marca: string;
  modelo: string;
  servico: string;
  custoPecaCents: number | null;
  pvpCents: number;
  tempoEstimadoMin: number | null;
  notas: string | null;
}

export interface UpdatePriceEntryForm extends CreatePriceEntryForm {
  activo: boolean;
}

export interface ImportPriceError {
  linha: number;
  campo: string;
  mensagem: string;
  valorOriginal: string | null;
}

export interface ImportPriceTableResponse {
  totalLinhas: number;
  criadas: number;
  ignoradas: number;
  comErro: number;
  erros: ImportPriceError[];
}

export const precosApi = {
  list(filters: { q?: string; categoria?: DeviceCategory | null; marca?: string | null; page?: number; pageSize?: number } = {}) {
    return api
      .get<PriceTablePage>('/price-table', {
        params: {
          q: filters.q || undefined,
          categoria: filters.categoria ?? undefined,
          marca: filters.marca || undefined,
          page: filters.page ?? 1,
          pageSize: filters.pageSize ?? 50,
        },
      })
      .then((r) => r.data);
  },
  marcas() {
    return api.get<string[]>('/price-table/marcas').then((r) => r.data);
  },
  get(id: string) {
    return api.get<PriceTableEntry>(`/price-table/${id}`).then((r) => r.data);
  },
  create(form: CreatePriceEntryForm) {
    return api.post<PriceTableEntry>('/price-table', form).then((r) => r.data);
  },
  update(id: string, form: UpdatePriceEntryForm) {
    return api.put<PriceTableEntry>(`/price-table/${id}`, form).then((r) => r.data);
  },
  remove(id: string) {
    return api.delete(`/price-table/${id}`).then(() => undefined);
  },
  importCsv(csv: string) {
    return api.post<ImportPriceTableResponse>('/price-table/import', { csv }).then((r) => r.data);
  },
};

// Avoid unused import in some bundlers
void DEVICE_CATEGORY;
