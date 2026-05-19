import { useCallback, useEffect, useMemo, useState } from 'react';
import { publicPortalApi, type BrowserPushSubscriptionPayload } from './api';

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
    keys: {
      p256dh: json.keys?.p256dh ?? '',
      auth: json.keys?.auth ?? '',
    },
  };
}

export function usePushSubscription(slug: string | undefined) {
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
        setStatus('denied');
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
    return () => {
      cancelled = true;
    };
  }, [supported]);

  const subscribe = useCallback(async () => {
    if (!slug || !supported) return;
    setStatus('busy');
    setError(null);

    try {
      const permission = await Notification.requestPermission();
      if (permission !== 'granted') {
        setStatus(permission === 'denied' ? 'denied' : 'idle');
        return;
      }

      const [{ publicKey }, registration] = await Promise.all([
        publicPortalApi.getVapidPublicKey(),
        navigator.serviceWorker.ready,
      ]);

      const existing = await registration.pushManager.getSubscription();
      const subscription = existing ?? await registration.pushManager.subscribe({
        userVisibleOnly: true,
        applicationServerKey: urlBase64ToUint8Array(publicKey),
      });

      await publicPortalApi.subscribePush(slug, toPayload(subscription));
      setStatus('subscribed');
    } catch (err) {
      setStatus('error');
      setError(err instanceof Error ? err.message : 'Não foi possível activar as notificações.');
    }
  }, [slug, supported]);

  const unsubscribe = useCallback(async () => {
    if (!slug || !supported) return;
    setStatus('busy');
    setError(null);

    try {
      const registration = await navigator.serviceWorker.ready;
      const existing = await registration.pushManager.getSubscription();
      if (existing) {
        await publicPortalApi.unsubscribePush(slug, existing.endpoint);
        await existing.unsubscribe();
      }
      setStatus('idle');
    } catch (err) {
      setStatus('error');
      setError(err instanceof Error ? err.message : 'Não foi possível desligar as notificações.');
    }
  }, [slug, supported]);

  return {
    supported,
    status,
    error,
    subscribe,
    unsubscribe,
  };
}
