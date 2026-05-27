import axios from 'axios';

// Sprint 389: cliente HTTP sem JWT — widget público de marcação (booking online).
const baseURL = (import.meta.env.VITE_API_URL as string | undefined) ?? '/api';
const httpPublic = axios.create({ baseURL, withCredentials: false });

export interface BookingInfo {
  lojaNome: string;
  primaryColor: string | null;
}

export interface SubmitBookingPayload {
  nome: string;
  telefone?: string | null;
  email?: string | null;
  equipamento?: string | null;
  notas?: string | null;
  scheduledAt: string; // ISO UTC
  durationMin?: number;
  website?: string; // honeypot
}

export const bookingPublicApi = {
  info(slug: string) {
    return httpPublic.get<BookingInfo>(`/public/booking/${slug}`).then((r) => r.data);
  },
  submit(slug: string, payload: SubmitBookingPayload) {
    return httpPublic.post<{ ok: boolean }>(`/public/booking/${slug}`, payload).then((r) => r.data);
  },
};
