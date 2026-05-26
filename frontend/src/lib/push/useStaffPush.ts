import { useCallback, useEffect, useMemo, useState } from 'react';
import type { BrowserPushSubscriptionPayload } from '../publicPortal/api';
import { staffPushApi } from './api';

type PushStatus = 'checking' | 'unsupported' | 'idle' | 'subscribed' | 'denied' | 'busy' | 'error';

function urlBase64ToUint8Array(value: string) {
  const padding = '='.repeat((4 - (value.length % 4)) % 4);
  const base64 = (value + padding).replace(/-/g, '+').replace(/_/g, '/');
  const raw = window.atob(base64);
  const output = new Uint8Array(raw.length);
  for (let i = 0; i < raw.length; i++) output[i] = raw.charCodeAt(i);
  return output;
}

function toPayload(subscription: PushSubscription): BrowserPushSubscriptionPayload {
  const json = subscription.toJSON() as BrowserPushSubscriptionPayload;
  return {
    endpoint: json.endpoint ?? subscription.endpoint,
    expirationTime: json.expirationTime ?? null,
    keys: { p256dh: json.keys?.p256dh ?? '', auth: json.keys?.auth ?? '' },
  };
}

/**
 * Sprint 366: subscrição de Web Push para ESTE dispositivo de staff. Liga o telemóvel/
 * desktop do utilizador às notificações internas (pedido online, etc.). O service worker
 * (push-sw.js, Sprint 365) é quem mostra a notificação.
 */
export function useStaffPush() {
  const supported = useMemo(
    () => typeof window !== 'undefined'
      && 'serviceWorker' in navigator
      && 'PushManager' in window
      && 'Notification' in window,
    [],
  );
  const [status, setStatus] = useState<PushStatus>(supported ? 'checking' : 'unsupported');
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    async function check() {
      if (!supported) return;
      if (Notification.permission === 'denied') {
        if (!cancelled) setStatus('denied');
        return;
      }
      try {
        const registration = await navigator.serviceWorker.ready;
        const existing = await registration.pushManager.getSubscription();
        if (!cancelled) setStatus(existing ? 'subscribed' : 'idle');
      } catch {
        if (!cancelled) setStatus('unsupported');
      }
    }
    check();
    return () => { cancelled = true; };
  }, [supported]);

  const subscribe = useCallback(async () => {
    if (!supported) return;
    setStatus('busy');
    setError(null);
    try {
      const permission = await Notification.requestPermission();
      if (permission !== 'granted') {
        setStatus(permission === 'denied' ? 'denied' : 'idle');
        return;
      }
      const [{ publicKey }, registration] = await Promise.all([
        staffPushApi.getVapidPublicKey(),
        navigator.serviceWorker.ready,
      ]);
      const existing = await registration.pushManager.getSubscription();
      const subscription = existing ?? await registration.pushManager.subscribe({
        userVisibleOnly: true,
        applicationServerKey: urlBase64ToUint8Array(publicKey),
      });
      await staffPushApi.subscribe(toPayload(subscription));
      setStatus('subscribed');
    } catch (err) {
      setStatus('error');
      setError(err instanceof Error ? err.message : 'Não foi possível activar as notificações.');
    }
  }, [supported]);

  const unsubscribe = useCallback(async () => {
    if (!supported) return;
    setStatus('busy');
    setError(null);
    try {
      const registration = await navigator.serviceWorker.ready;
      const existing = await registration.pushManager.getSubscription();
      if (existing) {
        await staffPushApi.unsubscribe(existing.endpoint);
        await existing.unsubscribe();
      }
      setStatus('idle');
    } catch (err) {
      setStatus('error');
      setError(err instanceof Error ? err.message : 'Não foi possível desligar as notificações.');
    }
  }, [supported]);

  return { supported, status, error, subscribe, unsubscribe };
}
