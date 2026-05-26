import axios from 'axios';

// Cliente HTTP sem JWT — widget público de pedido de reparação.
const baseURL = (import.meta.env.VITE_API_URL as string | undefined) ?? '/api';
const httpPublic = axios.create({ baseURL, withCredentials: false });

export interface WidgetInfo {
  lojaNome: string;
  primaryColor: string | null;
}

export interface SubmitPayload {
  nome: string;
  email?: string | null;
  telefone?: string | null;
  equipamento: string;
  descricao: string;
  website?: string; // honeypot
}

export const repairRequestPublicApi = {
  info(slug: string) {
    return httpPublic.get<WidgetInfo>(`/public/repair-requests/${slug}`).then((r) => r.data);
  },
  submit(slug: string, payload: SubmitPayload) {
    return httpPublic.post<{ ok: boolean }>(`/public/repair-requests/${slug}`, payload).then((r) => r.data);
  },
};
