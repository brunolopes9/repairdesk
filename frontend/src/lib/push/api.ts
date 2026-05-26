import { api } from '../api';
import { publicPortalApi, type BrowserPushSubscriptionPayload } from '../publicPortal/api';

export interface PushResultDto {
  subscribed: boolean;
}

/**
 * Sprint 366: push de STAFF (dispositivo do utilizador autenticado). A chave VAPID
 * pública é a mesma do portal do cliente (é pública), por isso reutilizamos o endpoint
 * anónimo. Subscribe/unsubscribe são autenticados.
 */
export const staffPushApi = {
  getVapidPublicKey() {
    return publicPortalApi.getVapidPublicKey();
  },
  subscribe(subscription: BrowserPushSubscriptionPayload) {
    return api.post<PushResultDto>('/push/subscribe', subscription).then((r) => r.data);
  },
  unsubscribe(endpoint: string) {
    return api.post<PushResultDto>('/push/unsubscribe', { endpoint }).then((r) => r.data);
  },
};
