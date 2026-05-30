import { api } from '../api';

/**
 * Sprint 461+462 (Doc 90 Tier 2 #6): asset registry — equipamento persistente do cliente.
 * Difere de ClienteEquipamentoDto (derived de reparações/vendas): este vive entre reparações,
 * pode existir sem reparação, e tem campos próprios (Apelido, GarantiaFabricanteUntil, etc).
 */
export interface Device {
  id: string;
  clienteId: string;
  tipo: string;
  marca: string | null;
  modelo: string | null;
  apelido: string | null;
  imei: string | null;
  serial: string | null;
  cor: string | null;
  dataAquisicao: string | null;          // ISO date "yyyy-MM-dd"
  garantiaFabricanteUntil: string | null; // ISO date
  notas: string | null;
  arquivado: boolean;
  createdAt: string;
}

export interface CreateDeviceForm {
  clienteId: string;
  tipo: string;
  marca?: string | null;
  modelo?: string | null;
  apelido?: string | null;
  imei?: string | null;
  serial?: string | null;
  cor?: string | null;
  dataAquisicao?: string | null;
  garantiaFabricanteUntil?: string | null;
  notas?: string | null;
}

export interface UpdateDeviceForm {
  tipo: string;
  marca?: string | null;
  modelo?: string | null;
  apelido?: string | null;
  imei?: string | null;
  serial?: string | null;
  cor?: string | null;
  dataAquisicao?: string | null;
  garantiaFabricanteUntil?: string | null;
  notas?: string | null;
  arquivado: boolean;
}

/**
 * Sprint 464: lookup por IMEI devolvido pelo endpoint /devices/by-imei/{imei}.
 * Não inclui campos sensíveis tipo Notas — só o suficiente para sugerir cliente
 * num modal de nova reparação.
 */
export interface DeviceByImei {
  id: string;
  clienteId: string;
  clienteNome: string;
  tipo: string;
  marca: string | null;
  modelo: string | null;
  apelido: string | null;
  cor: string | null;
  arquivado: boolean;
}

export const devicesApi = {
  listByCliente(clienteId: string, incluirArquivados = false) {
    return api
      .get<Device[]>('/devices', { params: { clienteId, incluirArquivados } })
      .then((r) => r.data);
  },
  /** Sprint 464: lookup por IMEI. Backend devolve 204 quando inexistente → mapeamos para null. */
  byImei(imei: string): Promise<DeviceByImei | null> {
    return api.get<DeviceByImei>(`/devices/by-imei/${encodeURIComponent(imei)}`)
      .then((r) => (r.status === 204 ? null : r.data || null))
      .catch(() => null);
  },
  get(id: string) {
    return api.get<Device>(`/devices/${id}`).then((r) => r.data);
  },
  create(form: CreateDeviceForm) {
    return api.post<Device>('/devices', form).then((r) => r.data);
  },
  update(id: string, form: UpdateDeviceForm) {
    return api.put<Device>(`/devices/${id}`, form).then((r) => r.data);
  },
  remove(id: string) {
    return api.delete(`/devices/${id}`);
  },
};
