import { api } from '../api';

export type AppointmentStatus = 'Agendado' | 'Confirmado' | 'Concluido' | 'Cancelado' | 'NaoCompareceu';

export interface Appointment {
  id: string;
  clienteId: string | null;
  nome: string;
  telefone: string | null;
  email: string | null;
  equipamento: string | null;
  notas: string | null;
  scheduledAt: string; // ISO UTC
  durationMin: number;
  status: AppointmentStatus;
  source: 'Balcao' | 'Online';
}

export interface CreateAppointmentRequest {
  clienteId?: string | null;
  nome: string;
  telefone?: string | null;
  email?: string | null;
  equipamento?: string | null;
  notas?: string | null;
  scheduledAt: string; // ISO
  durationMin?: number | null;
}

export const APPOINTMENT_STATUS_LABEL: Record<AppointmentStatus, string> = {
  Agendado: 'Agendado',
  Confirmado: 'Confirmado',
  Concluido: 'Concluído',
  Cancelado: 'Cancelado',
  NaoCompareceu: 'Não compareceu',
};

export const appointmentsApi = {
  list(fromIso: string, toIso: string) {
    return api
      .get<Appointment[]>('/appointments', { params: { from: fromIso, to: toIso } })
      .then((r) => r.data);
  },
  create(req: CreateAppointmentRequest) {
    return api.post<Appointment>('/appointments', req).then((r) => r.data);
  },
  updateStatus(id: string, status: AppointmentStatus) {
    return api.patch<Appointment>(`/appointments/${id}/status`, { status }).then((r) => r.data);
  },
  reschedule(id: string, scheduledAt: string, durationMin?: number) {
    return api.patch<Appointment>(`/appointments/${id}/reschedule`, { scheduledAt, durationMin }).then((r) => r.data);
  },
};
