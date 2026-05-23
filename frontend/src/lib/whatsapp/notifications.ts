import { api } from '../api';
import type { WhatsAppNotificationStatus } from '../tenantPreferences/types';

export const whatsappNotificationsApi = {
  sent(params: { entityId: string; templateKey: string; entityType?: string }) {
    return api
      .get<WhatsAppNotificationStatus>('/whatsapp-notifications/sent', { params })
      .then((r) => r.data);
  },
  create(payload: { entityId: string; templateKey: string; entityType?: string; phone?: string; estado?: number | null }) {
    return api.post<WhatsAppNotificationStatus>('/whatsapp-notifications', payload).then((r) => r.data);
  },
};
