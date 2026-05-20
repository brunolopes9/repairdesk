import { api } from '../api';

export interface Fornecedor {
  id: string;
  name: string;
  email: string | null;
  rmaEmail: string | null;
  phone: string | null;
  website: string | null;
  garantiaB2BDiasDefault: number | null;
  notas: string | null;
  active: boolean;
  createdAt: string;
}

export interface FornecedorWriteRequest {
  name: string;
  email?: string | null;
  rmaEmail?: string | null;
  phone?: string | null;
  website?: string | null;
  garantiaB2BDiasDefault?: number | null;
  notas?: string | null;
  active: boolean;
}

export const fornecedoresApi = {
  list(includeInactive = false) {
    return api.get<Fornecedor[]>('/fornecedores', { params: { includeInactive } }).then((r) => r.data);
  },
  create(req: FornecedorWriteRequest) {
    return api.post<Fornecedor>('/fornecedores', req).then((r) => r.data);
  },
  update(id: string, req: FornecedorWriteRequest) {
    return api.put<Fornecedor>(`/fornecedores/${id}`, req).then((r) => r.data);
  },
  remove(id: string) {
    return api.delete(`/fornecedores/${id}`).then(() => undefined);
  },
};
