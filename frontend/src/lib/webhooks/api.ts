import { api } from '../api';

export interface WebhookSubscription {
  id: string;
  name: string;
  url: string;
  events: string[];
  active: boolean;
  lastDeliveryAt: string | null;
  failureCount: number;
  disabledAt: string | null;
  createdAt: string;
}

export interface CreateWebhookSubscriptionRequest {
  name: string;
  url: string;
  events: string[];
}

export interface UpdateWebhookSubscriptionRequest extends CreateWebhookSubscriptionRequest {
  active: boolean;
}

export interface CreateWebhookSubscriptionResponse {
  subscription: WebhookSubscription;
  /** Devolvido UMA VEZ — usar para verificar HMAC dos POSTs do RepairDesk. */
  secret: string;
}

export const webhooksApi = {
  list() {
    return api.get<WebhookSubscription[]>('/webhooks').then((r) => r.data);
  },
  events() {
    return api.get<string[]>('/webhooks/events').then((r) => r.data);
  },
  create(req: CreateWebhookSubscriptionRequest) {
    return api.post<CreateWebhookSubscriptionResponse>('/webhooks', req).then((r) => r.data);
  },
  update(id: string, req: UpdateWebhookSubscriptionRequest) {
    return api.put<WebhookSubscription>(`/webhooks/${id}`, req).then((r) => r.data);
  },
  remove(id: string) {
    return api.delete(`/webhooks/${id}`).then(() => undefined);
  },
};
