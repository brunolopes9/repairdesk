import { api } from '../api';

export interface ServiceItem {
  id: string;
  nome: string;
  descricao: string | null;
  precoCents: number;
  garantiaDiasCliente: number;
  activo: boolean;
}

export interface CreateOrUpdateServiceItemRequest {
  nome: string;
  descricao: string | null;
  precoCents: number;
  garantiaDiasCliente: number;
  activo: boolean;
}

export const servicesApi = {
  list: (includeInactive = false) =>
    api.get<ServiceItem[]>('/services', { params: { includeInactive } }).then((r) => r.data),

  get: (id: string) => api.get<ServiceItem>(`/services/${id}`).then((r) => r.data),

  create: (form: CreateOrUpdateServiceItemRequest) =>
    api.post<ServiceItem>('/services', form).then((r) => r.data),

  update: (id: string, form: CreateOrUpdateServiceItemRequest) =>
    api.put<ServiceItem>(`/services/${id}`, form).then((r) => r.data),

  remove: (id: string) => api.delete(`/services/${id}`),
};
