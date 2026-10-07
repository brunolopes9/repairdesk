import { api } from '../api';
import type {
  CompraDocumento,
  CompraDocumentoWrite,
  CompraFiltro,
  ImportComprasResultado,
  InventarioLinha,
  Paged,
  ResumoCompras,
  SimuladorRequest,
  SimuladorResponse,
} from './types';

export const comprasApi = {
  search(filtro: CompraFiltro) {
    return api.get<Paged<CompraDocumento>>('/compras', { params: filtro }).then((r) => r.data);
  },
  get(id: string) {
    return api.get<CompraDocumento>(`/compras/${id}`).then((r) => r.data);
  },
  create(req: CompraDocumentoWrite) {
    return api.post<CompraDocumento>('/compras', req).then((r) => r.data);
  },
  /** Aprova uma fatura recebida (lida por IA) como compra. */
  createFromImport(importId: string, req: CompraDocumentoWrite) {
    return api.post<CompraDocumento>(`/compras/de-fatura/${importId}`, req).then((r) => r.data);
  },
  update(id: string, req: CompraDocumentoWrite) {
    return api.put<CompraDocumento>(`/compras/${id}`, req).then((r) => r.data);
  },
  remove(id: string) {
    return api.delete(`/compras/${id}`).then(() => undefined);
  },
  inventario() {
    return api.get<InventarioLinha[]>('/compras/inventario').then((r) => r.data);
  },
  resumo() {
    return api.get<ResumoCompras>('/compras/resumo').then((r) => r.data);
  },
  simular(req: SimuladorRequest) {
    return api.post<SimuladorResponse>('/compras/simulador', req).then((r) => r.data);
  },
  importarExcel(ficheiro: File) {
    const form = new FormData();
    form.append('ficheiro', ficheiro);
    return api.post<ImportComprasResultado>('/compras/importar-excel', form).then((r) => r.data);
  },
};
